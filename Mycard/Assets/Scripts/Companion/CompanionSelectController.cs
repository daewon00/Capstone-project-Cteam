using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Save;
using System.Collections.Generic;

/// <summary>
/// 동료 선택 흐름을 관리하고 초기 런 상태를 구성한 뒤 맵 씬으로 전환합니다.
/// </summary>
public class CompanionSelectController : MonoBehaviour
{
    [SerializeField] private string mapScene = "Map Scene";
    
    [Header("UI")]
    public Transform gridParent;
    public CompanionCardView cardPrefab;
    public Button startButton;
    public TMP_Text selectedLabel;

    private CompanionDefinition _selected;
    private CompanionDefinition[] _all;

    /// <summary>
    /// 데이터베이스 연결을 보장하고 동료 카드 목록을 구성한 뒤 UI 이벤트를 초기화합니다.
    /// </summary>
    void Start()
    {
        // DB 연결 보장(한 번만)
        DatabaseManager.Instance.Connect();

        // 동료 리스트 로드 (Resources/Companions 폴더에 저장된 SO)
        _all = Resources.LoadAll<CompanionDefinition>("Companions");

        // (옵션) 잠금 해제 필터링: 지금은 모두 표시
        foreach (var c in _all)
        {
            var item = Instantiate(cardPrefab, gridParent);
            item.Bind(c, OnSelect);
        }

        startButton.onClick.AddListener(OnClickStart);
        UpdateUI();
    }

    /// <summary>
    /// 선택된 동료 정보를 저장하고 전역 컨텍스트를 갱신한 뒤 UI를 새로고침합니다.
    /// </summary>
    void OnSelect(CompanionDefinition data)
    {
        _selected = data;
        GameContext.I.SelectedCompanionId = data.CompanionId;
        UpdateUI();
    }

    /// <summary>
    /// 동료 선택 여부에 따라 버튼 활성 상태를 조정하고 라벨에 현재 선택을 표시합니다.
    /// </summary>
    void UpdateUI()
    {
        startButton.interactable = _selected != null;
        if (selectedLabel) selectedLabel.text = _selected ? $"선택: {_selected.DisplayName}" : "동료를 선택하세요";
    }

    /// <summary>
    /// 선택된 동료를 기반으로 새 런을 생성·저장하고 관련 서비스를 재바인딩한 뒤 맵 씬을 로드합니다.
    /// </summary>
    void OnClickStart()
    {
        if (_selected == null) return;

        DatabaseManager.Instance.Connect();

        // 1. 새 게임을 위한 고유 ID와 정보를 생성합니다.
        var runId = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString("lastRunId", runId);
        PlayerPrefs.SetString("selectedCompanionId", _selected.CompanionId);
        PlayerPrefs.Save();

        // v3.0: 특전 스냅샷 생성 및 모디파이어 적용으로 시작 골드 계산
        var profileId = GameContext.I != null ? GameContext.I.ProfileId : "P1";
        var perkSvc = ServiceRegistry.Get<IPerkService>();
        var modSvc = ServiceRegistry.Get<IModifierService>();
        // 1) 런 스냅샷 생성 및 모디파이어 런 바인딩
        perkSvc?.ComputeRunSnapshotAndPersist(profileId, runId);
        modSvc?.RebindRun(runId);
        // 2) 시작 골드 계산: 기본값(300) + 동료 보너스 → 모디파이어 적용
        float baseGold = 300 + _selected.GoldBonus;
        float finalGold = modSvc != null ? modSvc.Apply("STARTING_GOLD", baseGold, ModifierScope.CurrentRun) : baseGold;

        var run = new CurrentRun {
            RunId = runId, ProfileId = profileId, // ProfileId는 나중에 로그인 시스템과 연동
            Act = 1, Floor = 0, NodeIndex = 0,
            Gold = Mathf.RoundToInt(finalGold),
            CurrentHp = 80 + _selected.MaxHpBonus,
            MaxHpBase = 80 + _selected.MaxHpBonus,
            EnergyMax = 3 + _selected.EnergyMaxBonus,
            CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = System.DateTime.UtcNow.ToString("o"),
        };

        // 2. 시작 덱과 유물을 '저장용 데이터' 형태로 완벽하게 만듭니다.
        int counter = 0;
        string NewId() => $"{runId}-{(++counter):X8}";

        var cards = new List<CardInDeck>();
        // 기본 덱
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_2", IsUpgraded = false });
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_3", IsUpgraded = false });
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_4", IsUpgraded = false });
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_5", IsUpgraded = false });
        // 동료 전용 카드
        foreach (var cid in _selected.StartingCardIds)
            cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = cid, IsUpgraded = false });

        var relics = _selected.StartingRelicIds
            .Select(id => new RelicInPossession { RunId = runId, RelicId = id, Stacks = 1, UsesLeft = -1 })
            .ToList();
        // 동료 자체를 '특별 유물'로 저장
        relics.Add(new RelicInPossession { RunId = runId, RelicId = "COMP_" + _selected.CompanionId, Stacks = 1, UsesLeft = -1 });

        var potions = _selected.StartingPotionIds
            .Select(id => new PotionInPossession { RunId = runId, PotionId = id, Charges = 1 })
            .ToList();

        // 3. 완성된 '첫 번째 세이브 파일'을 DB에 저장합니다. (세분화된 API 사용)
        var db = ServiceRegistry.GetRequired<IDatabase>();
        db.UpsertCurrentRun(run);
        db.ReplaceCardsInDeck(runId, cards);
        db.ReplaceRelics(runId, relics);
        db.ReplacePotions(runId, potions);

        // 3.5. 월렛을 새로운 런에 재바인딩하여 UI와 동기화합니다.
        ServiceRegistry.Get<IWalletService>()?.RebindRun(runId);

        // 3.6. 덱 서비스에 신규 런을 로드/준비시켜 캐시 및 RNG 동기화
        ServiceRegistry.Get<IDeckService>()?.LoadAndPrepareDeck(runId);

        // 3.7. 런 서비스에도 컨텍스트를 주입해 전투 결과 보고가 정확히 동작하도록 합니다.
        ServiceRegistry.Get<IRunService>()?.RebindRun(runId);

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            stageService.RebindRun(runId);
            var locationPayload = new RunStagePayloads.Location
            {
                act = run.Act,
                floor = run.Floor,
                nodeIndex = run.NodeIndex
            };
            stageService.SetStage(RunStageType.Map, mapScene, RunStageService.ToJson(locationPayload));
        }

        // 3.8. 이벤트 매니저 등록(조건부): 런이 생성된 시점에 EventManager를 등록합니다.
        try
        {
            var em = new EventManager(db, runId);
            ServiceRegistry.Register<IEventManager>(em);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossFlow][CompanionSelect] Registered IEventManager for runId='{runId}'");
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CompanionSelect] EventManager registration failed: {e.Message}");
        }

        // 4. 맵 씬으로 이동합니다.
        SceneManager.LoadScene(mapScene);
    }

    /// <summary>
    /// UI 없이 동료 정의만으로 런을 시작하던 이전 버전의 진입점입니다.
    /// </summary>
    void StartNewRunWithCompanion(CompanionDefinition comp)
    {
        // 새 런 ID
        var runId = System.Guid.NewGuid().ToString("N");
        GameContext.I.RunId = runId;

        // 기본 런 데이터
        var run = new CurrentRun {
            RunId = runId,
            ProfileId = GameContext.I.ProfileId,
            Act = 1, Floor = 0, NodeIndex = 0,
            Gold = 99 + comp.GoldBonus,
            CurrentHp = 70 + comp.MaxHpBonus,
            MaxHpBase = 80 + comp.MaxHpBonus,
            MaxHpFromPerks = 0,
            MaxHpFromRelics = 0,
            EnergyMax = 3 + comp.EnergyMaxBonus,
            Keys = 0,
            CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = System.DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion = "content-1",
            AppVersion = Application.version
        };

        // 덱 초기화
        var deck = FindObjectOfType<DeckManager>();
        deck.InitForRun(runId, persistedCards: null);

        // 기본 스타팅 덱(원하면 프로젝트 규칙대로)
        deck.CreateNewCardInstance("CARD_STRIKE", false);
        deck.CreateNewCardInstance("CARD_STRIKE", false);
        deck.CreateNewCardInstance("CARD_DEFEND", false);

        // 동료 스타팅 카드
        foreach (var cardId in comp.StartingCardIds)
            deck.CreateNewCardInstance(cardId, false);

        // 동료를 '특수 유물'로 저장해 런 전체에 남도록 (스키마 변경 불필요)
        var relicRows = comp.StartingRelicIds
            .Select(id => new RelicInPossession { RunId = runId, RelicId = id, Stacks = 1, UsesLeft = -1 })
            .ToList();

        relicRows.Add(new RelicInPossession { RunId = runId, RelicId = "COMP_" + comp.CompanionId, Stacks = 1, UsesLeft = -1 });

        // 포션
        var potRows = comp.StartingPotionIds
            .Select(id => new PotionInPossession { RunId = runId, PotionId = id, Charges = 1 })
            .ToList();


        // 맵/이벤트/RNG 초기값은 빈 리스트로 시작 (세분화된 API 사용)
        var db = ServiceRegistry.GetRequired<IDatabase>();
        db.UpsertCurrentRun(run);
        db.ReplaceCardsInDeck(runId, deck.ToCardRowsForSave());
        db.ReplaceRelics(runId, relicRows);
        db.ReplacePotions(runId, potRows);

        PlayerPrefs.SetString("lastRunId", runId);
        PlayerPrefs.Save();

        // 월렛 재바인딩 + 덱 서비스 로드 (안전)
        ServiceRegistry.Get<IWalletService>()?.RebindRun(runId);
        ServiceRegistry.Get<IDeckService>()?.LoadAndPrepareDeck(runId);
        ServiceRegistry.Get<IRunService>()?.RebindRun(runId);
    }
}

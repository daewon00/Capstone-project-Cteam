using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Save;
using System.Collections.Generic;

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

    void OnSelect(CompanionDefinition data)
    {
        _selected = data;
        GameContext.I.SelectedCompanionId = data.CompanionId;
        UpdateUI();
    }

    void UpdateUI()
    {
        startButton.interactable = _selected != null;
        if (selectedLabel) selectedLabel.text = _selected ? $"선택: {_selected.DisplayName}" : "동료를 선택하세요";
    }

    void OnClickStart()
    {
        if (_selected == null) return;

        DatabaseManager.Instance.Connect();

        // 1. 새 게임을 위한 고유 ID와 정보를 생성합니다.
        var runId = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString("lastRunId", runId);
        PlayerPrefs.SetString("selectedCompanionId", _selected.CompanionId);
        PlayerPrefs.Save();

        var run = new CurrentRun {
            RunId = runId, ProfileId = "P1", // ProfileId는 나중에 로그인 시스템과 연동
            Act = 1, Floor = 0, NodeIndex = 0,
            Gold = 99 + _selected.GoldBonus,
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
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_STRIKE", IsUpgraded = false });
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_STRIKE", IsUpgraded = false });
        cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = "CARD_DEFEND", IsUpgraded = false });
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

        // 3. 완성된 '첫 번째 세이브 파일'을 DB에 저장합니다.
        DatabaseManager.Instance.SaveCurrentRun(run, cards, relics, potions,
            nodes: new List<MapNodeState>(), rngStates: new List<RngState>());

        // 4. 맵 씬으로 이동합니다.
        SceneManager.LoadScene(mapScene);
    }

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

        // 맵/이벤트/RNG 초기값은 빈 리스트로 시작
        DatabaseManager.Instance.SaveCurrentRun(
            run,
            deck.ToCardRowsForSave(),
            relicRows,
            potRows,
            nodes: new System.Collections.Generic.List<MapNodeState>(),
            rngStates: new System.Collections.Generic.List<RngState>()
        );

        PlayerPrefs.SetString("lastRunId", runId);
        PlayerPrefs.Save();
    }
}
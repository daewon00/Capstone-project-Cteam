using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Save;

/// <summary>
/// 동료 선택 흐름을 관리하고 초기 런 상태를 구성한 뒤 맵 씬으로 전환합니다.
/// </summary>
public class CompanionSelectController : MonoBehaviour
{
    [SerializeField] private string mapScene = "Map Scene";
    [SerializeField] private string tutorialBattleScene = "Battle_android";
    
    [Header("Run Defaults")]
    [SerializeField] private int startingAct = 1;
    [SerializeField] private int startingFloor = 0;
    [SerializeField] private int startingNodeIndex = 0;
    [SerializeField] private int baseMaxHp = 80;
    [SerializeField] private int baseEnergyMax = 3;
    [SerializeField] private float baseStartingGold = 300f;

    [Header("UI")]
    [SerializeField] private CompanionCarouselPresenter carousel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button detailButton;
    [SerializeField] private TMP_Text selectedLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private string previousScene = "Main Menu";

    [Header("FX")]
    [SerializeField] private CompanionSelectFxController fxController;

    private CompanionDefinition _selected;
    private CompanionDefinition[] _all;
    private bool _allowTutorialSelectionReport;

    /// <summary>
    /// 데이터베이스 연결을 보장하고 동료 카드 목록을 구성한 뒤 UI 이벤트를 초기화합니다.
    /// </summary>
    void Start()
    {
        // DB 연결 보장(한 번만)
        DatabaseManager.Instance.Connect();

        // 동료 리스트 로드 (Resources/Companions 폴더에 저장된 SO)
        _all = Resources.LoadAll<CompanionDefinition>("Companions");

        int initialIndex = 0;
        if (_all != null && _all.Length > 0)
        {
            var previouslySelectedId = GameContext.I != null
                ? GameContext.I.SelectedCompanionId
                : PlayerPrefs.GetString("selectedCompanionId", string.Empty);

            if (!string.IsNullOrEmpty(previouslySelectedId))
            {
                for (int i = 0; i < _all.Length; i++)
                {
                    if (_all[i] != null && string.Equals(_all[i].CompanionId, previouslySelectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        initialIndex = i;
                        break;
                    }
                }
            }
        }

        RegisterStaticTargets();
        ServiceRegistry.Get<ITutorialService>()?.BeginPreviewIfEligible(TutorialIds.CoreOnboarding);

        if (carousel != null)
        {
            carousel.SelectionChanged += OnSelect;
            carousel.Initialize(_all, initialIndex);
            _allowTutorialSelectionReport = true;
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnClickStart);
        }

        if (detailButton != null)
        {
            detailButton.onClick.AddListener(OnClickDetail);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnClickBack);
        }
        UpdateUI();
    }

    /// <summary>
    /// 선택된 동료 정보를 저장하고 전역 컨텍스트를 갱신한 뒤 UI를 새로고침합니다.
    /// </summary>
    void OnSelect(CompanionDefinition data)
    {
        _selected = data;
        if (_selected == null)
        {
            UpdateUI();
            return;
        }

        if (GameContext.I != null)
        {
            GameContext.I.SelectedCompanionId = data.CompanionId;
        }
        if (_allowTutorialSelectionReport)
        {
            ServiceRegistry.Get<ITutorialService>()?.ReportAction(TutorialRequiredActionType.ButtonClick, $"companion-select:{data.CompanionId}");
        }
        UpdateUI();
    }

    /// <summary>
    /// 동료 선택 여부에 따라 버튼 활성 상태를 조정하고 라벨에 현재 선택을 표시합니다.
    /// </summary>
    void UpdateUI()
    {
        if (startButton != null)
        {
            startButton.interactable = _selected != null;
        }
        if (detailButton != null)
        {
            detailButton.interactable = _selected != null;
        }
        if (fxController != null)
        {
            fxController.enabled = _selected != null;
        }
        if (selectedLabel) selectedLabel.text = _selected ? $"선택: {_selected.DisplayName}" : "동료를 선택하세요";
    }

    /// <summary>
    /// 선택된 동료를 기반으로 새 런을 생성·저장하고 관련 서비스를 재바인딩한 뒤 맵 씬을 로드합니다.
    /// </summary>
    void OnClickStart()
    {
        if (_selected == null) return;
        if (fxController == null)
        {
            BeginNewRun();
            return;
        }

        fxController.PlayConfirmFX(BeginNewRun);
    }

    void OnClickBack()
    {
        if (string.IsNullOrEmpty(previousScene))
            return;

        SceneManager.LoadScene(previousScene);
    }

    void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnClickStart);
        }
        if (detailButton != null)
        {
            detailButton.onClick.RemoveListener(OnClickDetail);
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnClickBack);
        }

        if (carousel != null)
        {
            _allowTutorialSelectionReport = false;
            carousel.SelectionChanged -= OnSelect;
        }
    }

    void OnClickDetail()
    {
        if (_selected == null)
        {
            Debug.LogWarning("[CompanionSelect] Detail button pressed without an active selection.");
            return;
        }

        var summary = $"{_selected.DisplayName} · HP +{_selected.MaxHpBonus} · Energy +{_selected.EnergyMaxBonus} · Gold +{_selected.GoldBonus}";
        var description = string.IsNullOrWhiteSpace(_selected.Description)
            ? "추가 설명이 아직 준비되지 않았습니다."
            : _selected.Description;

        Debug.Log($"[CompanionSelect] Detail\n{summary}\n{description}");
    }

    void BeginNewRun()
    {
        if (_selected == null)
        {
            Debug.LogWarning("[CompanionSelect] BeginNewRun called without a selected companion.");
            return;
        }

        if (startButton != null)
        {
            startButton.interactable = false;
        }

        try
        {
            DatabaseManager.Instance.Connect();

            // 1. 새 게임을 위한 고유 ID와 정보를 생성합니다.
            var runId = System.Guid.NewGuid().ToString("N");
            ServiceRegistry.Get<IRunLifecycleService>()?.RegisterNewRun(runId, _selected.CompanionId);

            // v3.0: 특전 스냅샷 생성 및 모디파이어 적용으로 시작 골드 계산
            var profileId = GameContext.I != null ? GameContext.I.ProfileId : "P1";
            var perkSvc = ServiceRegistry.Get<IPerkService>();
            var modSvc = ServiceRegistry.Get<IModifierService>();
            // 1) 런 스냅샷 생성 및 모디파이어 런 바인딩
            perkSvc?.ComputeRunSnapshotAndPersist(profileId, runId);
            modSvc?.RebindRun(runId);
            // 2) 시작 골드 계산: 기본값(인스펙터 설정) + 동료 보너스 → 모디파이어 적용
            float baseGold = baseStartingGold + _selected.GoldBonus;
            float finalGold = modSvc != null ? modSvc.Apply("STARTING_GOLD", baseGold, ModifierScope.CurrentRun) : baseGold;

            var tutorialService = ServiceRegistry.Get<ITutorialService>();
            bool startTutorialRun = tutorialService != null && tutorialService.BeginTutorialIfNeeded(TutorialIds.CoreOnboarding);

            var run = new CurrentRun {
                RunId = runId, ProfileId = profileId, // ProfileId는 나중에 로그인 시스템과 연동
                CompanionId = _selected.CompanionId,
                Act = startingAct,
                Floor = startingFloor,
                NodeIndex = startingNodeIndex,
                Gold = Mathf.RoundToInt(finalGold),
                CurrentHp = baseMaxHp + _selected.MaxHpBonus,
                MaxHpBase = baseMaxHp + _selected.MaxHpBonus,
                EnergyMax = baseEnergyMax + _selected.EnergyMaxBonus,
                IsTutorialRun = startTutorialRun,
                CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
                UpdatedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };

            // 2. 시작 덱과 유물을 '저장용 데이터' 형태로 완벽하게 만듭니다.
            int counter = 0;
            string NewId() => $"{runId}-{(++counter):X8}";

            var cards = new List<CardInDeck>();
            if (_selected.StartingCardIds != null && _selected.StartingCardIds.Count > 0)
            {
                foreach (var cid in _selected.StartingCardIds)
                {
                    if (string.IsNullOrEmpty(cid))
                        continue;
                    cards.Add(new CardInDeck { InstanceId = NewId(), RunId = runId, CardId = cid, IsUpgraded = false });
                }
            }
            else
            {
                Debug.LogWarning($"[CompanionSelect] Starting deck is empty for companion {_selected.CompanionId}. 런이 비어 있는 덱으로 시작합니다.");
            }

            var relics = _selected.StartingRelicIds
                .Select(id => new RelicInPossession { RunId = runId, RelicId = id, Stacks = 1, UsesLeft = -1 })
                .ToList();

            var potions = _selected.StartingPotionIds
                .Select(id => new PotionInPossession { RunId = runId, PotionId = id, Charges = 1 })
                .ToList();

            // 3. 완성된 '첫 번째 세이브 파일'을 단일 트랜잭션으로 저장합니다.
            var db = ServiceRegistry.GetRequired<IDatabase>();
            db.CreateNewRunSnapshot(run, cards, relics, potions);
            EnsureRunRngSeeds(runId, db);

            tutorialService?.BindRun(runId, startTutorialRun);
            tutorialService?.ReportAction(TutorialRequiredActionType.ButtonClick, "start-run");

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
                if (startTutorialRun)
                {
                    var battlePayload = new RunStagePayloads.Battle
                    {
                        act = run.Act,
                        floor = run.Floor,
                        nodeIndex = run.NodeIndex,
                        battleKind = (int)GameContext.BattleKind.Normal,
                        sceneName = tutorialBattleScene,
                        prevAct = run.Act,
                        prevFloor = run.Floor,
                        prevNodeIndex = run.NodeIndex,
                        prevBattleKind = (int)GameContext.BattleKind.Normal,
                        hasPrevLocation = false,
                        isPending = false
                    };
                    stageService.SetStage(RunStageType.Battle, tutorialBattleScene, RunStageService.ToJson(battlePayload));
                }
                else
                {
                    var locationPayload = new RunStagePayloads.Location
                    {
                        act = run.Act,
                        floor = run.Floor,
                        nodeIndex = run.NodeIndex
                    };
                    stageService.SetStage(RunStageType.Map, mapScene, RunStageService.ToJson(locationPayload));
                }
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

            if (startTutorialRun)
            {
                if (GameContext.I != null)
                {
                    GameContext.I.CurrentBattleKind = GameContext.BattleKind.Normal;
                }
                try
                {
                    PlayerPrefs.SetInt("currentBattleKind", (int)GameContext.BattleKind.Normal);
                    PlayerPrefs.Save();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CompanionSelect] Failed to persist currentBattleKind: {e.Message}");
                }
            }

            var nextScene = startTutorialRun ? tutorialBattleScene : mapScene;
            SceneManager.LoadScene(nextScene);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CompanionSelect] Failed to start new run: {e.Message}");
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }
    }

    private void RegisterStaticTargets()
    {
        var tutorialService = ServiceRegistry.Get<ITutorialService>();
        if (tutorialService == null) return;

        if (startButton != null)
        {
            // 이미 동일 ID가 등록되어 있다면 씬의 작성값을 존중하고 새로 추가/등록하지 않습니다.
            if (tutorialService.GetTargetRect("start-button") == null)
            {
                var target = EnsureTarget(startButton.gameObject, "start-button");
                target?.SetFocusRect(startButton.transform as RectTransform);
            }
        }

        if (carousel != null)
        {
            if (tutorialService.GetTargetRect("companion-carousel") == null)
            {
                var target = EnsureTarget(carousel.gameObject, "companion-carousel");
                target?.SetFocusRect(carousel.transform as RectTransform);
            }
        }
    }

    private static TutorialTarget EnsureTarget(GameObject go, string id)
    {
        if (go == null || string.IsNullOrEmpty(id)) return null;
        var target = go.GetComponent<TutorialTarget>() ?? go.AddComponent<TutorialTarget>();
        // 씬에서 이미 설정된 ID가 있다면 덮어쓰지 않습니다(WYSIWYG 보존)
        if (string.IsNullOrEmpty(target.TargetId))
        {
            target.SetId(id);
        }
        return target;
    }

    /// <summary>
    /// UI 없이 동료 정의만으로 런을 시작하던 이전 버전의 진입점입니다.
    /// </summary>
    void StartNewRunWithCompanion(CompanionDefinition comp)
    {
        // 새 런 ID
        var runId = System.Guid.NewGuid().ToString("N");
        ServiceRegistry.Get<IRunLifecycleService>()?.RegisterNewRun(runId, comp.CompanionId);

        // 기본 런 데이터
        var run = new CurrentRun {
            RunId = runId,
            ProfileId = GameContext.I.ProfileId,
            CompanionId = comp.CompanionId,
            Act = startingAct,
            Floor = startingFloor,
            NodeIndex = startingNodeIndex,
            Gold = Mathf.RoundToInt(baseStartingGold) + comp.GoldBonus,
            CurrentHp = baseMaxHp + comp.MaxHpBonus,
            MaxHpBase = baseMaxHp + comp.MaxHpBonus,
            MaxHpFromPerks = 0,
            MaxHpFromRelics = 0,
            EnergyMax = baseEnergyMax + comp.EnergyMaxBonus,
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

        // 동료가 제공하는 유물을 런 시작 시 보관
        var relicRows = comp.StartingRelicIds
            .Select(id => new RelicInPossession { RunId = runId, RelicId = id, Stacks = 1, UsesLeft = -1 })
            .ToList();

        // 포션
        var potRows = comp.StartingPotionIds
            .Select(id => new PotionInPossession { RunId = runId, PotionId = id, Charges = 1 })
            .ToList();


        // 맵/이벤트/RNG 초기값은 빈 리스트로 시작 (세분화된 API 사용)
        var db = ServiceRegistry.GetRequired<IDatabase>();
        db.CreateNewRunSnapshot(run, deck.ToCardRowsForSave(), relicRows, potRows);
        EnsureRunRngSeeds(runId, db);

        // 월렛 재바인딩 + 덱 서비스 로드 (안전)
        ServiceRegistry.Get<IWalletService>()?.RebindRun(runId);
        ServiceRegistry.Get<IDeckService>()?.LoadAndPrepareDeck(runId);
        ServiceRegistry.Get<IRunService>()?.RebindRun(runId);
    }

    private void EnsureRunRngSeeds(string runId, IDatabase db)
    {
        if (string.IsNullOrEmpty(runId)) return;

        var rngService = ServiceRegistry.Get<IRngService>();
        if (rngService == null) return;

        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "deck-shuffle",
            "reward-generation",
            "deck-init"
        };

        var existing = rngService.GetStatesForSave();
        if (existing != null)
        {
            foreach (var state in existing)
            {
                if (!string.IsNullOrEmpty(state?.Domain))
                {
                    domains.Add(state.Domain);
                }
            }
        }

        foreach (var domain in domains)
        {
            try
            {
                rngService.Seed(domain, HashRunIdToSeed(runId, domain));
            }
            catch (Exception e)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[CompanionSelect] Failed to seed RNG domain '{domain}': {e.Message}");
#endif
            }
        }

        var statesForSave = rngService.GetStatesForSave();
        if (statesForSave != null)
        {
            db?.UpsertRngStates(runId, statesForSave);
        }
    }

    private static uint HashRunIdToSeed(string runId, string domain)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (!string.IsNullOrEmpty(runId))
            {
                foreach (char c in runId)
                {
                    h ^= c;
                    h *= 16777619u;
                }
            }
            if (!string.IsNullOrEmpty(domain))
            {
                foreach (char c in domain)
                {
                    h ^= c;
                    h *= 16777619u;
                }
            }
            return h == 0u ? 1u : h;
        }
    }
}

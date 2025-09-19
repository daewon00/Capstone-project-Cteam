using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Save;
using BattleSnapshot;

/// <summary>
/// 전투 씬 진입 시 필요한 서비스와 컨트롤러를 조립하고 런 컨텍스트를 복원합니다.
/// </summary>
[DefaultExecutionOrder(-9000)]
public class BattleSceneBootstrap : MonoBehaviour
{
    [Header("연결할 컨트롤러")]
    [SerializeField] private BattleController _battleController;
    [SerializeField] private HandController _handController;
    [SerializeField] private Card _cardPrefab; // 핸드에 생성할 카드 프리팹(권장: 명시 지정)

    /// <summary>
    /// 전투 중 카드 인스턴스를 생성할 때 사용할 기본 카드 프리팹입니다.
    /// </summary>
    public static Card CardPrefabReference { get; private set; }

    /// <summary>
    /// 필수 의존성을 검증하고 핸드/덱 서비스 바인딩을 준비합니다.
    /// </summary>
    void Awake()
    {
        // 유효성 검사: 필수 컨트롤러 레퍼런스 확인
        if (_battleController == null || _handController == null)
        {
            Debug.LogError("[BattleSceneBootstrap] 필수 컨트롤러가 인스펙터에 연결되지 않았습니다! 초기화를 중단합니다.", this);
            this.enabled = false;
            return;
        }

        var deckService = ServiceRegistry.GetRequired<IDeckService>();
        var cardCatalog = ServiceRegistry.GetRequired<ICardCatalog>();

        if (deckService == null) Debug.LogWarning("[BattleSceneBootstrap] IDeckService를 찾지 못했습니다.");
        if (cardCatalog == null) Debug.LogWarning("[BattleSceneBootstrap] ICardCatalog를 찾지 못했습니다.");
        if (deckService != null) GameServices.RegisterDeck(deckService);
        CardPrefabReference = _cardPrefab;
        // HandServiceBinder를 부착하고 즉시 초기화합니다.
        var binder = _handController.GetComponent<HandServiceBinder>();
        if (binder == null) binder = _handController.gameObject.AddComponent<HandServiceBinder>();
        // 카드 프리팹이 있다면 바인더에 설정
        if (_cardPrefab != null)
        {
            var field = typeof(HandServiceBinder).GetField("_cardPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(binder, _cardPrefab);
        }
        binder.Initialize(_handController, deckService, cardCatalog);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleSceneBootstrap] HandServiceBinder initialized. hand={_handController!=null}, deckSvc={deckService!=null}, catalog={cardCatalog!=null}, cardPrefab={_cardPrefab!=null}");
#endif

        if (_battleController != null && deckService != null)
        {
            _battleController.Initialize(deckService);
            // Start 단계에서 전투를 개시하도록 준비합니다.
        }
        else
        {
            if (_battleController == null) Debug.LogWarning("[BattleSceneBootstrap] BattleController가 연결되지 않았습니다.");
        }

        var scheduler = FindObjectOfType<BattleSnapshotScheduler>();
        if (scheduler == null)
        {
            var go = new GameObject("BattleSnapshotScheduler");
            scheduler = go.AddComponent<BattleSnapshotScheduler>();
        }
        scheduler.Initialize();

    }

    /// <summary>
    /// 런 정보를 재구성하고 필요한 서비스를 재바인딩한 뒤 전투를 시작하거나 저장 상태를 복원합니다.
    /// </summary>
    void Start()
    {
        if (!this.enabled || _battleController == null) return;

        var runId = GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId)
            ? GameContext.I.RunId
            : PlayerPrefs.GetString("lastRunId", string.Empty);

        var runService = ServiceRegistry.Get<IRunService>();
        runService?.RebindRun(runId);

        var deckService = ServiceRegistry.Get<IDeckService>();
        var cardCatalog = ServiceRegistry.Get<ICardCatalog>();
        var rngService = ServiceRegistry.Get<IRngService>();

        RunLoadResult runData = null;
        CurrentRun runRow = null;
        if (!string.IsNullOrEmpty(runId))
        {
            try
            {
                runData = DatabaseManager.Instance.LoadCurrentRun(runId);
                runRow = runData?.Run;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleSceneBootstrap] LoadCurrentRun failed: {e.Message}");
            }
        }

        if (runRow != null)
        {
            int maxHp = runRow.MaxHpBase + runRow.MaxHpFromPerks + runRow.MaxHpFromRelics;
            _battleController.ApplyRunStats(runRow.CurrentHp, maxHp, runRow.EnergyMax);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (!string.IsNullOrEmpty(runId))
        {
            Debug.LogWarning("[BattleSceneBootstrap] Run data missing; using inspector defaults for battle stats.");
        }
#endif

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            stageService.RebindRun(runId);
            RunStagePayloads.Battle payload;
            if (!stageService.TryGetPayload(out payload) || payload == null)
            {
                if (runData == null && !string.IsNullOrEmpty(runId))
                {
                    try
                    {
                        runData = DatabaseManager.Instance.LoadCurrentRun(runId);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[BattleSceneBootstrap] Reload current run failed: {e.Message}");
                    }
                }
                payload = new RunStagePayloads.Battle
                {
                    act = runData?.Run?.Act ?? 0,
                    floor = runData?.Run?.Floor ?? 0,
                    nodeIndex = runData?.Run?.NodeIndex ?? 0,
                    battleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal)
                };
            }

            payload.sceneName = SceneManager.GetActiveScene().name;
            stageService.SetStage(RunStageType.Battle, payload.sceneName, RunStageService.ToJson(payload));
        }

        BattleSnapshotDTO resume = null;
        try
        {
            var db = ServiceRegistry.Get<IDatabase>();
            var battleState = db?.LoadActiveBattleState(runId);
            if (battleState != null && !string.IsNullOrEmpty(battleState.Json))
            {
                resume = JsonUtility.FromJson<BattleSnapshotDTO>(battleState.Json);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BattleSceneBootstrap] Failed to parse battle snapshot: {e.Message}");
        }

        var scheduler = BattleSnapshotScheduler.Instance;

        if (resume != null)
        {
            BattleController.SkipInitialSetup = true;
            scheduler?.SetCombatResolving(true);

            var context = new BattleSceneContext(_battleController, _handController, CardPointsController.instance, EnemyController.instance,
                deckService, cardCatalog, _cardPrefab, rngService);
            BattleSnapshotRestorer.Apply(resume, context);
            if (resume.turn != null)
            {
                _battleController.SetTurnStateFromSnapshot(resume.turn.turnNumber,
                    (BattleController.TurnOrder)resume.turn.phase,
                    resume.turn.playerMana,
                    resume.turn.playerMaxMana,
                    resume.turn.enemyMana,
                    resume.turn.enemyMaxMana,
                    resume.turn.battleEnded);
            }

            scheduler?.SetCombatResolving(false);
            scheduler?.RequestSnapshot("ResumeLoaded");
        }
        else
        {
            _battleController.StartBattle();
        }
    }
}

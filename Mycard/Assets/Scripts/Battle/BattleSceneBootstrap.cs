using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Save;
using BattleSnapshot;

// 씬 조립자: 컨트롤러보다 먼저 실행되어 서비스 주입을 담당합니다.
[DefaultExecutionOrder(-9000)]
public class BattleSceneBootstrap : MonoBehaviour
{
    [Header("연결할 컨트롤러")]
    [SerializeField] private BattleController _battleController;
    [SerializeField] private HandController _handController;
    [SerializeField] private Card _cardPrefab; // 핸드에 생성할 카드 프리팹(권장: 명시 지정)

    public static Card CardPrefabReference { get; private set; }

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
        // HandServiceBinder 부착 및 초기화
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
            // Start에서 전투 개시(모든 Awake 완료 후)
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

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            stageService.RebindRun(runId);
            RunStagePayloads.Battle payload;
            if (!stageService.TryGetPayload(out payload) || payload == null)
            {
                var runData = string.IsNullOrEmpty(runId) ? null : DatabaseManager.Instance.LoadCurrentRun(runId);
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

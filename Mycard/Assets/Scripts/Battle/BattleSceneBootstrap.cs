using System;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private EnemyController _enemyController;
    [SerializeField] private Card _cardPrefab; // 핸드에 생성할 카드 프리팹(권장: 명시 지정)

    [Header("Encounter Configs")]
    [SerializeField, Tooltip("일반 전투에 사용할 Encounter 설정")] private EnemyEncounterConfig _normalEncounter;
    [SerializeField, Tooltip("엘리트 전투에 사용할 Encounter 설정")] private EnemyEncounterConfig _eliteEncounter;
    [SerializeField, Tooltip("보스 전투에 사용할 Encounter 설정")] private EnemyEncounterConfig _bossEncounter;
    [SerializeField, Tooltip("비어 있는 경우 사용할 기본 Encounter 설정(선택)")] private EnemyEncounterConfig _fallbackEncounter;

    /// <summary>
    /// 전투 중 카드 인스턴스를 생성할 때 사용할 기본 카드 프리팹입니다.
    /// </summary>
    public static Card CardPrefabReference { get; private set; }

    private EnemyEncounterConfig _activeEncounter;

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

        EnsureEnemyControllerReference();

        var deckService = ServiceRegistry.GetRequired<IDeckService>();
        var cardCatalog = ServiceRegistry.GetRequired<ICardCatalog>();

        if (deckService == null) Debug.LogWarning("[BattleSceneBootstrap] IDeckService를 찾지 못했습니다.");
        if (cardCatalog == null) Debug.LogWarning("[BattleSceneBootstrap] ICardCatalog를 찾지 못했습니다.");
        if (deckService != null) GameServices.RegisterDeck(deckService);
        CardPrefabReference = _cardPrefab;

        ConfigureEncounter();

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

        _handController.ClearLayoutLocks();

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            stageService.RebindRun(runId);
            var previousStage = stageService.Current != null ? stageService.Current.Stage : RunStageType.Unknown;
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
                    battleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal),
                    prevAct = runData?.Run?.Act ?? 0,
                    prevFloor = runData?.Run?.Floor ?? 0,
                    prevNodeIndex = runData?.Run?.NodeIndex ?? 0,
                    prevBattleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal),
                    hasPrevLocation = false,
                    isPending = false
                };
            }

            payload.sceneName = SceneManager.GetActiveScene().name;
            if (_activeEncounter != null && string.IsNullOrEmpty(payload.enemyId))
            {
                    payload.enemyId = _activeEncounter.EncounterId;
            }
            payload.isPending = false;
            stageService.SetStage(RunStageType.Battle, payload.sceneName, RunStageService.ToJson(payload));
            if (previousStage == RunStageType.BattlePending && !string.IsNullOrEmpty(runId))
            {
                CommitBattleEntry(runId, payload);
            }
        }

        var tutorialService = ServiceRegistry.Get<ITutorialService>();
        if (tutorialService != null)
        {
            tutorialService.BindRun(runId, runRow?.IsTutorialRun ?? false);
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

    private void EnsureEnemyControllerReference()
    {
        if (_enemyController == null)
        {
            _enemyController = EnemyController.instance != null
                ? EnemyController.instance
                : FindObjectOfType<EnemyController>();
        }
    }

    private void ConfigureEncounter()
    {
        EnsureEnemyControllerReference();
        if (_enemyController == null || _battleController == null)
            return;

        var kind = ResolveBattleKind();
        string storedEnemyId = ResolveStoredEnemyId(out var storedKind);
        if (storedKind.HasValue)
            kind = storedKind.Value;

        var encounter = ResolveEncounter(kind, storedEnemyId);
        if (encounter == null)
        {
            Debug.LogWarning("[BattleSceneBootstrap] Encounter 구성을 찾지 못해 기본 설정으로 진행합니다.");
            return;
        }

        _activeEncounter = encounter;
        _enemyController.ApplyEncounter(encounter);
        _battleController.ApplyEnemyStats(encounter.EnemyBaseHealth, encounter.EnemyMaxMana, encounter.EnemyStartingMana);
    }

    private GameContext.BattleKind ResolveBattleKind()
    {
        if (GameContext.I != null)
            return GameContext.I.CurrentBattleKind;

        return (GameContext.BattleKind)PlayerPrefs.GetInt("currentBattleKind", (int)GameContext.BattleKind.Normal);
    }

    private string ResolveStoredEnemyId(out GameContext.BattleKind? storedKind)
    {
        storedKind = null;
        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null && stageService.TryGetPayload(out RunStagePayloads.Battle payload) && payload != null)
        {
            if (payload.battleKind >= 0 && payload.battleKind <= (int)GameContext.BattleKind.Boss)
            {
                storedKind = (GameContext.BattleKind)payload.battleKind;
            }
            return payload.enemyId;
        }
        return null;
    }

    private EnemyEncounterConfig ResolveEncounter(GameContext.BattleKind kind, string enemyId)
    {
        var encounter = FindEncounterById(enemyId);
        if (encounter != null)
            return encounter;

        encounter = GetEncounterForKind(kind);
        if (encounter != null)
            return encounter;

        return BuildRuntimeFallback(kind);
    }

    private void CommitBattleEntry(string runId, RunStagePayloads.Battle payload)
    {
        var db = ServiceRegistry.Get<IDatabase>();
        if (db == null) return;
        try
        {
            db.UpdateRunPosition(runId, payload.act, payload.floor, payload.nodeIndex);

            MapNodeState nodeState = null;
            try
            {
                var runData = db.LoadCurrentRun(runId);
                nodeState = runData?.Nodes?.FirstOrDefault(n =>
                    n.RunId == runId && n.Act == payload.act && n.Floor == payload.floor && n.NodeIndex == payload.nodeIndex);
            }
            catch { }

            if (nodeState == null)
            {
                nodeState = new MapNodeState
                {
                    RunId = runId,
                    Act = payload.act,
                    Floor = payload.floor,
                    NodeIndex = payload.nodeIndex,
                    Type = ResolveNodeType(payload.battleKind)
                };
            }
            else
            {
                nodeState.Type = ResolveNodeType(payload.battleKind);
            }

            nodeState.Visited = true;
            db.UpsertNodeState(nodeState);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BattleSceneBootstrap] Failed to commit battle entry: {e.Message}");
        }
    }

    private static NodeType ResolveNodeType(int battleKind)
    {
        switch ((GameContext.BattleKind)Mathf.Clamp(battleKind, 0, (int)GameContext.BattleKind.Boss))
        {
            case GameContext.BattleKind.Elite:
                return NodeType.Elite;
            case GameContext.BattleKind.Boss:
                return NodeType.Boss;
            default:
                return NodeType.Battle;
        }
    }

    private EnemyEncounterConfig GetEncounterForKind(GameContext.BattleKind kind)
    {
        switch (kind)
        {
            case GameContext.BattleKind.Elite:
                return _eliteEncounter ?? _fallbackEncounter;
            case GameContext.BattleKind.Boss:
                return _bossEncounter ?? _fallbackEncounter;
            default:
                return _normalEncounter ?? _fallbackEncounter;
        }
    }

    private EnemyEncounterConfig FindEncounterById(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId))
            return null;

        foreach (var candidate in EnumerateConfiguredEncounters())
        {
            if (candidate != null && string.Equals(candidate.EncounterId, enemyId, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private IEnumerable<EnemyEncounterConfig> EnumerateConfiguredEncounters()
    {
        if (_normalEncounter != null) yield return _normalEncounter;
        if (_eliteEncounter != null) yield return _eliteEncounter;
        if (_bossEncounter != null) yield return _bossEncounter;
        if (_fallbackEncounter != null) yield return _fallbackEncounter;
    }

    private EnemyEncounterConfig BuildRuntimeFallback(GameContext.BattleKind kind)
    {
        if (_enemyController == null)
            return null;

        var runtime = ScriptableObject.CreateInstance<EnemyEncounterConfig>();
        runtime.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.DontSave;
        runtime.SetEncounterId($"{kind}_Fallback");
        runtime.SetAiOptions(new[] { _enemyController.CurrentAIType });
        runtime.SetStartHandSize(_enemyController.startHandSize);
        runtime.SetDrawPerTurn(_enemyController.DrawPerTurn);
        int baseHp = _battleController != null ? Mathf.Max(1, _battleController.enemyHealth) : 10;
        int maxMana = _battleController != null ? Mathf.Max(1, _battleController.enemymaxMana) : 3;
        int startMana = _battleController != null ? Mathf.Max(0, _battleController.startingEnemeyMana) : maxMana;
        runtime.SetEnemyStats(baseHp, maxMana, startMana);
        runtime.SetDeck(_enemyController.DeckTemplate);
        return runtime;
    }
}

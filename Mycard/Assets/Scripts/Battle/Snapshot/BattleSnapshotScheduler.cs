// 전투 진행 중 정기적으로 스냅샷을 생성하고 데이터베이스에 저장하는 스케줄러입니다.
using System;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public class BattleSnapshotScheduler : MonoBehaviour
{
    public static BattleSnapshotScheduler Instance { get; private set; }

    [SerializeField] private float cooldownSeconds = 0f;

    private float _lastSnapshotTime;
    private string _pendingReason;
    private bool _isResolving;
    private bool _initialized;

    private IDatabase _database;
    private IRngService _rngService;
    private IDeckService _deckService;
    private IRunStageService _stageService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 서비스 레지스트리에서 필요한 의존성을 가져오고 중복 초기화를 방지합니다.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _database = ServiceRegistry.GetRequired<IDatabase>();
        _rngService = ServiceRegistry.Get<IRngService>();
        _deckService = ServiceRegistry.Get<IDeckService>();
        _stageService = ServiceRegistry.Get<IRunStageService>();
        _initialized = true;
    }

    /// <summary>
    /// 전투 종료 처리 중 여부를 설정해 스냅샷 생성을 잠그거나 해제합니다.
    /// </summary>
    public void SetCombatResolving(bool value)
    {
        _isResolving = value;
    }

    /// <summary>
    /// 외부에서 스냅샷 요청을 수신하고 쿨다운/전투 상태를 고려해 예약합니다.
    /// </summary>
    public void RequestSnapshot(string reason)
    {
        if (!_initialized) Initialize();
        GameLog.Info($"[BattleSnapshotScheduler] RequestSnapshot reason={reason}, resolving={_isResolving}");
        if (_isResolving)
        {
            GameLog.Info("[BattleSnapshotScheduler] Snapshot request ignored: combat resolving.");
            return;
        }

        if (cooldownSeconds > 0f && Time.unscaledTime - _lastSnapshotTime < cooldownSeconds)
        {
            _pendingReason = reason;
            GameLog.Info($"[BattleSnapshotScheduler] Snapshot deferred (cooldown). reason={reason}");
            return;
        }

        CaptureNow(reason);
    }

    /// <summary>
    /// 쿨다운 경과 시 지연된 스냅샷을 실행합니다.
    /// </summary>
    private void Update()
    {
        if (!string.IsNullOrEmpty(_pendingReason) && Time.unscaledTime - _lastSnapshotTime >= cooldownSeconds && !_isResolving)
        {
            CaptureNow(_pendingReason);
            _pendingReason = string.Empty;
        }
    }

    /// <summary>
    /// 즉시 스냅샷을 생성하여 DB에 저장하고 진행중인 런 정보를 유지합니다.
    /// </summary>
    private void CaptureNow(string reason)
    {
        var snapshot = BattleSnapshotBuilder.Capture(reason);
        if (snapshot == null) return;
        var runId = ResolveRunId();
        if (string.IsNullOrEmpty(runId))
        {
            GameLog.Warn("[BattleSnapshotScheduler] Capture skipped: no runId");
            return;
        }

        try
        {
            var json = JsonUtility.ToJson(snapshot);
            _database.UpsertActiveBattleState(runId, json);
            _lastSnapshotTime = Time.unscaledTime;
            MaintainStage(runId, snapshot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[BattleSnapshotScheduler] Snapshot saved ({reason}) runId={runId}");
#endif
        }
        catch (Exception e)
        {
            GameLog.Warn($"[BattleSnapshotScheduler] Capture failed: {e.Message}");
        }
    }

    /// <summary>
    /// 런 스테이지 서비스를 통해 현재 전투 위치 정보를 갱신합니다.
    /// </summary>
    private void MaintainStage(string runId, BattleSnapshotDTO snapshot)
    {
        if (_stageService == null) return;
        var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName) ||
            sceneName.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) < 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[BattleSnapshotScheduler] MaintainStage skipped outside battle scene (scene='{sceneName}')");
#endif
            return;
        }

        var payload = new RunStagePayloads.Battle
        {
            act = 0,
            floor = 0,
            nodeIndex = 0,
            battleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal),
            sceneName = sceneName,
            prevAct = 0,
            prevFloor = 0,
            prevNodeIndex = 0,
            prevBattleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal),
            hasPrevLocation = false,
            isPending = false
        };
        _stageService.SetStage(RunStageType.Battle, payload.sceneName, RunStageService.ToJson(payload));
    }

    /// <summary>
    /// 런 스테이지 혹은 GameContext에서 현재 런 ID를 추적합니다.
    /// </summary>
    private string ResolveRunId()
    {
        if (_stageService != null && !string.IsNullOrEmpty(_stageService.RunId))
            return _stageService.RunId;

        if (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
            return GameContext.I.RunId;

        return PlayerPrefs.GetString("lastRunId", string.Empty);
    }
}

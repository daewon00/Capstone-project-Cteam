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

    public void Initialize()
    {
        if (_initialized) return;
        _database = ServiceRegistry.GetRequired<IDatabase>();
        _rngService = ServiceRegistry.Get<IRngService>();
        _deckService = ServiceRegistry.Get<IDeckService>();
        _stageService = ServiceRegistry.Get<IRunStageService>();
        _initialized = true;
    }

    public void SetCombatResolving(bool value)
    {
        _isResolving = value;
    }

    public void RequestSnapshot(string reason)
    {
        if (!_initialized) Initialize();
        Debug.Log($"[BattleSnapshotScheduler] RequestSnapshot reason={reason}, resolving={_isResolving}");
        if (_isResolving)
        {
            Debug.Log("[BattleSnapshotScheduler] Snapshot request ignored: combat resolving.");
            return;
        }

        if (cooldownSeconds > 0f && Time.unscaledTime - _lastSnapshotTime < cooldownSeconds)
        {
            _pendingReason = reason;
            Debug.Log($"[BattleSnapshotScheduler] Snapshot deferred (cooldown). reason={reason}");
            return;
        }

        CaptureNow(reason);
    }

    private void Update()
    {
        if (!string.IsNullOrEmpty(_pendingReason) && Time.unscaledTime - _lastSnapshotTime >= cooldownSeconds && !_isResolving)
        {
            CaptureNow(_pendingReason);
            _pendingReason = string.Empty;
        }
    }

    private void CaptureNow(string reason)
    {
        var snapshot = BattleSnapshotBuilder.Capture(reason);
        if (snapshot == null) return;
        var runId = ResolveRunId();
        if (string.IsNullOrEmpty(runId))
        {
            Debug.LogWarning("[BattleSnapshotScheduler] Capture skipped: no runId");
            return;
        }

        try
        {
            var json = JsonUtility.ToJson(snapshot);
            _database.UpsertActiveBattleState(runId, json);
            _lastSnapshotTime = Time.unscaledTime;
            MaintainStage(runId, snapshot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BattleSnapshotScheduler] Snapshot saved ({reason}) runId={runId}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BattleSnapshotScheduler] Capture failed: {e.Message}");
        }
    }

    private void MaintainStage(string runId, BattleSnapshotDTO snapshot)
    {
        if (_stageService == null) return;
        var payload = new RunStagePayloads.Battle
        {
            act = 0,
            floor = 0,
            nodeIndex = 0,
            battleKind = (int)(GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal),
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };
        _stageService.SetStage(RunStageType.Battle, payload.sceneName, RunStageService.ToJson(payload));
    }

    private string ResolveRunId()
    {
        if (_stageService != null && !string.IsNullOrEmpty(_stageService.RunId))
            return _stageService.RunId;

        if (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
            return GameContext.I.RunId;

        return PlayerPrefs.GetString("lastRunId", string.Empty);
    }
}

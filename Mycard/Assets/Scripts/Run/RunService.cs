using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Save;

public class RunService : IRunService
{
    private readonly IDatabase _database;
    private readonly IRngService _rngService;

    private string _runId;
    private bool _hasCommitted = false;

    public event Action OnRunEnded;

    public RunService(IDatabase database, IRngService rngService)
    {
        _database = database;
        _rngService = rngService;
    }

    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;
        _hasCommitted = false;
        Debug.Log($"[RunService] Rebound to Run ID: {_runId}");
    }

    public void ReportCombatEnded(CombatResult result)
    {
        if (_hasCommitted)
        {
            Debug.LogWarning("[RunService] Combat result already committed. Ignoring.");
            return;
        }
        _hasCommitted = true;

        switch (result)
        {
            case CombatResult.Victory:
                ProcessVictory();
                break;
            case CombatResult.Defeat:
                ProcessDefeat();
                break;
        }
    }

    private void ProcessVictory()
    {
        Debug.Log("[RunService] Processing VICTORY...");

        var lr = _database.LoadCurrentRun(_runId);
        if (lr == null || lr.Run == null)
        {
            Debug.LogError($"[RunService] Failed to load run {_runId}. Did you start the battle scene directly or finish the run earlier? Routing to Main Menu.");
            try { SceneManager.LoadScene("Main Menu"); } catch { }
            return;
        }

        var run = lr.Run;
        var nodeState = lr.Nodes?.FirstOrDefault(n =>
            n.Act == run.Act && n.Floor == run.Floor && n.NodeIndex == run.NodeIndex)
            ?? new MapNodeState { RunId = _runId, Act = run.Act, Floor = run.Floor, NodeIndex = run.NodeIndex };

        var rewardContainer = GenerateRewards();
        nodeState.RewardsJson = JsonUtility.ToJson(rewardContainer);
        nodeState.Cleared = true;

        _database.UpsertNodeState(nodeState);
        _database.UpsertRngStates(_runId, _rngService.GetStatesForSave());

        Debug.Log("[RunService] Node cleared. Rewards saved. Transitioning to Map Scene.");
        SceneManager.LoadScene("Map Scene");
    }

    private RewardContainer GenerateRewards()
    {
        var container = new RewardContainer();
        int goldAmount = _rngService.NextInt("reward-generation", 80, 121);
        container.Items.Add(new RewardItem { Type = "Gold", Amount = goldAmount });
        return container;
    }

    private void ProcessDefeat()
    {
        Debug.Log("[RunService] Processing DEFEAT...");

        var lr = _database.LoadCurrentRun(_runId);
        var summary = new RunSummary
        {
            RunId = _runId,
            ProfileId = lr?.Run?.ProfileId ?? "default_profile",
            Cleared = false,
            EndedAtUtc = DateTime.UtcNow.ToString("o")
        };
        _database.EndRunAndSummarize(summary);

        Debug.Log($"[RunService] Run {_runId} ended. Firing OnRunEnded and transitioning to Main Menu.");
        OnRunEnded?.Invoke();
        // 패배로 런이 종료되었으므로 이어하기 키를 정리해 혼선을 방지합니다.
        try { PlayerPrefs.DeleteKey("lastRunId"); PlayerPrefs.Save(); } catch { }
        SceneManager.LoadScene("Main Menu");
    }
}

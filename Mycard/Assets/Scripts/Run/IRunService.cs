using System;

public enum CombatResult { Victory, Defeat }

public interface IRunService
{
    void RebindRun(string runId);
    void ReportCombatEnded(CombatResult result);
    event Action OnRunEnded;
}


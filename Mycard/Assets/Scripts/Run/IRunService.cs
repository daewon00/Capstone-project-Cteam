using System;

/// <summary>
/// 전투 결과를 보고하고 런 수명주기를 관리하는 서비스 계약입니다.
/// </summary>
public enum CombatResult { Victory, Defeat }

/// <summary>
/// 런 재바인딩과 전투 종료 보고를 제공하는 서비스 인터페이스입니다.
/// </summary>
public interface IRunService
{
    void RebindRun(string runId);
    void ReportCombatEnded(CombatResult result);
    event Action OnRunEnded;
}


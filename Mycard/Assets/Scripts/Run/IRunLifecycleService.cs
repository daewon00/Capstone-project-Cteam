/// <summary>
/// 런 생성/삭제와 관련된 전역 상태(저장 슬롯, PlayerPrefs 등)를 관리하는 서비스 계약입니다.
/// </summary>
public interface IRunLifecycleService
{
    /// <summary>
    /// PlayerPrefs 및 데이터베이스에 유효한 진행 중인 런이 있는지 확인합니다.
    /// </summary>
    bool HasActiveRun();

    /// <summary>
    /// 현재 이어할 런 ID를 반환합니다. 없으면 빈 문자열을 반환합니다.
    /// </summary>
    string GetActiveRunId();

    /// <summary>
    /// 현재 진행 중인 런과 관련된 저장 데이터를 모두 정리합니다.
    /// </summary>
    void ResetActiveRun();

    /// <summary>
    /// 새 런이 시작될 때 PlayerPrefs 및 게임 컨텍스트를 갱신합니다.
    /// </summary>
    void RegisterNewRun(string runId, string companionId);
}

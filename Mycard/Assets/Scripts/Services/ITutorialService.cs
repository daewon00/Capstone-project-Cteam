using System;

/// <summary>
/// 튜토리얼 상태를 관리하고 씬간에 일관된 진행도를 전달하는 계약입니다.
/// </summary>
public interface ITutorialService
{
    event Action<TutorialStep> OnStepChanged;

    bool IsActive { get; }
    bool IsTutorialRun { get; }
    string ActiveTutorialId { get; }
    TutorialStep CurrentStep { get; }

    /// <summary>
    /// 현재 프로필의 진행도를 불러오고 캐시합니다.
    /// </summary>
    void RebindProfile(string profileId);

    /// <summary>
    /// 지정된 튜토리얼이 필요하다면 활성화하고, 활성화 여부를 반환합니다.
    /// </summary>
    bool BeginTutorialIfNeeded(string tutorialId);

    /// <summary>
    /// 현재 진행 중인 런과 튜토리얼 런 여부를 바인딩합니다.
    /// </summary>
    void BindRun(string runId, bool isTutorialRun);

    /// <summary>
    /// 튜토리얼 런 도중 전투가 끝났을 때 호출합니다.
    /// </summary>
    void NotifyBattleCompleted();

    /// <summary>
    /// 맵에서 새로운 노드를 방문했을 때 호출합니다.
    /// </summary>
    void NotifyMapNodeVisited(int act, int floor, int nodeIndex);

    /// <summary>
    /// 튜토리얼에서 허용되는 맵 이동인지 검사합니다.
    /// </summary>
    bool CanMoveToNode(int act, int floor, int nodeIndex);

    /// <summary>
    /// 튜토리얼이 완료되었을 때 호출합니다.
    /// </summary>
    void CompleteTutorial(string tutorialId);

    /// <summary>
    /// 지정된 튜토리얼을 이미 완료했는지 확인합니다.
    /// </summary>
    bool HasCompleted(string tutorialId);

    /// <summary>
    /// 런이 중단 또는 리셋되었을 때 튜토리얼 상태를 초기화합니다.
    /// </summary>
    void ResetActiveRun();
}

using Game.Save;

/// <summary>
/// 런 재개 시 어떤 씬과 스테이지를 불러올지 결정하기 위한 정보입니다.
/// </summary>
public struct RunStageResumeDecision
{
    public RunStageType Stage;
    public string SceneName;
    public string PayloadJson;

    public bool HasValidScene => !string.IsNullOrEmpty(SceneName);
}

/// <summary>
/// 런 진행 단계 정보를 저장하고 재개 결정을 제공하는 서비스 계약입니다.
/// </summary>
public interface IRunStageService
{
    RunStageState Current { get; }
    string RunId { get; }
    string CurrentSceneHint { get; }
    string CurrentPayloadJson { get; }

    void RebindRun(string runId);
    void ClearStage();
    void SetStage(RunStageType stage, string sceneHint = null, string payloadJson = null);
    RunStageResumeDecision GetResumeDecision();
    bool TryGetPayload<T>(out T payload) where T : class;
}

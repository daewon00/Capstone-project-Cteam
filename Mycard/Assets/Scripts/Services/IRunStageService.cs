using Game.Save;

public struct RunStageResumeDecision
{
    public RunStageType Stage;
    public string SceneName;
    public string PayloadJson;

    public bool HasValidScene => !string.IsNullOrEmpty(SceneName);
}

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

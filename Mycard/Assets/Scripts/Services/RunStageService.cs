using System;
using Game.Save;
using UnityEngine;

/// <summary>
/// 런 진행 단계 정보를 DB에 저장하고 씬 재개를 위한 데이터를 제공하는 서비스입니다.
/// </summary>
public sealed class RunStageService : IRunStageService
{
    private readonly IDatabase _db;
    private string _runId = string.Empty;
    private RunStageState _current;

    private const string LogTag = "[RunStageService]";

    /// <summary>
    /// 런 스테이지 서비스에 사용할 데이터베이스를 주입합니다.
    /// </summary>
    public RunStageService(IDatabase database)
    {
        _db = database;
    }

    public RunStageState Current => _current;
    public string RunId => _runId;
    public string CurrentSceneHint => _current?.SceneHint ?? string.Empty;
    public string CurrentPayloadJson => _current?.PayloadJson ?? string.Empty;

    /// <summary>
    /// 현재 런 ID를 바인딩하고 저장된 단계 정보를 로드합니다.
    /// </summary>
    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;
        _current = string.IsNullOrEmpty(_runId) ? null : _db.LoadRunStageState(_runId);
    }

    /// <summary>
    /// 진행 중인 스테이지 정보를 삭제합니다.
    /// </summary>
    public void ClearStage()
    {
        if (string.IsNullOrEmpty(_runId)) return;
        _db.DeleteRunStageState(_runId);
        _current = null;
    }

    /// <summary>
    /// 현재 스테이지 정보를 저장하고 스냅샷을 갱신합니다.
    /// </summary>
    public void SetStage(RunStageType stage, string sceneHint = null, string payloadJson = null)
    {
        if (string.IsNullOrEmpty(_runId)) return;

        var row = new RunStageState
        {
            RunId = _runId,
            Stage = stage,
            SceneHint = sceneHint ?? string.Empty,
            PayloadJson = payloadJson ?? string.Empty,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o")
        };

        _db.UpsertRunStageState(row);
        _current = row;

        if (stage != RunStageType.Battle)
        {
            _db.DeleteActiveBattleState(_runId);
        }
    }

    /// <summary>
    /// 재개 시 사용할 씬 이름과 페이로드 정보를 반환합니다.
    /// </summary>
    public RunStageResumeDecision GetResumeDecision()
    {
        if (_current == null)
        {
            return new RunStageResumeDecision
            {
                Stage = RunStageType.Unknown,
                SceneName = null,
                PayloadJson = null
            };
        }

        return new RunStageResumeDecision
        {
            Stage = _current.Stage,
            SceneName = string.IsNullOrEmpty(_current.SceneHint) ? null : _current.SceneHint,
            PayloadJson = string.IsNullOrEmpty(_current.PayloadJson) ? null : _current.PayloadJson
        };
    }

    /// <summary>
    /// 현재 저장된 페이로드를 지정한 타입으로 역직렬화합니다.
    /// </summary>
    public bool TryGetPayload<T>(out T payload) where T : class
    {
        payload = null;
        if (_current == null || string.IsNullOrEmpty(_current.PayloadJson)) return false;

        try
        {
            payload = JsonUtility.FromJson<T>(_current.PayloadJson);
            return payload != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} Payload parse failed for stage {_current.Stage}: {e.Message}");
            payload = null;
            return false;
        }
    }

    /// <summary>
    /// 페이로드 객체를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public static string ToJson<T>(T payload) where T : class
    {
        return payload == null ? string.Empty : JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열을 지정한 타입으로 역직렬화합니다.
    /// </summary>
    public static bool TryParse<T>(string json, out T payload) where T : class
    {
        payload = null;
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            payload = JsonUtility.FromJson<T>(json);
            return payload != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} TryParse failed: {e.Message}");
            payload = null;
            return false;
        }
    }
}

public static class RunStagePayloads
{
    [Serializable]
    public class Location
    {
        public int act;
        public int floor;
        public int nodeIndex;
    }

    [Serializable]
    public class Event : Location
    {
        public string eventId;
    }

    [Serializable]
    public class Shop : Location
    {
    }

    [Serializable]
    public class Battle : Location
    {
        public int battleKind;
        public string sceneName;
        public string enemyId;
    }

    [Serializable]
    public class Reward : Location
    {
    }
}

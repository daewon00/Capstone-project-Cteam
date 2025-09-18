using System;
using Game.Save;
using UnityEngine;

public sealed class RunStageService : IRunStageService
{
    private readonly IDatabase _db;
    private string _runId = string.Empty;
    private RunStageState _current;

    private const string LogTag = "[RunStageService]";

    public RunStageService(IDatabase database)
    {
        _db = database;
    }

    public RunStageState Current => _current;
    public string RunId => _runId;
    public string CurrentSceneHint => _current?.SceneHint ?? string.Empty;
    public string CurrentPayloadJson => _current?.PayloadJson ?? string.Empty;

    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;
        _current = string.IsNullOrEmpty(_runId) ? null : _db.LoadRunStageState(_runId);
    }

    public void ClearStage()
    {
        if (string.IsNullOrEmpty(_runId)) return;
        _db.DeleteRunStageState(_runId);
        _current = null;
    }

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

    public static string ToJson<T>(T payload) where T : class
    {
        return payload == null ? string.Empty : JsonUtility.ToJson(payload);
    }

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

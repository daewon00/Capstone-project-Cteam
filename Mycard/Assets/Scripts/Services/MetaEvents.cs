using System;

// Lightweight global event hub for broadcasting gameplay events to interested services
public static class MetaEvents
{
    // --- Payloads ---
    public struct CombatVictoryPayload
    {
        public string RunId;
        public int Act;
        public int Floor;
        public int NodeIndex;
    }

    public struct RunEndedPayload
    {
        public string RunId;
        public string ProfileId;
        public bool Cleared;
        public int DurationSeconds;
    }

    public struct FloorReachedPayload
    {
        public string RunId;
        public int Act;
        public int Floor;
    }

    public struct GoldChangedPayload
    {
        public string RunId;
        public int Delta;
        public int After;
    }

    // --- Events ---
    public static event Action<CombatVictoryPayload> OnCombatVictory;
    public static event Action<RunEndedPayload> OnRunEnded;
    public static event Action<FloorReachedPayload> OnFloorReached;
    public static event Action<GoldChangedPayload> OnGoldChanged;

    // --- Raisers ---
    public static void RaiseCombatVictory(CombatVictoryPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] CombatVictory: run={payload.RunId} @ {payload.Act}-{payload.Floor}:{payload.NodeIndex}");
#endif
        OnCombatVictory?.Invoke(payload);
    }

    public static void RaiseRunEnded(RunEndedPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] RunEnded: run={payload.RunId}, cleared={payload.Cleared}");
#endif
        OnRunEnded?.Invoke(payload);
    }

    public static void RaiseFloorReached(FloorReachedPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] FloorReached: run={payload.RunId} @ {payload.Act}-{payload.Floor}");
#endif
        OnFloorReached?.Invoke(payload);
    }

    public static void RaiseGoldChanged(GoldChangedPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] GoldChanged: run={payload.RunId}, delta={payload.Delta}, after={payload.After}");
#endif
        OnGoldChanged?.Invoke(payload);
    }
}

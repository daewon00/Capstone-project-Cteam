using System;

/// <summary>
/// 게임 진행 중 발생하는 주요 이벤트를 관심 있는 서비스에 전달하는 가벼운 전역 허브입니다.
/// </summary>
public static class MetaEvents
{
    // --- 페이로드 정의 ---
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

    public struct EnemyCardDestroyedPayload
    {
        public string RunId;
        public string CardId;
        public string InstanceId;
    }

    public struct AchievementUnlockedPayload
    {
        public string ProfileId;
        public string AchievementId;
        public string DisplayName;
        public string Description;
        public int Points;
        public string UnlockedAtUtc;
        public string RunId; // 선택적으로 사용되는 RunId
        public int TierIndex;
        public int TierCount;
        public bool IsFinalTier;
        public string TierDisplayName;
    }

    // --- 이벤트 ---
    public static event Action<CombatVictoryPayload> OnCombatVictory;
    public static event Action<RunEndedPayload> OnRunEnded;
    public static event Action<FloorReachedPayload> OnFloorReached;
    public static event Action<GoldChangedPayload> OnGoldChanged;
    public static event Action<EnemyCardDestroyedPayload> OnEnemyCardDestroyed;
    public static event Action<AchievementUnlockedPayload> OnAchievementUnlocked;

    // --- 발생 함수 ---
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

    public static void RaiseEnemyCardDestroyed(EnemyCardDestroyedPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] EnemyCardDestroyed: run={payload.RunId}, card={payload.CardId}, inst={payload.InstanceId}");
#endif
        OnEnemyCardDestroyed?.Invoke(payload);
    }

    public static void RaiseAchievementUnlocked(AchievementUnlockedPayload payload)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[MetaEvents] AchievementUnlocked: id={payload.AchievementId}, tier={payload.TierIndex}/{payload.TierCount}, final={payload.IsFinalTier}, +{payload.Points}pt");
#endif
        OnAchievementUnlocked?.Invoke(payload);
    }
}

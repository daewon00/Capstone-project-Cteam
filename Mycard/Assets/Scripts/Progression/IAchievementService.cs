using System.Collections.Generic;

/// <summary>
/// 업적 진행도 보고와 해금 로직을 제공하는 서비스 계약입니다.
/// </summary>
public interface IAchievementService
{
    void RebindProfile(string profileId);
    void ReportProgress(string achievementId, int delta);
    void UnlockIfEligible(string achievementId);
    void UnlockDirect(string achievementId, int pointsAward);
    void Flush();
    IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush(bool consume = false);
    IReadOnlyList<AchievementTierUnlock> GetNewlyUnlockedTiers(bool consume = false);

    // UI 조회용 API
    IReadOnlyList<AchievementDefinition> GetAllDefinitions();
    IReadOnlyDictionary<string, Game.Save.AchievementProgress> GetProgressSnapshot(string profileId);
    IReadOnlyDictionary<string, AchievementTierProgressInfo> GetTierInfoSnapshot(string profileId);
}

/// <summary>
/// 새로 달성한 업적 티어 정보를 UI/토스트에 전달하기 위한 경량 DTO입니다.
/// </summary>
public readonly struct AchievementTierUnlock
{
    public AchievementTierUnlock(string achievementId, int tierIndex, int tierCount, string displayName)
    {
        AchievementId = achievementId;
        TierIndex = tierIndex;
        TierCount = tierCount;
        DisplayName = displayName;
    }

    public string AchievementId { get; }
    public int TierIndex { get; }
    public int TierCount { get; }
    public string DisplayName { get; }
}

/// <summary>
/// 업적의 티어 진행과 누적/다음 보상을 함께 노출하기 위한 DTO입니다.
/// </summary>
public readonly struct AchievementTierProgressInfo
{
    public AchievementTierProgressInfo(
        Game.Save.AchievementProgress progress,
        int totalTiers,
        int currentTierIndex,
        int previousTierGoal,
        int currentTierGoal,
        int nextTierGoal,
        int currentTierTarget,
        int progressWithinCurrentTier,
        int remainingToNextTier,
        int cumulativeRewardPoints,
        int nextRewardPoints,
        bool hasNextTier,
        bool isFinalTierCompleted,
        string currentTierDisplayName,
        string nextTierDisplayName)
    {
        Progress = progress;
        TotalTiers = totalTiers;
        CurrentTierIndex = currentTierIndex;
        PreviousTierGoal = previousTierGoal;
        CurrentTierGoal = currentTierGoal;
        NextTierGoal = nextTierGoal;
        CurrentTierTarget = currentTierTarget;
        ProgressWithinCurrentTier = progressWithinCurrentTier;
        RemainingToNextTier = remainingToNextTier;
        CumulativeRewardPoints = cumulativeRewardPoints;
        NextRewardPoints = nextRewardPoints;
        HasNextTier = hasNextTier;
        IsFinalTierCompleted = isFinalTierCompleted;
        CurrentTierDisplayName = currentTierDisplayName;
        NextTierDisplayName = nextTierDisplayName;
    }

    public Game.Save.AchievementProgress Progress { get; }
    public int TotalTiers { get; }
    public int CurrentTierIndex { get; }
    public int PreviousTierGoal { get; }
    public int CurrentTierGoal { get; }
    public int NextTierGoal { get; }
    public int CurrentTierTarget { get; }
    public int ProgressWithinCurrentTier { get; }
    public int RemainingToNextTier { get; }
    public int CumulativeRewardPoints { get; }
    public int NextRewardPoints { get; }
    public bool HasNextTier { get; }
    public bool IsFinalTierCompleted { get; }
    public string CurrentTierDisplayName { get; }
    public string NextTierDisplayName { get; }
}

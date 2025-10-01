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
    System.Collections.Generic.IReadOnlyList<AchievementDefinition> GetAllDefinitions();
    System.Collections.Generic.IReadOnlyDictionary<string, Game.Save.AchievementProgress> GetProgressSnapshot(string profileId);
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

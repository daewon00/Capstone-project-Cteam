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
    IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush();

    // UI 조회용 API
    System.Collections.Generic.IReadOnlyList<AchievementDefinition> GetAllDefinitions();
    System.Collections.Generic.IReadOnlyDictionary<string, Game.Save.AchievementProgress> GetProgressSnapshot(string profileId);
}

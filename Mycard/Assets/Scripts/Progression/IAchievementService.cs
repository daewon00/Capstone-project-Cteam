using System.Collections.Generic;

public interface IAchievementService
{
    void RebindProfile(string profileId);
    void ReportProgress(string achievementId, int delta);
    void UnlockIfEligible(string achievementId);
    void UnlockDirect(string achievementId, int pointsAward);
    void Flush();
    IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush();

    // UI read APIs
    System.Collections.Generic.IReadOnlyList<AchievementDefinition> GetAllDefinitions();
    System.Collections.Generic.IReadOnlyDictionary<string, Game.Save.AchievementProgress> GetProgressSnapshot(string profileId);
}

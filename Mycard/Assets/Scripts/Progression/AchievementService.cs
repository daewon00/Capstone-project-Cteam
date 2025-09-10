using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

public sealed class AchievementService : IAchievementService
{
    private readonly IDatabase _db;
    private readonly Dictionary<string, AchievementDefinition> _defs;
    private readonly Dictionary<string, int> _pending = new();
    private readonly List<string> _newlyUnlocked = new();
    private string _profileId = "P1";

    public AchievementService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions();
    }

    public void RebindProfile(string profileId)
    {
        _profileId = string.IsNullOrEmpty(profileId) ? "P1" : profileId;
    }

    public void ReportProgress(string achievementId, int delta)
    {
        if (string.IsNullOrEmpty(achievementId) || delta == 0) return;
        _pending.TryGetValue(achievementId, out int cur);
        _pending[achievementId] = cur + delta;
    }

    public void UnlockIfEligible(string achievementId)
    {
        if (!_defs.TryGetValue(achievementId, out var def)) return;
        var row = _db.LoadAchievementProgress(_profileId, achievementId);
        if (row != null && row.IsUnlocked) return;

        int current = row?.Progress ?? 0;
        _pending.TryGetValue(achievementId, out int delta);
        int total = current + delta;
        if (total >= Mathf.Max(1, def.ProgressTarget))
        {
            UnlockInternal(achievementId, def.PointsReward);
            _pending[achievementId] = 0; // consumed
        }
    }

    public void UnlockDirect(string achievementId, int pointsAward)
    {
        UnlockInternal(achievementId, pointsAward);
    }

    private void UnlockInternal(string achievementId, int points)
    {
        var row = _db.LoadAchievementProgress(_profileId, achievementId) ?? new AchievementProgress
        {
            ProfileId = _profileId,
            AchievementId = achievementId,
        };
        if (row.IsUnlocked) return; // idempotent
        row.IsUnlocked = true;
        row.UnlockedAtUtc = DateTime.UtcNow.ToString("o");
        _db.UpsertAchievementProgress(row);
        _db.AddPerkPoints(_profileId, points);
        _newlyUnlocked.Add(achievementId);
    }

    public void Flush()
    {
        // Batch write pending progress updates
        foreach (var kv in _pending)
        {
            var id = kv.Key; var delta = kv.Value;
            if (!_defs.TryGetValue(id, out var def)) continue;
            var row = _db.LoadAchievementProgress(_profileId, id) ?? new AchievementProgress
            {
                ProfileId = _profileId,
                AchievementId = id,
                IsUnlocked = false,
                Progress = 0
            };
            row.Progress = Mathf.Max(0, row.Progress + delta);
            _db.UpsertAchievementProgress(row);
            if (row.Progress >= Mathf.Max(1, def.ProgressTarget) && !row.IsUnlocked)
            {
                UnlockInternal(id, def.PointsReward);
            }
        }
        _pending.Clear();
    }

    public IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush()
        => _newlyUnlocked.AsReadOnly();

    public IReadOnlyList<AchievementDefinition> GetAllDefinitions()
        => new List<AchievementDefinition>(_defs.Values);

    public IReadOnlyDictionary<string, AchievementProgress> GetProgressSnapshot(string profileId)
    {
        var map = new Dictionary<string, AchievementProgress>(StringComparer.OrdinalIgnoreCase);
        var pid = string.IsNullOrEmpty(profileId) ? _profileId : profileId;
        foreach (var id in _defs.Keys)
        {
            var row = _db.LoadAchievementProgress(pid, id);
            if (row == null)
            {
                row = new AchievementProgress
                {
                    ProfileId = pid,
                    AchievementId = id,
                    IsUnlocked = false,
                    Progress = 0,
                    UnlockedAtUtc = null
                };
            }
            map[id] = row;
        }
        return map;
    }

    private static Dictionary<string, AchievementDefinition> LoadDefinitions()
    {
        var dict = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assets = Resources.LoadAll<AchievementDefinition>("Progression/Achievements");
            foreach (var a in assets)
            {
                if (a != null && !string.IsNullOrEmpty(a.Id)) dict[a.Id] = a;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AchievementService] SO load failed: {e.Message}");
        }

        if (dict.Count == 0)
        {
            var firstWin = ScriptableObject.CreateInstance<AchievementDefinition>();
            firstWin.Id = "ACH_FIRST_WIN";
            firstWin.DisplayName = "첫 승리!";
            firstWin.Description = "전투에서 한 번 승리";
            firstWin.PointsReward = 1;
            firstWin.ProgressTarget = 1;
            dict[firstWin.Id] = firstWin;
        }
        return dict;
    }
}

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

    // In-memory counters for single-run achievements (e.g., floors traversed per run)
    private readonly Dictionary<string, int> _singleRunFloorCount = new(System.StringComparer.OrdinalIgnoreCase);

    public AchievementService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions();

        // Subscribe to meta events where the service owns the logic
        try
        {
            MetaEvents.OnFloorReached += HandleFloorReached;
            MetaEvents.OnRunEnded += HandleRunEnded;
        }
        catch { }
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

    // --- Meta event handlers (service-owned logic) ---
    private void HandleFloorReached(MetaEvents.FloorReachedPayload payload)
    {
        // Defensive guards
        if (payload.RunId == null) return;

        // Seed per-run counter from DB if missing (survives reload)
        if (!_singleRunFloorCount.ContainsKey(payload.RunId))
        {
            try
            {
                int seed = 0;
                var lr = _db.LoadCurrentRun(payload.RunId);
                if (lr != null && lr.Nodes != null)
                {
                    // Count distinct visited floors; transitions ≈ visitedFloors - 1
                    var distinctFloors = new System.Collections.Generic.HashSet<int>();
                    foreach (var n in lr.Nodes)
                    {
                        if (n != null && n.Visited) distinctFloors.Add(n.Floor);
                    }
                    seed = Mathf.Max(0, distinctFloors.Count - 1);
                }
                _singleRunFloorCount[payload.RunId] = seed;
            }
            catch { _singleRunFloorCount[payload.RunId] = 0; }
        }

        // 1) Total floors traversed: count +1 per FloorReached event, unlock tiered
        try
        {
            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T1", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T1");

            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T2", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T2");

            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T3", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T3");
        }
        catch { }

        // 2) Single-run floors traversed: in-memory counter per run
        try
        {
            _singleRunFloorCount.TryGetValue(payload.RunId, out int cur);
            cur += 1; // +1 per floor reached
            _singleRunFloorCount[payload.RunId] = cur;

            // Unlock when reaching thresholds. Use definitions if present.
            if (_defs.TryGetValue("ACH_TRAVERSE_FLOORS_SINGLE_RUN_T1", out var def1))
            {
                if (cur >= Math.Max(1, def1.ProgressTarget))
                {
                    UnlockDirect(def1.Id, def1.PointsReward);
                }
            }
            if (_defs.TryGetValue("ACH_TRAVERSE_FLOORS_SINGLE_RUN_T2", out var def2))
            {
                if (cur >= Math.Max(1, def2.ProgressTarget))
                {
                    UnlockDirect(def2.Id, def2.PointsReward);
                }
            }
        }
        catch { }
    }

    private void HandleRunEnded(MetaEvents.RunEndedPayload payload)
    {
        if (payload.RunId == null) return;
        _singleRunFloorCount.Remove(payload.RunId);
    }
}

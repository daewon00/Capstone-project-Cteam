using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Save;

/// <summary>
/// 업적 정의를 로드하고 진행도를 집계하며 특전 포인트 지급을 담당하는 서비스입니다.
/// 티어 기반 업적을 지원하도록 확장되었습니다.
/// </summary>
public sealed class AchievementService : IAchievementService
{
    private readonly IDatabase _db;
    private readonly Dictionary<string, AchievementDefinition> _defs;
    private readonly Dictionary<string, List<string>> _legacyTierIdMap;
    private readonly Dictionary<string, AchievementProgress> _progressCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingFinalSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pendingFinalList = new();
    private readonly Dictionary<string, AchievementTierUnlock> _pendingTierHighlights = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _singleRunFloorCount = new(StringComparer.OrdinalIgnoreCase);
    private string _profileId = "P1";

    public AchievementService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions(out _legacyTierIdMap);

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
        _progressCache.Clear();
        _singleRunFloorCount.Clear();

        try
        {
            MigrateLegacyProgress();
        }
        catch (Exception e)
        {
            GameLog.Warn($"[AchievementService] Legacy migration failed: {e.Message}");
        }
    }

    public void ReportProgress(string achievementId, int delta)
    {
        if (string.IsNullOrEmpty(achievementId) || delta == 0) return;
        if (!_defs.TryGetValue(achievementId, out var def)) return;

        var row = GetOrCreateProgress(achievementId);
        if (row.IsUnlocked) return;

        row.Progress = Mathf.Max(0, row.Progress + delta);
        ProcessProgress(def, row);
        _db.UpsertAchievementProgress(row);
    }

    public void UnlockIfEligible(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (!_defs.TryGetValue(achievementId, out var def)) return;

        var row = GetOrCreateProgress(achievementId);
        if (row.IsUnlocked) return;

        ProcessProgress(def, row);
        if (row.IsUnlocked)
        {
            _db.UpsertAchievementProgress(row);
        }
    }

    public void UnlockDirect(string achievementId, int pointsAward)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (!_defs.TryGetValue(achievementId, out var def)) return;

        var row = GetOrCreateProgress(achievementId);
        int tierCount = GetTierCount(def);
        row.Progress = Mathf.Max(row.Progress, def.GetFinalGoal());
        row.HighestTierUnlocked = tierCount;
        CompleteFinalTier(def, row, pointsAward > 0 ? pointsAward : def.PointsReward, tierCount, broadcastTier: true);
        _db.UpsertAchievementProgress(row);
    }

    public void Flush()
    {
        // ReportProgress 단계에서 즉시 반영되도록 변경되었으므로 여기서는 대기열 처리 필요 없음.
        // 기존 호출부와의 호환성을 위해 메서드는 남겨둡니다.
    }

    public IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush(bool consume = false)
    {
        var snapshot = _pendingFinalList.ToList();
        if (consume)
        {
            _pendingFinalList.Clear();
            _pendingFinalSet.Clear();
        }
        return snapshot;
    }

    public IReadOnlyList<AchievementTierUnlock> GetNewlyUnlockedTiers(bool consume = false)
    {
        var snapshot = _pendingTierHighlights.Values.ToArray();
        if (consume)
        {
            _pendingTierHighlights.Clear();
        }
        return snapshot;
    }

    public IReadOnlyList<AchievementDefinition> GetAllDefinitions()
        => new List<AchievementDefinition>(_defs.Values);

    public IReadOnlyDictionary<string, AchievementProgress> GetProgressSnapshot(string profileId)
    {
        var map = new Dictionary<string, AchievementProgress>(StringComparer.OrdinalIgnoreCase);
        var pid = string.IsNullOrEmpty(profileId) ? _profileId : profileId;
        foreach (var id in _defs.Keys)
        {
            var row = _db.LoadAchievementProgress(pid, id) ?? new AchievementProgress
            {
                ProfileId = pid,
                AchievementId = id,
                IsUnlocked = false,
                Progress = 0,
                UnlockedAtUtc = null,
                HighestTierUnlocked = 0
            };
            map[id] = row;
        }
        return map;
    }

    public IReadOnlyDictionary<string, AchievementTierProgressInfo> GetTierInfoSnapshot(string profileId)
    {
        var map = new Dictionary<string, AchievementTierProgressInfo>(StringComparer.OrdinalIgnoreCase);
        var pid = string.IsNullOrEmpty(profileId) ? _profileId : profileId;
        foreach (var def in _defs.Values)
        {
            var row = _db.LoadAchievementProgress(pid, def.Id) ?? new AchievementProgress
            {
                ProfileId = pid,
                AchievementId = def.Id,
                IsUnlocked = false,
                Progress = 0,
                UnlockedAtUtc = null,
                HighestTierUnlocked = 0
            };
            map[def.Id] = BuildTierInfo(def, row);
        }
        return map;
    }

    private AchievementProgress GetOrCreateProgress(string achievementId)
    {
        if (_progressCache.TryGetValue(achievementId, out var cached) && cached != null)
        {
            return cached;
        }

        var row = _db.LoadAchievementProgress(_profileId, achievementId) ?? new AchievementProgress
        {
            ProfileId = _profileId,
            AchievementId = achievementId,
            IsUnlocked = false,
            Progress = 0,
            HighestTierUnlocked = 0,
            UnlockedAtUtc = null
        };
        _progressCache[achievementId] = row;
        return row;
    }

    private void ProcessProgress(AchievementDefinition def, AchievementProgress row)
    {
        int finalGoal = def.GetFinalGoal();
        var tiers = NormalizeTiers(def);

        if (tiers.Count == 0)
        {
            if (!row.IsUnlocked && row.Progress >= finalGoal)
            {
                CompleteFinalTier(def, row, def.PointsReward, 1, broadcastTier: true);
            }
            return;
        }

        int previousHighest = Mathf.Max(0, row.HighestTierUnlocked);
        int newHighest = previousHighest;
        for (int i = previousHighest; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            int tierGoal = Mathf.Max(1, tier.goal);
            if (row.Progress < tierGoal) break;

            newHighest = i + 1;
            ApplyTierReward(def, tier);
            BroadcastTierUnlocked(def, row, tier, newHighest, tiers.Count, isFinalTier: (i == tiers.Count - 1));
        }

        if (newHighest > previousHighest)
        {
            row.HighestTierUnlocked = newHighest;
        }

        if (!row.IsUnlocked && row.HighestTierUnlocked >= tiers.Count && row.Progress >= finalGoal)
        {
            CompleteFinalTier(def, row, def.PointsReward, tiers.Count, broadcastTier: false);
        }
    }

    private void CompleteFinalTier(AchievementDefinition def, AchievementProgress row, int pointsAward, int tierCount, bool broadcastTier)
    {
        if (row.IsUnlocked) return;

        row.IsUnlocked = true;
        row.UnlockedAtUtc = DateTime.UtcNow.ToString("o");
        row.HighestTierUnlocked = Mathf.Max(row.HighestTierUnlocked, tierCount);

        int totalPoints = pointsAward > 0 ? pointsAward : def.PointsReward;
        if (totalPoints > 0)
        {
            _db.AddPerkPoints(_profileId, totalPoints);
        }

        if (_pendingFinalSet.Add(def.Id))
        {
            _pendingFinalList.Add(def.Id);
        }

        var payload = BuildPayload(def, row, tierCount, tierCount, true, totalPoints);
        MetaEvents.RaiseAchievementUnlocked(payload);

        if (broadcastTier)
        {
            _pendingTierHighlights[def.Id] = new AchievementTierUnlock(def.Id, tierCount, tierCount, def.DisplayName);
        }
    }

    private void ApplyTierReward(AchievementDefinition def, AchievementDefinition.Tier tier)
    {
        if (tier?.reward == null) return;
        if (tier.reward.perkPoints > 0)
        {
            _db.AddPerkPoints(_profileId, tier.reward.perkPoints);
        }

        if (!string.IsNullOrEmpty(tier.reward.rewardType))
        {
            GameLog.Info($"[AchievementService] Tier reward hook pending: id={def.Id}, type={tier.reward.rewardType}, payload={tier.reward.rewardPayload}");
        }
    }

    private void BroadcastTierUnlocked(AchievementDefinition def, AchievementProgress row, AchievementDefinition.Tier tier, int tierIndex, int tierCount, bool isFinalTier)
    {
        _pendingTierHighlights[def.Id] = new AchievementTierUnlock(def.Id, tierIndex, tierCount, def.DisplayName);

        if (!tier.announce && !isFinalTier)
        {
            return;
        }

        var payload = BuildPayload(def, row, tierIndex, tierCount, isFinalTier, tier.reward?.perkPoints ?? 0, tier.displayName);
        MetaEvents.RaiseAchievementUnlocked(payload);
    }

    private static List<AchievementDefinition.Tier> NormalizeTiers(AchievementDefinition def)
    {
        if (def.Tiers == null) return new List<AchievementDefinition.Tier>();
        var ordered = def.Tiers
            .Where(t => t != null)
            .OrderBy(t => Mathf.Max(1, t.goal))
            .ToList();
        return ordered;
    }

    private static int GetTierCount(AchievementDefinition def)
    {
        var tiers = def.Tiers?.Count ?? 0;
        return Mathf.Max(1, tiers == 0 ? 1 : tiers);
    }

    private MetaEvents.AchievementUnlockedPayload BuildPayload(AchievementDefinition def, AchievementProgress row, int tierIndex, int tierCount, bool isFinalTier, int points, string tierDisplayName = null)
    {
        return new MetaEvents.AchievementUnlockedPayload
        {
            ProfileId = _profileId,
            AchievementId = def.Id,
            DisplayName = def.DisplayName,
            Description = def.Description,
            Points = points,
            UnlockedAtUtc = row.UnlockedAtUtc,
            RunId = GameContext.I != null ? GameContext.I.RunId : string.Empty,
            TierIndex = tierIndex,
            TierCount = tierCount,
            IsFinalTier = isFinalTier,
            TierDisplayName = string.IsNullOrEmpty(tierDisplayName) ? def.DisplayName : tierDisplayName
        };
    }

    private static Dictionary<string, AchievementDefinition> LoadDefinitions(out Dictionary<string, List<string>> legacyTierMap)
    {
        legacyTierMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var grouped = new Dictionary<string, List<(AchievementDefinition def, int tierIndex)>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assets = Resources.LoadAll<AchievementDefinition>("Progression/Achievements");
            foreach (var asset in assets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.Id)) continue;

                // 이미 티어 데이터가 구성된 자산은 그대로 사용
                if (asset.Tiers != null && asset.Tiers.Count > 0)
                {
                    grouped[asset.Id] = new List<(AchievementDefinition, int)> { (asset, 0) };
                    continue;
                }

                if (TryParseTierId(asset.Id, out var baseId, out int tierIndex))
                {
                    if (!grouped.TryGetValue(baseId, out var list))
                    {
                        list = new List<(AchievementDefinition, int)>();
                        grouped[baseId] = list;
                    }
                    list.Add((asset, tierIndex));
                }
                else
                {
                    grouped[asset.Id] = new List<(AchievementDefinition, int)> { (asset, 0) };
                }
            }
        }
        catch (Exception e)
        {
            GameLog.Warn($"[AchievementService] SO load failed: {e.Message}");
        }

        var dict = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in grouped)
        {
            var list = kv.Value;
            if (list == null || list.Count == 0) continue;

            if (list.Count == 1 && list[0].tierIndex == 0)
            {
                var asset = list[0].def;
                if (asset != null && !string.IsNullOrEmpty(asset.Id))
                {
                    dict[asset.Id] = asset;
                }
                continue;
            }

            list.Sort((a, b) => a.tierIndex.CompareTo(b.tierIndex));
            var aggregated = ScriptableObject.CreateInstance<AchievementDefinition>();
            aggregated.Id = kv.Key;
            aggregated.DisplayName = list[0].def.DisplayName;
            aggregated.Description = list[list.Count - 1].def.Description;
            aggregated.Hidden = list.Any(x => x.def.Hidden);
            aggregated.Tiers = new List<AchievementDefinition.Tier>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var tier = new AchievementDefinition.Tier
                {
                    goal = Mathf.Max(1, entry.def.ProgressTarget),
                    reward = new AchievementDefinition.TierReward
                    {
                        perkPoints = entry.def.PointsReward,
                        rewardType = string.Empty,
                        rewardPayload = string.Empty
                    },
                    displayName = entry.def.DisplayName,
                    announce = true
                };

                aggregated.Tiers.Add(tier);
            }

            // 최종 티어는 별도 특전 포인트로 지급하므로 Reward → 0 처리하고 ProgressTarget/PointsReward 갱신
            if (aggregated.Tiers.Count > 0)
            {
                var lastTier = aggregated.Tiers[aggregated.Tiers.Count - 1];
                aggregated.PointsReward = Mathf.Max(0, lastTier.reward.perkPoints);
                aggregated.ProgressTarget = Mathf.Max(1, lastTier.goal);
                lastTier.reward.perkPoints = 0; // 최종 티어 보상은 서비스의 PointsReward로 지급
                aggregated.Tiers[aggregated.Tiers.Count - 1] = lastTier;
            }

            dict[aggregated.Id] = aggregated;
            legacyTierMap[aggregated.Id] = list.Select(l => l.def.Id).ToList();
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
            legacyTierMap[firstWin.Id] = new List<string>();
        }

        return dict;
    }

    private static bool TryParseTierId(string id, out string baseId, out int tierIndex)
    {
        baseId = id;
        tierIndex = 0;
        if (string.IsNullOrEmpty(id)) return false;

        var idx = id.LastIndexOf("_T", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0 || idx >= id.Length - 2) return false;

        var suffix = id.Substring(idx + 2);
        if (int.TryParse(suffix, out var parsed) && parsed > 0)
        {
            baseId = id.Substring(0, idx);
            tierIndex = parsed;
            return true;
        }
        return false;
    }

    private void MigrateLegacyProgress()
    {
        if (_legacyTierIdMap == null || _legacyTierIdMap.Count == 0) return;

        foreach (var kvp in _legacyTierIdMap)
        {
            if (!_defs.TryGetValue(kvp.Key, out var def)) continue;

            var row = _db.LoadAchievementProgress(_profileId, kvp.Key) ?? new AchievementProgress
            {
                ProfileId = _profileId,
                AchievementId = kvp.Key,
                IsUnlocked = false,
                Progress = 0,
                HighestTierUnlocked = 0,
                UnlockedAtUtc = null
            };

            int tierCount = Mathf.Max(1, def.Tiers?.Count ?? 1);
            int highestTier = row.HighestTierUnlocked;
            int progress = row.Progress;
            bool unlocked = row.IsUnlocked;
            string unlockedAt = row.UnlockedAtUtc;

            var legacyIds = kvp.Value;
            for (int i = 0; i < legacyIds.Count; i++)
            {
                var legacyId = legacyIds[i];
                var legacyRow = _db.LoadAchievementProgress(_profileId, legacyId);
                if (legacyRow == null) continue;

                progress = Mathf.Max(progress, legacyRow.Progress);
                if (legacyRow.IsUnlocked && (i + 1) > highestTier)
                {
                    highestTier = i + 1;
                    if (!string.IsNullOrEmpty(legacyRow.UnlockedAtUtc))
                    {
                        unlockedAt = legacyRow.UnlockedAtUtc;
                    }
                }
            }

            bool changed = false;
            if (progress > row.Progress)
            {
                row.Progress = progress;
                changed = true;
            }
            if (highestTier > row.HighestTierUnlocked)
            {
                row.HighestTierUnlocked = highestTier;
                changed = true;
            }

            bool shouldBeUnlocked = highestTier >= tierCount;
            if (shouldBeUnlocked && !row.IsUnlocked)
            {
                row.IsUnlocked = true;
                row.UnlockedAtUtc = string.IsNullOrEmpty(unlockedAt) ? row.UnlockedAtUtc : unlockedAt;
                changed = true;
            }

            if (changed)
            {
                _db.UpsertAchievementProgress(row);
            }
        }
    }


    private AchievementTierProgressInfo BuildTierInfo(AchievementDefinition def, AchievementProgress row)
    {
        if (row == null)
        {
            row = new AchievementProgress
            {
                ProfileId = _profileId,
                AchievementId = def.Id,
                IsUnlocked = false,
                Progress = 0,
                UnlockedAtUtc = null,
                HighestTierUnlocked = 0
            };
        }

        var tiers = NormalizeTiers(def);
        int totalTiers = tiers.Count > 0 ? tiers.Count : 1;
        int finalGoal = def.GetFinalGoal();
        int rawProgress = Mathf.Max(0, row.Progress);
        int unlockedTiers = Mathf.Clamp(row.HighestTierUnlocked, 0, totalTiers);
        bool finalCompleted = row.IsUnlocked || unlockedTiers >= totalTiers || rawProgress >= finalGoal;
        int currentTierIndex = finalCompleted ? Mathf.Max(totalTiers - 1, 0) : Mathf.Clamp(unlockedTiers, 0, totalTiers - 1);

        int previousGoal = GetTierGoal(tiers, currentTierIndex - 1, finalGoal);
        int currentGoal = GetTierGoal(tiers, currentTierIndex, finalGoal);
        int currentTarget = Mathf.Max(1, currentGoal - previousGoal);

        bool hasNextTier = !finalCompleted && unlockedTiers < totalTiers;
        int nextGoal = hasNextTier ? GetTierGoal(tiers, unlockedTiers, finalGoal) : currentGoal;
        int progressWithin = Mathf.Clamp(rawProgress - previousGoal, 0, currentTarget);
        if (finalCompleted) progressWithin = currentTarget;
        int remainingToNext = hasNextTier ? Mathf.Max(0, nextGoal - rawProgress) : 0;

        int cumulativeReward = 0;
        if (tiers.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(unlockedTiers, tiers.Count); i++)
            {
                var reward = tiers[i].reward;
                if (reward != null) cumulativeReward += Mathf.Max(0, reward.perkPoints);
            }
        }
        if (row.IsUnlocked)
        {
            cumulativeReward += Mathf.Max(0, def.PointsReward);
        }

        int nextReward = 0;
        if (hasNextTier)
        {
            if (tiers.Count > 0 && unlockedTiers < tiers.Count)
            {
                var reward = tiers[unlockedTiers].reward;
                nextReward = reward != null ? Mathf.Max(0, reward.perkPoints) : 0;
                if (unlockedTiers == tiers.Count - 1)
                {
                    nextReward = Mathf.Max(0, def.PointsReward);
                }
            }
            else
            {
                nextReward = Mathf.Max(0, def.PointsReward);
            }
        }

        string currentDisplay = def.DisplayName;
        if (tiers.Count > 0)
        {
            var currentTier = tiers[Mathf.Clamp(currentTierIndex, 0, tiers.Count - 1)];
            if (!string.IsNullOrEmpty(currentTier.displayName)) currentDisplay = currentTier.displayName;
        }

        string nextDisplay = null;
        if (hasNextTier)
        {
            if (tiers.Count > 0 && unlockedTiers < tiers.Count)
            {
                var nextTier = tiers[unlockedTiers];
                nextDisplay = !string.IsNullOrEmpty(nextTier.displayName) ? nextTier.displayName : def.DisplayName;
            }
            else
            {
                nextDisplay = def.DisplayName;
            }
        }

        return new AchievementTierProgressInfo(
            row,
            totalTiers,
            currentTierIndex,
            previousGoal,
            currentGoal,
            nextGoal,
            currentTarget,
            progressWithin,
            remainingToNext,
            cumulativeReward,
            nextReward,
            hasNextTier,
            finalCompleted,
            currentDisplay,
            nextDisplay
        );
    }

    private static int GetTierGoal(List<AchievementDefinition.Tier> tiers, int index, int finalGoal)
    {
        if (tiers == null || tiers.Count == 0)
        {
            if (index < 0) return 0;
            return Mathf.Max(1, finalGoal);
        }

        if (index < 0) return 0;
        if (index >= tiers.Count) return Mathf.Max(1, finalGoal);
        return Mathf.Max(1, tiers[index].goal);
    }

    private void HandleFloorReached(MetaEvents.FloorReachedPayload payload)
    {
        if (payload.RunId == null) return;

        if (!_singleRunFloorCount.ContainsKey(payload.RunId))
        {
            try
            {
                int seed = 0;
                var lr = _db.LoadCurrentRun(payload.RunId);
                if (lr != null && lr.Nodes != null)
                {
                    var distinctFloors = new HashSet<int>();
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

        try
        {
            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL");
        }
        catch { }

        try
        {
            _singleRunFloorCount.TryGetValue(payload.RunId, out int cur);
            cur += 1;
            _singleRunFloorCount[payload.RunId] = cur;

            if (_defs.TryGetValue("ACH_TRAVERSE_FLOORS_SINGLE_RUN", out var singleRunDef))
            {
                var row = GetOrCreateProgress("ACH_TRAVERSE_FLOORS_SINGLE_RUN");
                row.Progress = Mathf.Max(row.Progress, cur);
                ProcessProgress(singleRunDef, row);
                _db.UpsertAchievementProgress(row);
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

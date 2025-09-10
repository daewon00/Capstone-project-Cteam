using System;
using System.Collections.Generic;
using System.Linq;
using Game.Save;
using UnityEngine;

public sealed class PerkService : IPerkService
{
    private readonly IDatabase _db;
    private readonly Dictionary<string, PerkDefinition> _defs;

    public PerkService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions();
    }

    public IReadOnlyList<PerkDefinition> GetAllDefinitions() => _defs.Values.ToList();

    public IReadOnlyList<PerkAllocation> GetAllocations(string profileId)
        => _db.LoadPerkAllocations(profileId);

    public bool TryPurchase(string profileId, string perkId, int levels, out string error)
    {
        error = null;
        if (!_defs.TryGetValue(perkId, out var def)) { error = "Unknown perk"; return false; }
        if (levels <= 0) { error = "Invalid level"; return false; }

        var profile = DatabaseManager.Instance.LoadProfile(profileId);
        if (profile == null)
        {
            profile = new PlayerProfile
            {
                ProfileId = profileId,
                SchemaVersion = 1,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                AppVersion = Application.version,
                UnspentPerkPoints = 0
            };
            DatabaseManager.Instance.SaveProfile(profile);
        }

        var allocations = _db.LoadPerkAllocations(profileId).ToList();
        var existing = allocations.FirstOrDefault(a => a.PerkId == perkId);
        int currentLevel = existing?.Level ?? 0;
        int targetLevel = Mathf.Clamp(currentLevel + levels, 0, def.MaxLevel);
        int deltaLevels = targetLevel - currentLevel;
        if (deltaLevels <= 0) { error = "Already at max"; return false; }

        int cost = def.Cost * deltaLevels;
        if (profile.UnspentPerkPoints < cost) { error = "Not enough points"; return false; }

        profile.UnspentPerkPoints -= cost;
        DatabaseManager.Instance.SaveProfile(profile);

        if (existing == null)
        {
            allocations.Add(new PerkAllocation { ProfileId = profileId, PerkId = perkId, Level = targetLevel });
        }
        else
        {
            existing.Level = targetLevel;
        }
        DatabaseManager.Instance.SavePerkAllocations(profileId, allocations);
        MirrorAllocationsToPrefs(profileId, allocations); // convenient debug mirror
        return true;
    }

    public void ComputeRunSnapshotAndPersist(string profileId, string runId)
    {
        var aggregates = ComputeAggregatesForProfile(profileId);
        var rows = new List<RunPerkSnapshot>();

        // 1) 현재 보유한 레벨 기준 집계 추가
        foreach (var kv in aggregates)
        {
            rows.Add(new RunPerkSnapshot
            {
                RunId = runId,
                EffectKey = kv.Key,
                AggregatedFlatValue = kv.Value.flat,
                AggregatedPercentValue = kv.Value.percent
            });
        }

        // 2) 정의된 모든 EffectKey에 대해 기본 0행을 보장(스냅샷 키 누락 방지)
        var effectKeys = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var def in _defs.Values)
        {
            if (string.IsNullOrEmpty(def?.EffectKey)) continue;
            if (!effectKeys.Add(def.EffectKey)) continue; // distinct only
            if (aggregates.ContainsKey(def.EffectKey)) continue; // already added
            rows.Add(new RunPerkSnapshot
            {
                RunId = runId,
                EffectKey = def.EffectKey,
                AggregatedFlatValue = 0f,
                AggregatedPercentValue = 0f
            });
        }

        _db.ReplaceRunPerkSnapshot(runId, rows);
    }

    public Dictionary<string, (float flat, float percent)> ComputeAggregatesForProfile(string profileId)
    {
        var dict = new Dictionary<string, (float flat, float percent)>(StringComparer.OrdinalIgnoreCase);
        foreach (var alloc in _db.LoadPerkAllocations(profileId))
        {
            if (!_defs.TryGetValue(alloc.PerkId, out var def)) continue;
            float value = def.PerLevelValue * Mathf.Max(0, alloc.Level);
            if (!dict.TryGetValue(def.EffectKey, out var agg)) agg = (0f, 0f);
            if (def.Kind == ValueKind.Flat)
                agg.flat += value;
            else
                agg.percent += value;
            dict[def.EffectKey] = agg;
        }
        return dict;
    }

    public bool ApplyAdjustments(string profileId, System.Collections.Generic.Dictionary<string, int> targetLevels, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(profileId)) { error = "Invalid profile"; return false; }
        if (targetLevels == null) { error = "No adjustments"; return false; }

        // Load current state
        var currentAlloc = _db.LoadPerkAllocations(profileId).ToDictionary(a => a.PerkId, a => a.Level, StringComparer.OrdinalIgnoreCase);
        var profile = _db.LoadProfile(profileId);
        int unspent = profile?.UnspentPerkPoints ?? 0;

        int totalCost = 0;
        int totalRefund = 0;

        // Union of keys: adjust only provided keys, keep others
        var allKeys = new HashSet<string>(currentAlloc.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in targetLevels.Keys) allKeys.Add(k);

        var finalLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var perkId in allKeys)
        {
            int current = currentAlloc.TryGetValue(perkId, out var lv) ? Mathf.Max(0, lv) : 0;
            int target = targetLevels.TryGetValue(perkId, out var tv) ? Mathf.Max(0, tv) : current;

            if (!_defs.TryGetValue(perkId, out var def))
            {
                // Unknown perk ID in request → ignore with warning
                Debug.LogWarning($"[PerkService] Unknown perk in ApplyAdjustments: {perkId}");
                finalLevels[perkId] = current; // no change
                continue;
            }

            target = Mathf.Clamp(target, 0, Mathf.Max(0, def.MaxLevel));
            finalLevels[perkId] = target;

            if (target > current)
            {
                int inc = target - current;
                totalCost += inc * Mathf.Max(0, def.Cost);
            }
            else if (target < current)
            {
                int dec = current - target;
                totalRefund += dec * Mathf.Max(0, def.Cost);
            }
        }

        int pointsDelta = -totalCost + totalRefund;
        int finalUnspent = unspent + pointsDelta;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PerkService] ApplyAdjustments preview: keys={finalLevels.Count}, cost={totalCost}, refund={totalRefund}, delta={pointsDelta}, unspent={unspent} -> final={finalUnspent}");
#endif
        if (finalUnspent < 0)
        {
            error = "Not enough perk points";
            return false;
        }

        // Compose new allocation rows: only levels > 0
        var rows = new List<PerkAllocation>();
        foreach (var kv in finalLevels)
        {
            if (kv.Value <= 0) continue;
            rows.Add(new PerkAllocation { ProfileId = profileId, PerkId = kv.Key, Level = kv.Value });
        }

        try
        {
            _db.ApplyPerkAdjustments(profileId, rows, pointsDelta);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var after = _db.LoadPerkAllocations(profileId);
            Debug.Log($"[PerkService] ApplyAdjustments committed: rowsSaved={rows.Count}, rowsNow={after?.Count}");
#endif
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerkService] ApplyAdjustments failed: {e.Message}");
            error = e.Message;
            return false;
        }
    }

    private Dictionary<string, PerkDefinition> LoadDefinitions()
    {
        var result = new Dictionary<string, PerkDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assets = Resources.LoadAll<PerkDefinition>("Progression/Perks");
            foreach (var a in assets)
            {
                if (a != null && !string.IsNullOrEmpty(a.Id)) result[a.Id] = a;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PerkService] Failed to load SOs: {e.Message}");
        }

        // Fallback default definitions for dev/testing if none provided
        if (result.Count == 0)
        {
            var flat = ScriptableObject.CreateInstance<PerkDefinition>();
            flat.Id = "PERK_STARTING_GOLD_FLAT";
            flat.DisplayName = "부유한 시작 (+50)";
            flat.Description = "+50 시작 골드";
            flat.Cost = 1; flat.MaxLevel = 5;
            flat.EffectKey = "STARTING_GOLD";
            flat.StackingMode = StackingMode.Additive; flat.Kind = ValueKind.Flat; flat.PerLevelValue = 50f;
            result[flat.Id] = flat;

            var pct = ScriptableObject.CreateInstance<PerkDefinition>();
            pct.Id = "PERK_STARTING_GOLD_PERCENT";
            pct.DisplayName = "시드 머니 (+10%)";
            pct.Description = "+10% 시작 골드";
            pct.Cost = 2; pct.MaxLevel = 3;
            pct.EffectKey = "STARTING_GOLD";
            pct.StackingMode = StackingMode.Multiplicative; pct.Kind = ValueKind.Percentage; pct.PerLevelValue = 0.10f;
            result[pct.Id] = pct;
        }

        return result;
    }

    // Note: For editor/dev convenience we mirror the latest allocations snapshot into PlayerPrefs
    // so the debug overlay can show levels even before a read API existed.

    // Helper to persist allocations snapshot in PlayerPrefs whenever we SavePerkAllocations
    public static void MirrorAllocationsToPrefs(string profileId, IEnumerable<PerkAllocation> perks)
    {
        var wrapper = new AllocWrapper { items = perks.ToList() };
        var json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString($"perk_allocs_{profileId}", json);
        PlayerPrefs.Save();
    }

    [Serializable]
    private class AllocWrapper { public List<PerkAllocation> items; }
}

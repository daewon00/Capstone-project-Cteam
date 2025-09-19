using System;
using System.Collections.Generic;
using System.Linq;
using Game.Save;
using UnityEngine;

/// <summary>
/// 특전 정의를 로드하고 구매·집계·스냅샷 계산을 담당하는 서비스입니다.
/// </summary>
public sealed class PerkService : IPerkService
{
    private readonly IDatabase _db;
    private readonly Dictionary<string, PerkDefinition> _defs;

    /// <summary>
    /// DB 핸들을 받아 특전 서비스를 초기화합니다.
    /// </summary>
    public PerkService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions();
    }

    /// <summary>
    /// 모든 특전 정의를 반환합니다.
    /// </summary>
    public IReadOnlyList<PerkDefinition> GetAllDefinitions() => _defs.Values.ToList();

    /// <summary>
    /// 지정한 프로필의 현재 특전 배치를 조회합니다.
    /// </summary>
    public IReadOnlyList<PerkAllocation> GetAllocations(string profileId)
        => _db.LoadPerkAllocations(profileId);

    /// <summary>
    /// 특전 레벨을 구매하고 포인트를 차감합니다.
    /// </summary>
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
        MirrorAllocationsToPrefs(profileId, allocations); // 디버그용 미러링
        return true;
    }

    /// <summary>
    /// 특전 집계값을 계산하고 런 스냅샷 테이블에 저장합니다.
    /// </summary>
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
            if (!effectKeys.Add(def.EffectKey)) continue; // 이미 처리한 키는 건너뜁니다.
            if (aggregates.ContainsKey(def.EffectKey)) continue; // 집계에 존재하면 생략합니다.
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

    /// <summary>
    /// 프로필의 특전 배치로부터 평면/비율 보너스를 집계합니다.
    /// </summary>
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

    /// <summary>
    /// 목표 레벨 집합을 적용하며 포인트 변화를 계산합니다.
    /// </summary>
    public bool ApplyAdjustments(string profileId, System.Collections.Generic.Dictionary<string, int> targetLevels, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(profileId)) { error = "Invalid profile"; return false; }
        if (targetLevels == null) { error = "No adjustments"; return false; }

        // 현재 상태를 로드합니다.
        var currentAlloc = _db.LoadPerkAllocations(profileId).ToDictionary(a => a.PerkId, a => a.Level, StringComparer.OrdinalIgnoreCase);
        var profile = _db.LoadProfile(profileId);
        int unspent = profile?.UnspentPerkPoints ?? 0;

        int totalCost = 0;
        int totalRefund = 0;

        // 적용 대상 키를 합집합으로 구성합니다.
        var allKeys = new HashSet<string>(currentAlloc.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in targetLevels.Keys) allKeys.Add(k);

        var finalLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var perkId in allKeys)
        {
            int current = currentAlloc.TryGetValue(perkId, out var lv) ? Mathf.Max(0, lv) : 0;
            int target = targetLevels.TryGetValue(perkId, out var tv) ? Mathf.Max(0, tv) : current;

            if (!_defs.TryGetValue(perkId, out var def))
            {
                // 요청에 알 수 없는 특전이 포함된 경우 경고 후 무시합니다.
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

        // 레벨이 0 초과인 항목만 새 행으로 구성합니다.
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

    /// <summary>
    /// Resources에서 특전 정의를 로드하거나 기본 데이터를 생성합니다.
    /// </summary>
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

        // 에셋이 없을 경우 개발/테스트용 기본 정의를 생성합니다.
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

    // 참고: 에디터/개발 편의를 위해 최신 배치를 PlayerPrefs에도 복사합니다.

    /// <summary>
    /// 특전 배치를 PlayerPrefs에 복제해 디버그 오버레이가 즉시 참조할 수 있도록 합니다.
    /// </summary>
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

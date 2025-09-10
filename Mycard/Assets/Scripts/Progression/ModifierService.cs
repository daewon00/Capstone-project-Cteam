using System;
using System.Collections.Generic;
using Game.Save;
using UnityEngine;

public sealed class ModifierService : IModifierService
{
    private readonly IDatabase _db;
    private string _runId = string.Empty;

    // Cache for current run snapshot
    private Dictionary<string, (float flat, float percent)> _cache = new();

    public ModifierService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;
        _cache.Clear();
        if (!string.IsNullOrEmpty(_runId))
        {
            try
            {
                var rows = _db.LoadRunPerkSnapshot(_runId);
                _cache = new Dictionary<string, (float flat, float percent)>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    _cache[r.EffectKey] = (r.AggregatedFlatValue, r.AggregatedPercentValue);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ModifierService] Snapshot load failed: {e.Message}");
            }
        }
    }

    public float Apply(string key, float baseValue, ModifierScope scope)
    {
        if (string.IsNullOrEmpty(key)) return baseValue;

        (float flat, float percent) agg = (0f, 0f);
        switch (scope)
        {
            case ModifierScope.CurrentRun:
                if (!_cache.TryGetValue(key, out agg))
                {
                    // 스냅샷에 해당 키가 없으면 수정자 없음으로 간주하고 기본값 사용
                    return baseValue;
                }
                break;
            case ModifierScope.Global:
                // Not implemented: global effects aggregation placeholder
                break;
        }

        // FinalValue = Clamp( (Base + sumFlat) * Product(1 + sumPercent), Min, Max )
        float sumFlat = agg.flat;
        float sumPercent = agg.percent; // already aggregated as sum of percentages
        float result = (baseValue + sumFlat) * (1f + sumPercent);
        return result; // Clamp handled by consumers if needed
    }
}

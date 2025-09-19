using System;
using System.Collections.Generic;
using Game.Save;
using UnityEngine;

/// <summary>
/// 런 스냅샷에서 수정자 값을 로드해 지정된 키에 적용합니다.
/// </summary>
public sealed class ModifierService : IModifierService
{
    private readonly IDatabase _db;
    private string _runId = string.Empty;

    // 현재 런 스냅샷을 보관하는 캐시
    private Dictionary<string, (float flat, float percent)> _cache = new();

    /// <summary>
    /// DB 핸들을 받아 수정자 서비스를 초기화합니다.
    /// </summary>
    public ModifierService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 런 ID를 변경하고 연관된 스냅샷 캐시를 다시 로드합니다.
    /// </summary>
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

    /// <summary>
    /// 지정된 키에 해당하는 수정자를 적용해 계산된 값을 반환합니다.
    /// </summary>
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
                // 전역 수정자 집계는 추후 구현 예정입니다.
                break;
        }

        // 최종값 = (기본값 + 합산 평면 보너스) * (1 + 합산 비율 보너스)
        float sumFlat = agg.flat;
        float sumPercent = agg.percent; // 이미 퍼센트 누계 값입니다.
        float result = (baseValue + sumFlat) * (1f + sumPercent);
        return result; // 필요 시 클램프는 호출 측에서 처리합니다.
    }
}

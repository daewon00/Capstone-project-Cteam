using System;
using System.Collections.Generic;
using Game.Save;
using URandom = Unity.Mathematics.Random; // Unity.Mathematics.Random 별칭

/// <summary>
/// 도메인별 결정론적 PRNG를 제공하는 서비스 구현체입니다.
/// - 도메인 단위로 시드를 설정/복원합니다.
/// - 호출마다 내부 상태를 갱신하고, 저장 시 현재 상태를 `RngState`로 내보냅니다.
/// </summary>
public class RngService : IRngService
{
    // 각 도메인의 PRNG 인스턴스(값 타입이므로 사본 반환/할당 주의)
    private readonly Dictionary<string, URandom> _rngs = new Dictionary<string, URandom>();
    // 사용된 호출 횟수(디버그/감사용). 재현에는 현재 상태(StateA)만으로 충분하지만 보조 정보로 유지합니다.
    private readonly Dictionary<string, long> _steps = new Dictionary<string, long>();
    // 초기 시드(선택 정보). 저장 시 참고용으로 내보낼 수 있습니다.
    private readonly Dictionary<string, uint> _initialSeeds = new Dictionary<string, uint>();

    public RngService(IEnumerable<RngState> initialStates)
    {
        if (initialStates == null) return;

        foreach (var state in initialStates)
        {
            if (string.IsNullOrEmpty(state?.Domain)) continue;

            // 1) 가능하면 저장된 현재 상태(StateA)를 사용해 정확히 복원합니다.
            // 2) 없으면 초기 시드(Seed)의 하위 32비트로 초기화합니다.
            uint seed32 = state.StateA != 0UL
                ? (uint)(state.StateA & 0xFFFFFFFFUL)
                : (uint)(state.Seed & 0xFFFFFFFFUL);

            if (seed32 == 0u) seed32 = 1u; // Unity.Mathematics.Random은 0 시드를 허용하지 않습니다.

            var rng = new URandom(seed32);
            _rngs[state.Domain] = rng;
            _steps[state.Domain] = state.Step;
            _initialSeeds[state.Domain] = (uint)(state.Seed & 0xFFFFFFFFUL);
        }
    }

    public void Seed(string domain, uint seed)
    {
        if (string.IsNullOrEmpty(domain))
            throw new ArgumentException("RNG domain must be a non-empty string.", nameof(domain));

        if (seed == 0u) seed = 1u;
        _rngs[domain] = new URandom(seed);
        _steps[domain] = 0L;
        _initialSeeds[domain] = seed;
    }

    private URandom GetRngValue(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            throw new ArgumentException("RNG domain must be a non-empty string.", nameof(domain));

        if (!_rngs.TryGetValue(domain, out var rng))
        {
            // 시드되지 않은 도메인은 명확하게 실패시켜 초기화 순서를 강제합니다.
            throw new InvalidOperationException(
                $"RNG for domain '{domain}' has not been seeded. Call Seed(domain, seed) before first use.");
        }
        return rng;
    }

    public uint NextUInt(string domain)
    {
        var rng = GetRngValue(domain);
        var result = rng.NextUInt();
        _rngs[domain] = rng; // struct 변경 반영
        _steps[domain] = _steps.TryGetValue(domain, out var s) ? (s + 1) : 1;
        return result;
    }

    public int NextInt(string domain, int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive는 minInclusive보다 반드시 커야 합니다.");

        var rng = GetRngValue(domain);
        var result = rng.NextInt(minInclusive, maxExclusive);
        _rngs[domain] = rng;
        _steps[domain] = _steps.TryGetValue(domain, out var s) ? (s + 1) : 1;
        return result;
    }

    public float NextFloat(string domain)
    {
        var rng = GetRngValue(domain);
        var result = rng.NextFloat();
        _rngs[domain] = rng;
        _steps[domain] = _steps.TryGetValue(domain, out var s) ? (s + 1) : 1;
        return result;
    }

    public List<RngState> GetStatesForSave()
    {
        var states = new List<RngState>(_rngs.Count);
        foreach (var pair in _rngs)
        {
            var domain = pair.Key;
            var rng = pair.Value;
            _steps.TryGetValue(domain, out var step);
            _initialSeeds.TryGetValue(domain, out var initSeed);

            states.Add(new RngState
            {
                // 저장 측에서 RunId를 채워 넣어야 합니다.
                Domain = domain,
                // Seed: 초기 시드를 보존하며, 없으면 현재 상태로 대체합니다.
                Seed = initSeed != 0u ? initSeed : rng.state,
                // 현재 상태 스냅샷(하위 32비트가 의미 있는 값)
                StateA = rng.state,
                StateB = 0UL,
                Step = step
            });
        }
        return states;
    }
}

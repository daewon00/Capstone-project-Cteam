using UnityEngine;

/// <summary>
/// CardReadabilityProfile 전역 접근 레지스트리. Resources 에서 지연 로드합니다.
/// </summary>
public static class CardReadabilityRegistry
{
    private static CardReadabilityProfile _profile;
    private static bool _missingLogged;

    public static CardReadabilityProfile Profile
    {
        get
        {
            if (_profile == null)
            {
                _profile = LoadProfile();
                if (_profile == null)
                {
                    if (!_missingLogged)
                    {
                        GameLog.Warn("[CardReadabilityRegistry] Resources/Cards/CardReadabilityProfile.asset not found. Using code defaults.");
                        _missingLogged = true;
                    }
                    _profile = ScriptableObject.CreateInstance<CardReadabilityProfile>();
                }
            }
            return _profile;
        }
    }

    public static void SetProfile(CardReadabilityProfile profile)
    {
        _profile = profile;
        _missingLogged = false;
    }

    private static CardReadabilityProfile LoadProfile()
    {
        // 1) 권장 기본 경로 시도
        var profile = Resources.Load<CardReadabilityProfile>("Cards/CardReadabilityProfile");
        if (profile != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info("[CardReadabilityRegistry] Loaded CardReadabilityProfile from Resources/Cards.");
#endif
            return profile;
        }

        // 2) 다른 Resources 하위 경로에서 검색(예: 경로 변경/다중 리소스)
        var all = Resources.LoadAll<CardReadabilityProfile>(string.Empty);
        if (all != null && all.Length > 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[CardReadabilityRegistry] Loaded CardReadabilityProfile fallback path: {all[0].name}");
#endif
            return all[0];
        }

        return null;
    }
}

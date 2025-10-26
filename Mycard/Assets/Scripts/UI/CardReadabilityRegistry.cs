using UnityEngine;

/// <summary>
/// CardReadabilityProfile 전역 접근 레지스트리. Resources 에서 지연 로드합니다.
/// </summary>
public static class CardReadabilityRegistry
{
    private static CardReadabilityProfile _profile;

    public static CardReadabilityProfile Profile
    {
        get
        {
            if (_profile == null)
            {
                _profile = Resources.Load<CardReadabilityProfile>("Cards/CardReadabilityProfile");
                if (_profile == null)
                {
                    Debug.LogWarning("[CardReadabilityRegistry] Resources/Cards/CardReadabilityProfile.asset not found. Using code defaults.");
                }
            }
            return _profile;
        }
    }

    public static void SetProfile(CardReadabilityProfile profile)
    {
        _profile = profile;
    }
}


using UnityEngine;

/// <summary>
/// 카드 비주얼 프로파일과 희귀도 엠블렘을 제공하는 전역 접근 지점입니다.
/// </summary>
public static class CardVisualRegistry
{
    private static CardVisualProfile _profile;

    public static CardVisualProfile Profile
    {
        get
        {
            if (_profile == null)
            {
                _profile = Resources.Load<CardVisualProfile>("Cards/CardVisualProfile");
                if (_profile == null)
                {
                    GameLog.Warn("[CardVisualRegistry] Resources/Cards/CardVisualProfile.asset 을 찾을 수 없습니다. 비주얼이 기본값으로 표시됩니다.");
                }
            }
            return _profile;
        }
    }

    public static void SetProfile(CardVisualProfile profile)
    {
        _profile = profile;
    }
}

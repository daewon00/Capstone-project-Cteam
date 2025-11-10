using Game.Save;
using UnityEngine;

/// <summary>
/// 카드 강화 가능 여부를 판단하는 공통 규칙 모음입니다.
/// 맵 오버레이, 캠프파이어 등 여러 UI가 동일한 기준을 사용하도록 합니다.
/// </summary>
public static class CardUpgradeRules
{
    /// <summary>
    /// 카드가 강화 대상이 될 수 있는지 확인합니다.
    /// </summary>
    public static bool IsUpgradeable(CardRuntimeState state, ICardCatalog catalog)
        => TryGetUpgradeable(state, catalog, out _);

    /// <summary>
    /// 강화 가능 여부를 확인하고 카드 데이터를 돌려줍니다.
    /// </summary>
    public static bool TryGetUpgradeable(CardRuntimeState state, ICardCatalog catalog, out CardScriptableObject cardData)
    {
        cardData = null;
        if (state == null)
            return false;

        if (state.IsUpgraded())
            return false;

        if (catalog == null)
        {
            GameLog.Warn("[CardUpgradeRules] CardCatalog가 없어 강화 가능 여부를 판별할 수 없습니다.");
            return false;
        }

        if (!catalog.TryGetCardData(state.CardId, out cardData) || cardData == null)
            return false;

        if (!cardData.UpgradeEnabled)
            return false;

        return true;
    }
}

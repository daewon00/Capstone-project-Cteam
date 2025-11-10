using Game.Save;
using UnityEngine;

/// <summary>
/// 카드 제거 이벤트에서 사용되는 공통 필터 규칙입니다.
/// 후속 확장을 위해 별도 클래스로 분리해두었습니다.
/// </summary>
public static class CardRemovalRules
{
    /// <summary>
    /// 카드가 제거 대상이 될 수 있는지 확인합니다.
    /// </summary>
    public static bool IsRemovable(CardRuntimeState state, ICardCatalog catalog)
        => TryGetRemovable(state, catalog, out _);

    /// <summary>
    /// 제거 가능 여부를 확인하고 대응하는 카드 데이터를 반환합니다.
    /// </summary>
    public static bool TryGetRemovable(CardRuntimeState state, ICardCatalog catalog, out CardScriptableObject cardData)
    {
        cardData = null;
        if (state == null)
            return false;

        if (catalog == null)
        {
            GameLog.Warn("[CardRemovalRules] 카드 카탈로그가 없어 제거 가능 여부를 판별할 수 없습니다.");
            return false;
        }

        if (!catalog.TryGetCardData(state.CardId, out cardData) || cardData == null)
            return false;

        // 추후 특정 카드나 위치를 제한해야 하면 여기에서 조건을 추가합니다.
        return true;
    }
}

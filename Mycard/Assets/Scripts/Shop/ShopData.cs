/// <summary>
/// 상점 세션의 리롤 횟수와 슬롯 상태를 저장하는 DTO입니다.
/// </summary>
[System.Serializable]
public class ShopSessionDTO
{
    public int rerollCount;
    public SlotDTO[] slots;
}

/// <summary>
/// 상점 슬롯 하나에 대한 아이템 ID, 가격, 특가 여부를 표현합니다.
/// </summary>
[System.Serializable]
public class SlotDTO
{
    public string itemId; // 카드의 고유 ID
    public bool soldOut;

    public string detail;   // 아이템 타입 ("Card", "Relic" 등)
    public int price;       // 할인이 적용된 최종 가격
    public bool isDeal;     // 특가 상품인지 여부
    public CardRarity rarity;
}

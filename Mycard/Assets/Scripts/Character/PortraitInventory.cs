using UnityEngine;

public class PortraitInventory : MonoBehaviour
{
    public PortraitSlot[] portraitSlots;
    public static PortraitInventory instance;

    private void Awake()
    {
        instance = this;
    }

    public void AddCharacter(CharacterSO character)
    {
        foreach (var slot in portraitSlots)
        {
            // 자식 유무가 아니라 '스프라이트가 비었는지'로 체크
            if (slot != null && slot.IsEmpty)
            {
                slot.SetSlot(character);
                return;
            }
        }

        Debug.LogWarning("빈 초상화 슬롯이 없습니다.");
    }
}

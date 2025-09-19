using UnityEngine;

/// <summary>
/// 초상화 슬롯을 관리하며 캐릭터 데이터를 빈 슬롯에 배치합니다.
/// </summary>
public class PortraitInventory : MonoBehaviour
{
    public PortraitSlot[] portraitSlots;
    public static PortraitInventory instance;

    /// <summary>
    /// 싱글턴 인스턴스를 등록합니다.
    /// </summary>
    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 전달된 캐릭터를 비어 있는 초상화 슬롯에 할당합니다.
    /// </summary>
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

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 초상화 슬롯을 표현하며 캐릭터 데이터를 표시하거나 비웁니다.
/// </summary>
public class PortraitSlot : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    private CharacterSO character;

    /// <summary>
    /// 스프라이트가 비어 있으면 빈 슬롯으로 간주합니다.
    /// </summary>
    public bool IsEmpty => portraitImage == null || portraitImage.sprite == null;

    /// <summary>
    /// 슬롯에 새 캐릭터를 할당하고 초상화를 표시합니다.
    /// </summary>
    public void SetSlot(CharacterSO newCharacter)
    {
        character = newCharacter;
        portraitImage.sprite = character.portraitSprite;
        portraitImage.enabled = true;

        // 혹시 알파가 0인 경우 대비합니다.
        var c = portraitImage.color;
        c.a = 1f;
        portraitImage.color = c;
    }

    /// <summary>
    /// 슬롯을 초기화해 캐릭터와 이미지를 제거합니다.
    /// </summary>
    public void ClearSlot()
    {
        character = null;
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }
    }
}

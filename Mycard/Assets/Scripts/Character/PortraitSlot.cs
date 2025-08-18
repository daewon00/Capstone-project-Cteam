using UnityEngine;
using UnityEngine.UI;

public class PortraitSlot : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    private CharacterSO character;

    // 빈 슬롯인지 확인(스프라이트 비었으면 빈 슬롯으로 간주)
    public bool IsEmpty => portraitImage == null || portraitImage.sprite == null;

    public void SetSlot(CharacterSO newCharacter)
    {
        character = newCharacter;
        portraitImage.sprite = character.portraitSprite;
        portraitImage.enabled = true;

        // 혹시 알파가 0인 경우 대비
        var c = portraitImage.color;
        c.a = 1f;
        portraitImage.color = c;
    }

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
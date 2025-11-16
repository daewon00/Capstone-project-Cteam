using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class RelicIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text stackText;

    private RelicData relicData;

    public void Setup(RelicData data, int stacks)
    {
        relicData = data;
        if (icon) icon.sprite = data ? data.icon : null;
        SetStacks(stacks);
    }

    public void SetStacks(int stacks)
    {
        if (!stackText) return;
        bool show = stacks > 1;
        stackText.gameObject.SetActive(show);
        if (show) stackText.text = stacks.ToString();
    }
    public void OnPointerEnter(PointerEventData eventData) => ShowTooltip();
    public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.HideTooltip();

    // 터치 단말에서도 툴팁을 볼 수 있도록 탭/꾹 누르기 입력을 처리한다.
    public void OnPointerDown(PointerEventData eventData) => ShowTooltip();
    public void OnPointerUp(PointerEventData eventData) => TooltipManager.Instance?.HideTooltip();

    private void OnDisable()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    private void ShowTooltip()
    {
        if (!relicData)
        {
            return;
        }

        TooltipManager.Instance?.ShowRelicTooltip(relicData);
    }
}

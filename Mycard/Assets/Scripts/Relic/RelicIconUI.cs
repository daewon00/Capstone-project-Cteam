using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class RelicIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!relicData)
        {
            return;
        }

        TooltipManager.Instance?.ShowRelicTooltip(relicData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance?.HideTooltip();
    }

    private void OnDisable()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}

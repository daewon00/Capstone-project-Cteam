using UnityEngine;

[RequireComponent(typeof(CardDisplay))]
public class CardDisplayTooltipSource : MonoBehaviour, ICardTooltipSource
{
    [SerializeField] private CardDisplay display;
    [SerializeField] private RectTransform anchorOverride;
    [SerializeField] private bool useHandOffsets;

    private void Awake()
    {
        if (display == null)
            display = GetComponent<CardDisplay>();
    }

    public void SetUseHandOffsets(bool value) => useHandOffsets = value;

    public void SetAnchor(RectTransform anchor) => anchorOverride = anchor;

    public void SetDisplay(CardDisplay target) => display = target;

    public CardTooltipData GetTooltipData()
    {
        if (display != null && display.HasBoundData)
            return new CardTooltipData(display.BoundDisplayName, display.BoundDescription ?? string.Empty);
        return new CardTooltipData(string.Empty, string.Empty);
    }

    public Vector3 GetTooltipAnchorWorldPos()
    {
        if (anchorOverride != null)
            return anchorOverride.position;
        if (display != null)
            return display.transform.position;
        return transform.position;
    }

    public bool ShouldUseHandOffset => useHandOffsets;

    public bool IsTooltipValid => display != null && display.gameObject.activeInHierarchy && display.HasBoundData;
}

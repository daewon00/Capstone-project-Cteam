using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private TooltipUI tooltipUI;
    [SerializeField] private TooltipDisplayMode initialDisplayMode = TooltipDisplayMode.Default;

    private TooltipDisplayMode _currentDisplayMode;

    public bool HasVisibleTooltip => tooltipUI != null && tooltipUI.IsVisible;

    private void Awake()
    {
        /*if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }*/

        Instance = this;
        
        if (!tooltipUI)
        {
            tooltipUI = GetComponentInChildren<TooltipUI>(true);
        }

        _currentDisplayMode = initialDisplayMode;
        ApplyDisplayMode();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowTooltip(string title, string description)
    {
        if (!tooltipUI)
        {
            return;
        }

        ResetDisplayMode();
        tooltipUI.Show(title, description);
    }

    public void ShowRelicTooltip(RelicData data)
    {
        if (!data)
        {
            HideTooltip();
            return;
        }

        ShowTooltip(data.displayName, data.description);
    }

    public void HideTooltip()
    {
        
        tooltipUI?.Hide();
    }

    public void ShowTooltipAtScreenPosition(string title, string description, Vector2 screenPosition, Vector2? customOffset = null)
    {
        ApplyDisplayMode();
        tooltipUI?.ShowAtScreenPoint(title, description, screenPosition, customOffset, false);
    }

    public void UpdateTooltipScreenPosition(Vector2 screenPosition, Vector2? customOffset = null)
    {
        tooltipUI?.UpdateScreenPosition(screenPosition, customOffset);
    }

    public void HideTooltipImmediate()
    {
        tooltipUI?.HideImmediate();
    }

    public void SetDisplayMode(TooltipDisplayMode mode)
    {
        _currentDisplayMode = mode;
        ApplyDisplayMode();
    }

    public void ResetDisplayMode()
    {
        _currentDisplayMode = TooltipDisplayMode.Default;
        ApplyDisplayMode();
    }

    private void ApplyDisplayMode()
    {
        tooltipUI?.ApplyStyle(_currentDisplayMode);
    }
}

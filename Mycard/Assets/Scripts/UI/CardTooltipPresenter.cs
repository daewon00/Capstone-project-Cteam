using UnityEngine;

/// <summary>
/// Standalone presenter that owns a TooltipUI instance dedicated to battle card tooltips.
/// No singleton usage; CardTooltipService keeps a direct reference to this component.
/// </summary>
public class CardTooltipPresenter : MonoBehaviour
{
    [SerializeField] private TooltipUI tooltipUI;
    [SerializeField] private TooltipDisplayMode displayMode = TooltipDisplayMode.Compact;
    [SerializeField, Tooltip("기본 스크린 오프셋(픽셀). null 전달 시 사용됩니다.")] private Vector2 defaultScreenOffset = new Vector2(0f, 48f);
    [SerializeField, Tooltip("필요 시 강제로 붙일 Canvas (비워두면 런타임에 자동으로 탐색)")]
    private Canvas parentCanvasOverride;

    private RectTransform _rect;

    private void Awake()
    {
        if (tooltipUI == null)
        {
            tooltipUI = GetComponentInChildren<TooltipUI>(true);
        }

        if (tooltipUI == null)
        {
            GameLog.Warn("[CardTooltipPresenter] TooltipUI reference missing.", this);
        }

        _rect = GetComponent<RectTransform>();
        EnsureParentCanvas();
    }

    public void AttachToCanvas(Canvas canvas)
    {
        if (canvas == null || _rect == null)
            return;

        parentCanvasOverride = canvas;
        _rect.SetParent(canvas.transform, false);
        tooltipUI?.RebindCanvasRefs();
    }

    public void EnsureParentCanvas()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        if (_rect == null)
            return;

        if (parentCanvasOverride != null)
        {
            if (_rect.transform.parent != parentCanvasOverride.transform)
            {
                _rect.SetParent(parentCanvasOverride.transform, false);
            }
            return;
        }

        var existingCanvas = _rect.GetComponentInParent<Canvas>();
        if (existingCanvas != null)
        {
            parentCanvasOverride = existingCanvas;
            return;
        }

        var fallbackCanvas = FindFirstObjectByType<Canvas>();
        if (fallbackCanvas != null)
        {
            AttachToCanvas(fallbackCanvas);
        }
        else
        {
            GameLog.Warn("[CardTooltipPresenter] Could not locate a Canvas to parent to. Tooltip may not render.", this);
        }
    }

    public void Show(CardTooltipData data, Vector2 screenPosition, Vector2? customOffset = null)
    {
        if (tooltipUI == null)
            return;

        tooltipUI.ApplyStyle(displayMode);
        tooltipUI.ShowAtScreenPoint(
            data.Title,
            data.Description,
            screenPosition,
            customOffset ?? defaultScreenOffset,
            followPointer: false);
    }

    public void UpdatePosition(Vector2 screenPosition, Vector2? customOffset = null)
    {
        tooltipUI?.UpdateScreenPosition(screenPosition, customOffset ?? defaultScreenOffset);
    }

    public void Hide()
    {
        tooltipUI?.Hide();
    }

    public void HideImmediate()
    {
        tooltipUI?.HideImmediate();
    }
}

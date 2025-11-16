using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 mouseOffset = new(24f, -24f);
    [SerializeField] private float hideDelay = 2f;

    [Header("Layout Components (optional)")]
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private VerticalLayoutGroup layoutGroup;

    [Header("Style Presets")]
    [SerializeField] private TooltipStyle defaultStyle = TooltipStyle.CreateDefault();
    [SerializeField] private TooltipStyle compactStyle = TooltipStyle.CreateCompact();

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private bool isVisible;
    private bool shouldFollowMouse = true;
    private Vector2 lastScreenPosition;
    private Coroutine hideCoroutine;
    private readonly Vector3[] canvasWorldCorners = new Vector3[4];
    private readonly Vector3[] tooltipWorldCorners = new Vector3[4];
    private Vector2 _activeOffset;
    private TooltipDisplayMode _currentMode = TooltipDisplayMode.Default;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        RebindCanvasRefs();
        _activeOffset = mouseOffset;
        ApplyStyle(_currentMode);
        HideImmediate();
    }

    public void RebindCanvasRefs()
    {
        if (!root)
            root = transform as RectTransform;
        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!layoutElement && root != null)
            layoutElement = root.GetComponent<LayoutElement>();
        if (!layoutGroup)
            layoutGroup = GetComponentInChildren<VerticalLayoutGroup>(true);

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas)
            canvasRect = parentCanvas.transform as RectTransform;
    }

    private void OnTransformParentChanged()
    {
        RebindCanvasRefs();
    }

    private void Update()
    {
        if (!isVisible)
            return;

        if (shouldFollowMouse)
            UpdateScreenPosition(Input.mousePosition);
    }

    public void Show(string title, string description)
    {
        ApplyStyle(_currentMode);
        ShowAtScreenPoint(title, description, Input.mousePosition, null, true);
    }

    public void ShowAtScreenPoint(string title, string description, Vector2 screenPosition, Vector2? customOffset = null, bool followPointer = false)
    {
        SetContent(title, description);
        CancelHideRoutine();
        shouldFollowMouse = followPointer;
        _activeOffset = customOffset ?? mouseOffset;
        isVisible = true;
        SetVisibleState(true);
        UpdateScreenPosition(screenPosition);
    }

    public void UpdateScreenPosition(Vector2 screenPosition, Vector2? customOffset = null)
    {
        if (!isVisible)
            return;

        if (customOffset.HasValue)
            _activeOffset = customOffset.Value;

        lastScreenPosition = screenPosition;
        UpdatePosition(lastScreenPosition);
    }

    private void SetContent(string title, string description)
    {
        if (titleText)
        {
            bool hasTitle = !string.IsNullOrWhiteSpace(title);
            titleText.text = hasTitle ? title : string.Empty;
            titleText.gameObject.SetActive(hasTitle);
        }

        if (descriptionText)
            descriptionText.text = string.IsNullOrEmpty(description) ? string.Empty : description;
    }

    public void Hide()
    {
        if (!isVisible)
            return;

        CancelHideRoutine();
        shouldFollowMouse = false;
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    public void HideImmediate()
    {
        CancelHideRoutine();
        shouldFollowMouse = false;
        isVisible = false;
        SetVisibleState(false);
    }

    private void SetVisibleState(bool visible)
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else if (root)
        {
            root.gameObject.SetActive(visible);
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        hideCoroutine = null;
        isVisible = false;
        SetVisibleState(false);
    }

    private void CancelHideRoutine()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private void UpdatePosition(Vector2 screenPosition)
    {
        if (!root || !canvasRect)
            return;

        var renderMode = parentCanvas ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        var camera = renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (renderMode == RenderMode.WorldSpace)
            camera = parentCanvas.worldCamera;

        lastScreenPosition = screenPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out var localPoint);
        localPoint += _activeOffset;

        Vector3 worldPoint = canvasRect.TransformPoint(localPoint);
        root.position = worldPoint;

        ClampToCanvasBounds();
    }

    private void ClampToCanvasBounds()
    {
        if (!canvasRect)
            return;

        canvasRect.GetWorldCorners(canvasWorldCorners);
        root.GetWorldCorners(tooltipWorldCorners);

        float canvasMinX = canvasWorldCorners[0].x;
        float canvasMinY = canvasWorldCorners[0].y;
        float canvasMaxX = canvasWorldCorners[2].x;
        float canvasMaxY = canvasWorldCorners[2].y;

        Vector3 adjustment = Vector3.zero;

        if (tooltipWorldCorners[0].x < canvasMinX)
            adjustment.x += canvasMinX - tooltipWorldCorners[0].x;
        if (tooltipWorldCorners[2].x > canvasMaxX)
            adjustment.x -= tooltipWorldCorners[2].x - canvasMaxX;
        if (tooltipWorldCorners[0].y < canvasMinY)
            adjustment.y += canvasMinY - tooltipWorldCorners[0].y;
        if (tooltipWorldCorners[2].y > canvasMaxY)
            adjustment.y -= tooltipWorldCorners[2].y - canvasMaxY;

        if (adjustment.sqrMagnitude > 0f)
            root.position += adjustment;
    }

    public void ApplyStyle(TooltipDisplayMode mode)
    {
        _currentMode = mode;
        ApplyStyle(GetStylePreset(mode));
    }

    private TooltipStyle GetStylePreset(TooltipDisplayMode mode)
    {
        return mode switch
        {
            TooltipDisplayMode.Compact => compactStyle ?? defaultStyle ?? TooltipStyle.CreateCompact(),
            _ => defaultStyle ?? TooltipStyle.CreateDefault()
        };
    }

    private void ApplyStyle(TooltipStyle style)
    {
        if (style == null)
            return;

        if (titleText && style.TitleFontSize > 0f)
            titleText.fontSize = style.TitleFontSize;

        if (descriptionText)
        {
            if (style.DescriptionFontSize > 0f)
                descriptionText.fontSize = style.DescriptionFontSize;
            descriptionText.lineSpacing = style.DescriptionLineSpacing;
        }

        if (layoutGroup != null)
        {
            layoutGroup.spacing = style.VerticalSpacing;
            layoutGroup.padding = style.GetPadding();
        }

        float height = Mathf.Max(0f, style.PanelHeight);
        if (layoutElement != null)
        {
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }
        else if (root != null)
        {
            var size = root.sizeDelta;
            size.y = height;
            root.sizeDelta = size;
        }
    }
}

[System.Serializable]
public class TooltipStyle
{
    [Tooltip("패널 전체 높이")] public float PanelHeight = 420f;
    [Tooltip("제목 폰트 크기(0 이하이면 변경하지 않음)")] public float TitleFontSize = 32f;
    [Tooltip("본문 폰트 크기(0 이하이면 변경하지 않음)")] public float DescriptionFontSize = 28f;
    [Tooltip("본문 라인 간격")] public float DescriptionLineSpacing = 0f;
    [Tooltip("레이아웃 요소 간 간격")] public float VerticalSpacing = 8f;
    [Tooltip("패딩 (Left, Right, Top, Bottom)")] public Vector4 Padding = new Vector4(32f, 32f, 28f, 32f);

    public RectOffset GetPadding()
    {
        return new RectOffset(
            Mathf.RoundToInt(Padding.x),
            Mathf.RoundToInt(Padding.y),
            Mathf.RoundToInt(Padding.z),
            Mathf.RoundToInt(Padding.w)
        );
    }

    public static TooltipStyle CreateDefault()
    {
        return new TooltipStyle
        {
            PanelHeight = 420f,
            TitleFontSize = 32f,
            DescriptionFontSize = 28f,
            DescriptionLineSpacing = 4f,
            VerticalSpacing = 8f,
            Padding = new Vector4(40f, 40f, 32f, 38f)
        };
    }

    public static TooltipStyle CreateCompact()
    {
        return new TooltipStyle
        {
            PanelHeight = 290f,
            TitleFontSize = 30f,
            DescriptionFontSize = 26f,
            DescriptionLineSpacing = 2f,
            VerticalSpacing = 6f,
            Padding = new Vector4(28f, 28f, 24f, 28f)
        };
    }
}

public enum TooltipDisplayMode
{
    Default = 0,
    Compact = 1
}

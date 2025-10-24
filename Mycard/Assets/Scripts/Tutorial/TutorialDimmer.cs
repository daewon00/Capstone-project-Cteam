using UnityEngine;

/// <summary>
/// 튜토리얼 단계에서 화면을 어둡게 처리하고 강조 영역만 남겨두는 뷰 컨트롤러입니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class TutorialDimmer : MonoBehaviour
{
    [SerializeField] private RectTransform topBlock;
    [SerializeField] private RectTransform bottomBlock;
    [SerializeField] private RectTransform leftBlock;
    [SerializeField] private RectTransform rightBlock;
    [SerializeField] private RectTransform highlightFrame;
    [SerializeField] private float highlightPadding = 12f;

    private CanvasGroup _group;
    private RectTransform _rootRect;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _rootRect = transform as RectTransform;
        Hide();
    }

    public void Apply(TutorialStepConfig config, RectTransform highlight)
    {
        if (config == null || config.InteractionGate == TutorialInteractionGate.None)
        {
            Hide();
            return;
        }

        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;

        if (highlight == null && config.HighlightOptional)
        {
            HideHighlight();
            return;
        }

        if (highlight == null)
        {
            CoverAll();
            return;
        }

        if (!TryGetHighlightRect(highlight, out var rect))
        {
            CoverAll();
            return;
        }

        InflateRect(ref rect, highlightPadding);
        ApplyCutout(rect);
    }

    private void Hide()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        HideHighlight();
    }

    private void HideHighlight()
    {
        if (highlightFrame != null)
        {
            highlightFrame.gameObject.SetActive(false);
        }
        CoverAll();
    }

    private void CoverAll()
    {
        if (topBlock != null)
        {
            topBlock.anchorMin = Vector2.zero;
            topBlock.anchorMax = Vector2.one;
            topBlock.offsetMin = Vector2.zero;
            topBlock.offsetMax = Vector2.zero;
            topBlock.gameObject.SetActive(true);
        }
        if (bottomBlock != null) bottomBlock.gameObject.SetActive(false);
        if (leftBlock != null) leftBlock.gameObject.SetActive(false);
        if (rightBlock != null) rightBlock.gameObject.SetActive(false);
    }

    private void ApplyCutout(Rect rect)
    {
        var canvasRect = _rootRect.rect;
        float width = canvasRect.width;
        float height = canvasRect.height;

        float leftNorm = Mathf.Clamp01((rect.xMin + canvasRect.width * _rootRect.pivot.x) / width);
        float rightNorm = Mathf.Clamp01((rect.xMax + canvasRect.width * _rootRect.pivot.x) / width);
        float bottomNorm = Mathf.Clamp01((rect.yMin + canvasRect.height * _rootRect.pivot.y) / height);
        float topNorm = Mathf.Clamp01((rect.yMax + canvasRect.height * _rootRect.pivot.y) / height);

        if (topBlock != null)
        {
            topBlock.gameObject.SetActive(true);
            topBlock.anchorMin = new Vector2(0f, topNorm);
            topBlock.anchorMax = new Vector2(1f, 1f);
            topBlock.offsetMin = Vector2.zero;
            topBlock.offsetMax = Vector2.zero;
        }

        if (bottomBlock != null)
        {
            bottomBlock.gameObject.SetActive(true);
            bottomBlock.anchorMin = new Vector2(0f, 0f);
            bottomBlock.anchorMax = new Vector2(1f, bottomNorm);
            bottomBlock.offsetMin = Vector2.zero;
            bottomBlock.offsetMax = Vector2.zero;
        }

        if (leftBlock != null)
        {
            leftBlock.gameObject.SetActive(true);
            leftBlock.anchorMin = new Vector2(0f, bottomNorm);
            leftBlock.anchorMax = new Vector2(leftNorm, topNorm);
            leftBlock.offsetMin = Vector2.zero;
            leftBlock.offsetMax = Vector2.zero;
        }

        if (rightBlock != null)
        {
            rightBlock.gameObject.SetActive(true);
            rightBlock.anchorMin = new Vector2(rightNorm, bottomNorm);
            rightBlock.anchorMax = new Vector2(1f, topNorm);
            rightBlock.offsetMin = Vector2.zero;
            rightBlock.offsetMax = Vector2.zero;
        }

        if (highlightFrame != null)
        {
            highlightFrame.gameObject.SetActive(true);
            highlightFrame.anchorMin = new Vector2(leftNorm, bottomNorm);
            highlightFrame.anchorMax = new Vector2(rightNorm, topNorm);
            highlightFrame.offsetMin = Vector2.zero;
            highlightFrame.offsetMax = Vector2.zero;
        }
    }

    private bool TryGetHighlightRect(RectTransform target, out Rect rect)
    {
        rect = default;
        if (_rootRect == null || target == null)
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);
        Vector2[] localCorners = new Vector2[4];

        var canvas = GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]), camera, out localCorners[i]);
        }

        Vector2 min = localCorners[0];
        Vector2 max = localCorners[0];
        for (int i = 1; i < localCorners.Length; i++)
        {
            min = Vector2.Min(min, localCorners[i]);
            max = Vector2.Max(max, localCorners[i]);
        }

        rect = new Rect(min, max - min);
        return true;
    }

    private static void InflateRect(ref Rect rect, float amount)
    {
        rect.xMin -= amount;
        rect.xMax += amount;
        rect.yMin -= amount;
        rect.yMax += amount;
    }
}

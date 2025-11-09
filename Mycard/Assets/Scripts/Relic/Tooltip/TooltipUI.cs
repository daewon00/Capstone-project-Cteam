using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class TooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 mouseOffset = new(24f, -24f);
    [SerializeField] private float hideDelay = 2f;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private bool isVisible;
    private bool shouldFollowMouse = true;
    private Vector2 lastScreenPosition;
    private Coroutine hideCoroutine;
    private readonly Vector3[] canvasWorldCorners = new Vector3[4];
    private readonly Vector3[] tooltipWorldCorners = new Vector3[4];

    private void Awake()
    {
        if (!root)
        {
            root = transform as RectTransform;
        }

        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas)
        {
            canvasRect = parentCanvas.transform as RectTransform;
        }

        HideImmediate();
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        if (shouldFollowMouse)
        {
            lastScreenPosition = Input.mousePosition;
            UpdatePosition(lastScreenPosition);
        }
    }

    public void Show(string title, string description)
    {
        if (titleText)
        {
            titleText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
        }

        if (descriptionText)
        {
            descriptionText.text = string.IsNullOrEmpty(description) ? string.Empty : description;
        }
        CancelHideRoutine();
        shouldFollowMouse = true;
        isVisible = true;
        SetVisibleState(true);
        lastScreenPosition = Input.mousePosition;
        UpdatePosition(lastScreenPosition);
    }

    public void Hide()
    {
        if (!isVisible)
        {
            return;
        }

        CancelHideRoutine();
        shouldFollowMouse = false;
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void HideImmediate()
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
        {
            return;
        }

        var renderMode = parentCanvas ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        var camera = renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        if (renderMode == RenderMode.WorldSpace)
        {
            camera = parentCanvas.worldCamera;
        }
        lastScreenPosition = screenPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out var localPoint);
        localPoint += mouseOffset;

        Vector3 worldPoint = canvasRect.TransformPoint(localPoint);
        root.position = worldPoint;

        ClampToCanvasBounds();
    }

    private void ClampToCanvasBounds()
    {
        if (!canvasRect)
        {
            return;
        }

        canvasRect.GetWorldCorners(canvasWorldCorners);
        root.GetWorldCorners(tooltipWorldCorners);

        float canvasMinX = canvasWorldCorners[0].x;
        float canvasMinY = canvasWorldCorners[0].y;
        float canvasMaxX = canvasWorldCorners[2].x;
        float canvasMaxY = canvasWorldCorners[2].y;

        Vector3 adjustment = Vector3.zero;

        if (tooltipWorldCorners[0].x < canvasMinX)
        {
            adjustment.x += canvasMinX - tooltipWorldCorners[0].x;
        }

        if (tooltipWorldCorners[2].x > canvasMaxX)
        {
            adjustment.x -= tooltipWorldCorners[2].x - canvasMaxX;
        }

        if (tooltipWorldCorners[0].y < canvasMinY)
        {
            adjustment.y += canvasMinY - tooltipWorldCorners[0].y;
        }

        if (tooltipWorldCorners[2].y > canvasMaxY)
        {
            adjustment.y -= tooltipWorldCorners[2].y - canvasMaxY;
        }

        if (adjustment.sqrMagnitude > 0f)
        {
            root.position += adjustment;
        }
    }
}

using UnityEngine;

/// <summary>
/// 오버레이(Overlay Canvas) 하위에서, 원본(월드/카메라 기반)의 화면 사각을 투영하여
/// 자신의 RectTransform으로 반영하는 프록시입니다. TutorialDimmer의 하이라이트 대상으로 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldRectProxy : MonoBehaviour
{
    [SerializeField] private RectTransform overlayRoot; // 오버레이 루트(RectTransform)
    [SerializeField] private Transform sourceTransform; // 원본(카드 루트 등)
    [SerializeField] private Renderer sourceRenderer;   // 우선 사용(있으면)
    [SerializeField] private Collider sourceCollider;   // 대안(없으면)
    [SerializeField] private RectTransform sourceUiRect; // 최후 대안(Front UI Rect 등)
    [SerializeField] private float paddingPx = 12f;
    [SerializeField] private Camera overrideCamera; // 투영 카메라(지정 없으면 자동 선택)

    private RectTransform _self;

    public void Bind(RectTransform overlay, Transform srcTf, Renderer srcRend, Collider srcCol, RectTransform srcUi, Camera cam, float padding)
    {
        overlayRoot = overlay;
        sourceTransform = srcTf;
        sourceRenderer = srcRend;
        sourceCollider = srcCol;
        sourceUiRect = srcUi;
        overrideCamera = cam;
        paddingPx = padding;

        if (_self == null) _self = transform as RectTransform;
        if (_self != null)
        {
            _self.anchorMin = Vector2.zero;
            _self.anchorMax = Vector2.zero;
            _self.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void Awake()
    {
        _self = transform as RectTransform;
    }

    private void LateUpdate()
    {
        UpdateProxyRect();
    }

    public void UpdateProxyRect()
    {
        if (_self == null || overlayRoot == null) return;

        if (!TryComputeLocalRect(out var localMin, out var localMax))
        {
            return;
        }

        // 패딩 적용
        localMin.x -= paddingPx;
        localMin.y -= paddingPx;
        localMax.x += paddingPx;
        localMax.y += paddingPx;

        // 크기가 말이 되도록 보정(최소값)
        const float minSize = 24f;
        if (localMax.x - localMin.x < minSize)
        {
            float cx = (localMin.x + localMax.x) * 0.5f;
            localMin.x = cx - minSize * 0.5f;
            localMax.x = cx + minSize * 0.5f;
        }
        if (localMax.y - localMin.y < minSize)
        {
            float cy = (localMin.y + localMax.y) * 0.5f;
            localMin.y = cy - minSize * 0.5f;
            localMax.y = cy + minSize * 0.5f;
        }

        // RectTransform 설정(anchors는 (0,0))
        Vector2 size = localMax - localMin;
        Vector2 center = (localMin + localMax) * 0.5f;
        _self.sizeDelta = size;
        _self.anchoredPosition = center;
    }

    private bool TryComputeLocalRect(out Vector2 localMin, out Vector2 localMax)
    {
        localMin = Vector2.zero;
        localMax = Vector2.zero;
        if (overlayRoot == null) return false;

        Camera cam = SelectProjectionCamera();
        if (sourceRenderer != null)
        {
            return TryProjectBounds(sourceRenderer.bounds, cam, overlayRoot, out localMin, out localMax);
        }
        if (sourceCollider != null)
        {
            return TryProjectBounds(sourceCollider.bounds, cam, overlayRoot, out localMin, out localMax);
        }
        if (sourceUiRect != null)
        {
            return TryProjectUiRect(sourceUiRect, cam, overlayRoot, out localMin, out localMax);
        }
        // 마지막 대안: 소스 트랜스폼의 위치를 작은 사각으로 표시
        Vector3 wp = sourceTransform != null ? sourceTransform.position : Vector3.zero;
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, wp);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, sp, GetOverlayCamera(), out var lp))
        {
            localMin = lp - new Vector2(8f, 8f);
            localMax = lp + new Vector2(8f, 8f);
            return true;
        }
        return false;
    }

    private Camera SelectProjectionCamera()
    {
        if (overrideCamera != null) return overrideCamera;

        // UI Rect가 소스일 경우, 해당 Canvas의 카메라 우선
        if (sourceUiRect != null)
        {
            var uiCanvas = sourceUiRect.GetComponentInParent<Canvas>();
            if (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCanvas.worldCamera != null)
            {
                return uiCanvas.worldCamera;
            }
        }

        // 렌더러/콜라이더인 경우 기본 메인 카메라
        return Camera.main;
    }

    private Camera GetOverlayCamera()
    {
        var cv = overlayRoot.GetComponentInParent<Canvas>();
        if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return cv.worldCamera;
        }
        return null; // Overlay
    }

    private static bool TryProjectBounds(Bounds b, Camera cam, RectTransform overlay, out Vector2 localMin, out Vector2 localMax)
    {
        localMin = Vector2.zero; localMax = Vector2.zero;
        if (cam == null || overlay == null) return false;

        // 8개 꼭짓점
        Vector3 c = b.center; Vector3 e = b.extents;
        Vector3[] wc = new Vector3[8]
        {
            c + new Vector3( e.x,  e.y,  e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x, -e.y, -e.z)
        };

        Vector2 spMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 spMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < wc.Length; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(wc[i]);
            spMin = Vector2.Min(spMin, sp);
            spMax = Vector2.Max(spMax, sp);
        }

        var overlayCam = overlay.GetComponentInParent<Canvas>()?.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMin, overlayCam, out localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMax, overlayCam, out localMax);
        return true;
    }

    private static bool TryProjectUiRect(RectTransform uiRect, Camera cam, RectTransform overlay, out Vector2 localMin, out Vector2 localMax)
    {
        localMin = Vector2.zero; localMax = Vector2.zero;
        if (overlay == null || uiRect == null) return false;

        Vector3[] worldCorners = new Vector3[4];
        uiRect.GetWorldCorners(worldCorners);
        Vector2 spMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 spMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
            spMin = Vector2.Min(spMin, sp);
            spMax = Vector2.Max(spMax, sp);
        }

        var overlayCam = overlay.GetComponentInParent<Canvas>()?.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMin, overlayCam, out localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMax, overlayCam, out localMax);
        return true;
    }
}


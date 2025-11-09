using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 원본(월드/카메라 기반/UI Rect)의 화면 사각들의 합집합을
/// 오버레이 좌표계 RectTransform으로 반영하는 프록시입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MultiWorldRectProxy : MonoBehaviour
{
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private Camera overrideCamera;
    [SerializeField] private float paddingPx = 12f;

    private readonly List<Transform> _srcTf = new();
    private readonly List<Renderer> _srcRend = new();
    private readonly List<Collider> _srcCol = new();
    private readonly List<RectTransform> _srcUi = new();

    private RectTransform _self;

    public void Bind(RectTransform overlay, Camera cam, float padding)
    {
        overlayRoot = overlay; overrideCamera = cam; paddingPx = padding;
        if (_self == null) _self = transform as RectTransform;
        if (_self != null)
        {
            _self.anchorMin = Vector2.zero;
            _self.anchorMax = Vector2.zero;
            _self.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    public void AddSource(Transform tf, Renderer rend, Collider col, RectTransform uiRect)
    {
        if (tf != null && !_srcTf.Contains(tf)) _srcTf.Add(tf);
        if (rend != null && !_srcRend.Contains(rend)) _srcRend.Add(rend);
        if (col != null && !_srcCol.Contains(col)) _srcCol.Add(col);
        if (uiRect != null && !_srcUi.Contains(uiRect)) _srcUi.Add(uiRect);
    }

    private void Awake()
    {
        _self = transform as RectTransform;
    }

    private void LateUpdate()
    {
        UpdateProxyRect();
    }

    private void UpdateProxyRect()
    {
        if (_self == null || overlayRoot == null) return;

        if (!TryComputeLocalRect(out var localMin, out var localMax)) return;

        // padding
        localMin.x -= paddingPx; localMin.y -= paddingPx;
        localMax.x += paddingPx; localMax.y += paddingPx;

        const float minSize = 24f;
        if (localMax.x - localMin.x < minSize)
        {
            float cx = (localMin.x + localMax.x) * 0.5f; localMin.x = cx - minSize * 0.5f; localMax.x = cx + minSize * 0.5f;
        }
        if (localMax.y - localMin.y < minSize)
        {
            float cy = (localMin.y + localMax.y) * 0.5f; localMin.y = cy - minSize * 0.5f; localMax.y = cy + minSize * 0.5f;
        }

        Vector2 size = localMax - localMin; Vector2 center = (localMin + localMax) * 0.5f;
        _self.sizeDelta = size; _self.anchoredPosition = center;
    }

    private bool TryComputeLocalRect(out Vector2 localMin, out Vector2 localMax)
    {
        localMin = new Vector2(float.MaxValue, float.MaxValue);
        localMax = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        Camera cam = SelectProjectionCamera();

        // UI Rects
        for (int i = 0; i < _srcUi.Count; i++)
        {
            var ui = _srcUi[i]; if (ui == null) continue;
            if (ProjectUiRect(ui, cam, overlayRoot, out var a, out var b)) { any = true; localMin = Vector2.Min(localMin, a); localMax = Vector2.Max(localMax, b); }
        }
        // Renderers
        for (int i = 0; i < _srcRend.Count; i++)
        {
            var r = _srcRend[i]; if (r == null) continue;
            if (ProjectBounds(r.bounds, cam, overlayRoot, out var a, out var b)) { any = true; localMin = Vector2.Min(localMin, a); localMax = Vector2.Max(localMax, b); }
        }
        // Colliders
        for (int i = 0; i < _srcCol.Count; i++)
        {
            var c = _srcCol[i]; if (c == null) continue;
            if (ProjectBounds(c.bounds, cam, overlayRoot, out var a, out var b)) { any = true; localMin = Vector2.Min(localMin, a); localMax = Vector2.Max(localMax, b); }
        }

        if (!any)
        {
            // 마지막 대안: 트랜스폼 포인트들의 작은 사각
            for (int i = 0; i < _srcTf.Count; i++)
            {
                var tf = _srcTf[i]; if (tf == null) continue;
                Vector3 sp = RectTransformUtility.WorldToScreenPoint(cam, tf.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, sp, GetOverlayCamera(), out var lp))
                {
                    any = true;
                    Vector2 a = lp - new Vector2(8f, 8f); Vector2 b = lp + new Vector2(8f, 8f);
                    localMin = Vector2.Min(localMin, a); localMax = Vector2.Max(localMax, b);
                }
            }
        }

        if (!any) { localMin = Vector2.zero; localMax = Vector2.zero; }
        return any;
    }

    private Camera SelectProjectionCamera()
    {
        if (overrideCamera != null) return overrideCamera;
        return Camera.main;
    }

    private Camera GetOverlayCamera()
    {
        var cv = overlayRoot.GetComponentInParent<Canvas>();
        if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) return cv.worldCamera;
        return null;
    }

    private static bool ProjectBounds(Bounds b, Camera cam, RectTransform overlay, out Vector2 localMin, out Vector2 localMax)
    {
        localMin = Vector2.zero; localMax = Vector2.zero;
        if (cam == null || overlay == null) return false;
        Vector3 c = b.center; Vector3 e = b.extents;
        Vector3[] wc = new Vector3[8]
        {
            c + new Vector3( e.x,  e.y,  e.z), c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z), c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z), c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z), c + new Vector3(-e.x, -e.y, -e.z)
        };
        Vector2 spMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 spMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < wc.Length; i++) { Vector3 sp = cam.WorldToScreenPoint(wc[i]); spMin = Vector2.Min(spMin, sp); spMax = Vector2.Max(spMax, sp); }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMin, overlay.GetComponentInParent<Canvas>()?.worldCamera, out localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMax, overlay.GetComponentInParent<Canvas>()?.worldCamera, out localMax);
        return true;
    }

    private static bool ProjectUiRect(RectTransform uiRect, Camera cam, RectTransform overlay, out Vector2 localMin, out Vector2 localMax)
    {
        localMin = Vector2.zero; localMax = Vector2.zero;
        if (overlay == null || uiRect == null) return false;
        Vector3[] worldCorners = new Vector3[4]; uiRect.GetWorldCorners(worldCorners);
        Vector2 spMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 spMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++) { Vector3 sp = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]); spMin = Vector2.Min(spMin, sp); spMax = Vector2.Max(spMax, sp); }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMin, overlay.GetComponentInParent<Canvas>()?.worldCamera, out localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, spMax, overlay.GetComponentInParent<Canvas>()?.worldCamera, out localMax);
        return true;
    }
}


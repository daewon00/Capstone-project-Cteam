using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays small, non-blocking toasts when achievements are unlocked.
/// Self-contained: creates its own Canvas/anchor if none provided.
/// </summary>
public sealed class AchievementsToastController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 anchorMargin = new Vector2(24, 24);
    [SerializeField] private int maxStack = 3;
    [SerializeField] private float showDuration = 2.5f;
    [SerializeField] private float slideTime = 0.25f;
    [SerializeField] private float fadeTime = 0.2f;
    [SerializeField] private Vector2 itemSize = new Vector2(360, 80);
    [SerializeField] private Vector2 spacing = new Vector2(0, 8);
    [SerializeField] private bool dedupeWithinWindow = true;

    [Header("Prefab (optional, recommended)")]
    [SerializeField] private AchievementToastView toastPrefab; // If null, tries Resources/UI/AchievementToastView

    private Transform _anchor;
    private VerticalLayoutGroup _vLayout;
    private readonly Queue<MetaEvents.AchievementUnlockedPayload> _queue = new();
    private readonly HashSet<string> _visibleIds = new(System.StringComparer.OrdinalIgnoreCase);
    private int _showing;
    private bool _initialized;

    // Dedicated, persistent canvas shared across scenes
    private static Canvas sCanvas;
    private static RectTransform sSafeAreaRoot;
    private static Transform sAnchor;
    private static AchievementsToastController sInstance;

    private void Awake()
    {
        // Singletonize to avoid duplicates across scenes
        if (sInstance != null && sInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        sInstance = this;

        TryEnsureSetup();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        TryEnsureSetup();
        MetaEvents.OnAchievementUnlocked += Enqueue;
    }

    private void OnDisable()
    {
        MetaEvents.OnAchievementUnlocked -= Enqueue;
    }

    private void TryEnsureSetup()
    {
        if (_initialized) return;
        // Try bind prefab from Resources when not set via Inspector
        if (toastPrefab == null)
        {
            try { toastPrefab = Resources.Load<AchievementToastView>("UI/AchievementToastView"); }
            catch { toastPrefab = null; }
        }
        // Always use a dedicated, persistent overlay canvas (do not attach to scene canvases)
        if (sCanvas == null || sCanvas.gameObject == null)
        {
            var go = new GameObject("AchievementToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500; // high enough for most overlays
            var scaler = go.GetComponent<CanvasScaler>();
            // Default scaler
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Attempt to mirror main UI CanvasScaler for consistency
            try
            {
                var all = FindObjectsOfType<Canvas>(true);
                foreach (var c in all)
                {
                    if (c == null || c == canvas) continue;
                    if (!c.isRootCanvas) continue;
                    var cs = c.GetComponent<CanvasScaler>();
                    if (cs != null)
                    {
                        scaler.uiScaleMode = cs.uiScaleMode;
                        scaler.referenceResolution = cs.referenceResolution;
                        scaler.screenMatchMode = cs.screenMatchMode;
                        scaler.matchWidthOrHeight = cs.matchWidthOrHeight;
                        scaler.referencePixelsPerUnit = cs.referencePixelsPerUnit;
                        break;
                    }
                }
            }
            catch { }
            DontDestroyOnLoad(go);
            sCanvas = canvas;
        }

        // Safe Area root to keep content within visible area on devices with notches/status bars
        if (sSafeAreaRoot == null || sSafeAreaRoot.gameObject == null)
        {
            var root = new GameObject("SafeAreaRoot", typeof(RectTransform));
            root.transform.SetParent(sCanvas.transform, false);
            sSafeAreaRoot = (RectTransform)root.transform;
            ApplySafeArea(sSafeAreaRoot);
        }

        if (sAnchor == null || sAnchor.gameObject == null)
        {
            var anchorGO = new GameObject("ToastAnchor", typeof(RectTransform));
            anchorGO.transform.SetParent(sSafeAreaRoot != null ? sSafeAreaRoot : sCanvas.transform, false);
            var rt = (RectTransform)anchorGO.transform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-anchorMargin.x, -anchorMargin.y);
            _vLayout = anchorGO.AddComponent<VerticalLayoutGroup>();
            _vLayout.childAlignment = TextAnchor.UpperRight;
            _vLayout.childForceExpandHeight = false;
            _vLayout.childForceExpandWidth = false;
            _vLayout.spacing = spacing.y;
            var fitter = anchorGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sAnchor = anchorGO.transform;
        }

        _anchor = sAnchor;

        _initialized = true;
    }

    private void Enqueue(MetaEvents.AchievementUnlockedPayload p)
    {
        // Optional de-dupe: if already visible, ignore
        if (dedupeWithinWindow && _visibleIds.Contains(p.AchievementId)) return;
        _queue.Enqueue(p);
        TryDequeue();
    }

    private void TryDequeue()
    {
        while (_showing < maxStack && _queue.Count > 0)
        {
            var p = _queue.Dequeue();
            StartCoroutine(ShowToastCo(p));
        }
    }

    private IEnumerator ShowToastCo(MetaEvents.AchievementUnlockedPayload p)
    {
        _showing++;
        _visibleIds.Add(p.AchievementId);

        // Build view (prefab-first, fallback to simple generated)
        AchievementToastView view = null;
        RectTransform rt = null;
        CanvasGroup cg = null;

        // Create a layout row container to avoid fighting with VerticalLayoutGroup during animation
        var rowGO = new GameObject("ToastRow", typeof(RectTransform), typeof(LayoutElement));
        var rowRT = (RectTransform)rowGO.transform;
        rowRT.SetParent(_anchor, false);
        rowRT.anchorMin = new Vector2(1, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(1, 1);
        rowRT.anchoredPosition = Vector2.zero;
        var rowLE = rowGO.GetComponent<LayoutElement>();
        rowLE.flexibleHeight = 0;
        if (_anchor == null) { TryEnsureSetup(); }
        if (toastPrefab != null)
        {
            view = Instantiate(toastPrefab, rowRT);
            rt = view.GetComponent<RectTransform>();
            cg = view.CanvasGroup;
            if (cg != null) cg.alpha = 0f;
            view.Bind(p);
            // Use view's layout for row height if available
            var vLE = view.GetComponent<LayoutElement>();
            if (vLE != null && vLE.preferredHeight > 0)
                rowLE.preferredHeight = vLE.preferredHeight;
            else
                rowLE.preferredHeight = itemSize.y;
            // Ensure view is anchored to top-right within row
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
        }
        else
        {
            var go = new GameObject($"Toast_{p.AchievementId}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(rowRT, false);
            rt = (RectTransform)go.transform;
            rt.sizeDelta = itemSize;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            bg.raycastTarget = false;
            cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            // Minimal text fallback
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(go.transform, false);
            var titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
            titleTMP.text = string.IsNullOrEmpty(p.DisplayName) ? p.AchievementId : p.DisplayName;
            titleTMP.fontSize = 22f; titleTMP.color = new Color(1f, 0.95f, 0.7f, 1f);
            var subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGO.transform.SetParent(go.transform, false);
            var subTMP = subGO.GetComponent<TextMeshProUGUI>();
            subTMP.text = string.IsNullOrEmpty(p.Description) ? $"+{p.Points} pt" : p.Description;
            subTMP.fontSize = 16f; subTMP.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            // Default row height for fallback
            rowLE.preferredHeight = itemSize.y;
            // Anchor to top-right inside row
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
        }

        if (rt == null || this == null || _anchor == null)
        {
            _visibleIds.Remove(p.AchievementId);
            _showing--;
            if (rowRT != null) Destroy(rowRT.gameObject);
            yield break;
        }

        // Slide in from right
        Vector2 start = new Vector2(40f, 0f);
        Vector2 end = Vector2.zero;
        if (rt == null) { _visibleIds.Remove(p.AchievementId); _showing--; yield break; }
        rt.anchoredPosition = start;
        float t = 0f;
        while (t < slideTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / slideTime);
            if (rt == null) { _visibleIds.Remove(p.AchievementId); _showing--; yield break; }
            rt.anchoredPosition = Vector2.Lerp(start, end, EaseOutCubic(k));
            if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }
        if (rt == null) { _visibleIds.Remove(p.AchievementId); _showing--; yield break; }
        rt.anchoredPosition = end;
        if (cg != null) cg.alpha = 1f;

        // Hold
        float hold = Mathf.Max(0f, showDuration);
        float timer = 0f;
        while (timer < hold)
        {
            timer += Time.unscaledDeltaTime;
            if (rt == null) { _visibleIds.Remove(p.AchievementId); _showing--; yield break; }
            yield return null;
        }

        // Fade out and slide down slightly
        t = 0f;
        Vector2 outPos = end + new Vector2(0f, -10f);
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);
            if (rt == null) { _visibleIds.Remove(p.AchievementId); _showing--; yield break; }
            rt.anchoredPosition = Vector2.Lerp(end, outPos, k);
            if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }
        if (cg != null) cg.alpha = 0f;

        // Cleanup
        _visibleIds.Remove(p.AchievementId);
        if (rowRT != null && rowRT.gameObject != null) Destroy(rowRT.gameObject);
        _showing--;
        TryDequeue();
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private void OnDestroy()
    {
        // Stop animations to prevent referencing destroyed transforms
        try { StopAllCoroutines(); } catch { }
        if (sInstance == this) sInstance = null;
    }

    private static void ApplySafeArea(RectTransform target)
    {
        try
        {
            var sa = Screen.safeArea;
            var w = (float)Screen.width;
            var h = (float)Screen.height;
            if (w <= 0 || h <= 0) { target.anchorMin = Vector2.zero; target.anchorMax = Vector2.one; return; }
            var min = new Vector2(sa.xMin / w, sa.yMin / h);
            var max = new Vector2(sa.xMax / w, sa.yMax / h);
            target.anchorMin = min;
            target.anchorMax = max;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            target.pivot = new Vector2(0.5f, 0.5f);
        }
        catch
        {
            target.anchorMin = Vector2.zero; target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero; target.offsetMax = Vector2.zero;
        }
    }
}

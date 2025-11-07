using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 튜토리얼 안내 문구와 딤머를 함께 제어하는 뷰 컨트롤러입니다.
/// </summary>
public sealed class TutorialOverlayView : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    private struct AnchorSlot
    {
        public TutorialAnchor Anchor;
        public RectTransform Pivot;
    }

    [Header("UI References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private TutorialDimmer dimmer;
    [SerializeField] private AnchorSlot[] anchorSlots;

    private readonly Dictionary<TutorialAnchor, RectTransform> _anchorLookup = new();
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private TutorialStepConfig _currentConfig;
    private RectTransform _currentHighlight;

    private void Awake()
    {
        if (rootGroup == null) rootGroup = GetComponent<CanvasGroup>();
        if (panel == null) panel = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

        if (anchorSlots != null)
        {
            foreach (var slot in anchorSlots)
            {
                if (slot.Pivot == null) continue;
                _anchorLookup[slot.Anchor] = slot.Pivot;
            }
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null)
        {
            svc.OnStepVisualChanged += HandleStepVisualChanged;
            HandleStepVisualChanged(svc.CurrentConfig, svc.CurrentHighlight);
        }
    }

    private void OnDisable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null)
        {
            svc.OnStepVisualChanged -= HandleStepVisualChanged;
        }
    }

    private void HandleStepVisualChanged(TutorialStepConfig config, RectTransform highlight)
    {
        _currentConfig = config;
        _currentHighlight = highlight;

        if (config == null)
        {
            SetVisible(false);
            dimmer?.Apply(null, null);
            return;
        }

        SetVisible(true);
        if (messageLabel != null)
        {
            messageLabel.text = string.IsNullOrEmpty(config.Message)
                ? string.Empty
                : config.Message;
        }

        ApplyAnchor(config, highlight);
        dimmer?.Apply(config, highlight);
    }

    private void ApplyAnchor(TutorialStepConfig config, RectTransform highlight)
    {
        if (panel == null) return;

        var anchor = config.PreferFallback ? config.FallbackAnchor : config.PrimaryAnchor;
        if (!TryApplyAnchor(anchor, panel))
        {
            TryApplyAnchor(TutorialAnchor.Center, panel);
        }

        if (highlight == null || _canvasRect == null)
        {
            return;
        }

        if (config.PreferFallback)
        {
            return;
        }

        if (OverlapsHighlight(panel, highlight))
        {
            if (!TryApplyAnchor(config.FallbackAnchor, panel))
            {
                TryApplyAnchor(TutorialAnchor.Bottom, panel);
            }
        }
    }

    private bool TryApplyAnchor(TutorialAnchor anchor, RectTransform target)
    {
        if (!_anchorLookup.TryGetValue(anchor, out var slot) || slot == null)
        {
            return false;
        }

        target.pivot = slot.pivot;
        target.anchorMin = slot.anchorMin;
        target.anchorMax = slot.anchorMax;
        target.anchoredPosition = slot.anchoredPosition;
        target.localRotation = slot.localRotation;
        target.localScale = slot.localScale;
        return true;
    }

    private bool OverlapsHighlight(RectTransform overlay, RectTransform highlight)
    {
        if (_canvasRect == null || overlay == null || highlight == null)
        {
            return false;
        }

        if (!TryGetRect(overlay, out var overlayRect))
        {
            return false;
        }

        if (!TryGetRect(highlight, out var highlightRect))
        {
            return false;
        }

        return overlayRect.Overlaps(highlightRect);
    }

    private bool TryGetRect(RectTransform rectTransform, out Rect rect)
    {
        rect = default;
        if (rectTransform == null || _canvasRect == null) return false;

        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        Vector2 min = worldCorners[0];
        Vector2 max = worldCorners[0];

        var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        for (int i = 1; i < worldCorners.Length; i++)
        {
            min = Vector2.Min(min, worldCorners[i]);
            max = Vector2.Max(max, worldCorners[i]);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, min, camera, out var localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, max, camera, out var localMax);

        rect = new Rect(localMin, localMax - localMin);
        return true;
    }

    private void SetVisible(bool visible)
    {
        if (rootGroup == null) return;
        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.blocksRaycasts = visible;
        rootGroup.interactable = visible;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        svc?.TryAdvanceOverlayStep();
    }
}

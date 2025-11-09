using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 안내 문구와 딤머를 함께 제어하는 뷰 컨트롤러입니다.
/// </summary>
public sealed partial class TutorialOverlayView : MonoBehaviour, IPointerClickHandler, ICanvasRaycastFilter
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
    [SerializeField] private TMP_Text tapHintLabel;
    [SerializeField] private TutorialDimmer dimmer;
    [SerializeField] private AnchorSlot[] anchorSlots;
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private readonly Dictionary<TutorialAnchor, RectTransform> _anchorLookup = new();
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private TutorialStepConfig _currentConfig;
    private RectTransform _currentHighlight;
    private Graphic _panelGraphic;
    private Graphic _overlayGraphic;
    private bool _allowTap;
    private bool _gateOthers;
    private readonly List<RectTransform> _secondaryPassRects = new();

    private void Awake()
    {
        if (rootGroup == null) rootGroup = GetComponent<CanvasGroup>();
        if (panel == null) panel = transform as RectTransform;
        _panelGraphic = panel != null ? panel.GetComponent<Graphic>() : null;
        _overlayGraphic = GetComponent<Graphic>();
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

        // 오버레이에서 월드 프록시 코디네이터가 없으면 보강
        if (GetComponent<HighlightProxyCoordinator>() == null)
        {
            gameObject.AddComponent<HighlightProxyCoordinator>();
        }

        SetVisible(false);
        D($"Awake: anchors={anchorSlots?.Length ?? 0}");
    }

    private void OnEnable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null)
        {
            svc.OnStepVisualChanged += HandleStepVisualChanged;
            HandleStepVisualChanged(svc.CurrentConfig, svc.CurrentHighlight);
            D($"OnEnable: svc active={svc.IsActive}, step={svc.CurrentStep}, canTap={svc.CanAdvanceViaOverlay}");
        }
        else
        {
            D("OnEnable: ITutorialService not found");
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

        if (config == null)
        {
            SetVisible(false);
            _secondaryPassRects.Clear();
            dimmer?.Apply(null, null, null);
            return;
        }

        SetVisible(true);
        if (messageLabel != null)
        {
            messageLabel.text = string.IsNullOrEmpty(config.Message)
                ? string.Empty
                : config.Message;
        }
        var svc = ServiceRegistry.Get<ITutorialService>();
        // 메인 하이라이트를 서비스 상태에서 재해석(프록시/동적 타깃 반영)
        var main = ResolveMainHighlight(config, highlight);
        ApplyAnchor(config, main);
        PopulateSecondaryRects(config, svc);
        dimmer?.Apply(config, main, _secondaryPassRects);
        _allowTap = svc?.CanAdvanceViaOverlay ?? false;
        _gateOthers = config.InteractionGate == TutorialInteractionGate.BlockOthers
                      || config.InteractionGate == TutorialInteractionGate.Exclusive;
        UpdateRaycastState();
        if (tapHintLabel != null)
        {
            tapHintLabel.gameObject.SetActive(_allowTap);
        }
        D($"StepChanged: step={config.Step} req={config.RequiredAction} allowTap={config.AllowTapToContinue} svcCanTap={svc?.CanAdvanceViaOverlay ?? false} gate={config.InteractionGate} highlightId='{config.HighlightTargetId}' hasHighlight={(main!=null)}");

        // 프록시/타깃 등록 타이밍을 고려해 한 프레임 뒤 재적용(안정화)
        if (_delayedRefresh != null) StopCoroutine(_delayedRefresh);
        _delayedRefresh = StartCoroutine(DelayedReapply());
    }

    private RectTransform ResolveMainHighlight(TutorialStepConfig config, RectTransform fallback)
    {
        RectTransform resolved = null;
        if (config != null && !string.IsNullOrEmpty(config.HighlightTargetId))
        {
            try { resolved = ServiceRegistry.Get<ITutorialService>()?.GetTargetRect(config.HighlightTargetId); } catch { resolved = null; }
        }
        if (resolved == null) resolved = fallback;
        _currentHighlight = resolved;
        return resolved;
    }

    private Coroutine _delayedRefresh;
    private IEnumerator DelayedReapply()
    {
        yield return null; // 한 프레임 대기(프록시 바인딩 완료 대기)
        var cfg = _currentConfig;
        if (cfg == null) yield break;
        var svc = ServiceRegistry.Get<ITutorialService>();
        var main = ResolveMainHighlight(cfg, _currentHighlight);
        PopulateSecondaryRects(cfg, svc);
        dimmer?.Apply(cfg, main, _secondaryPassRects);
        UpdateRaycastState();
        if (tapHintLabel != null) tapHintLabel.gameObject.SetActive(_allowTap);
        _delayedRefresh = null;
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
        if (!visible)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
            if (_panelGraphic != null) _panelGraphic.raycastTarget = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc == null || !svc.CanAdvanceViaOverlay)
        {
            D($"OnPointerClick IGNORED at {eventData.position}: canTap={svc?.CanAdvanceViaOverlay ?? false}");
            return;
        }
        var ok = svc.TryAdvanceOverlayStep();
        D($"OnPointerClick at {eventData.position}: advanceResult={ok}");
    }

    private void UpdateRaycastState()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = _gateOthers || _allowTap;
            rootGroup.interactable = _allowTap;
        }
        if (_panelGraphic == null && panel != null)
        {
            _panelGraphic = panel.GetComponent<Graphic>();
        }
        if (_panelGraphic != null)
        {
            _panelGraphic.raycastTarget = false;
        }
        if (_overlayGraphic != null)
        {
            _overlayGraphic.raycastTarget = _gateOthers || _allowTap;
        }
        D($"RaycastState: allowTap={_allowTap} gateOthers={_gateOthers} root.blocks={rootGroup?.blocksRaycasts} interactable={rootGroup?.interactable} overlay.raycast={_overlayGraphic?.raycastTarget}");
    }

    private void PopulateSecondaryRects(TutorialStepConfig config, ITutorialService svc)
    {
        _secondaryPassRects.Clear();
        if (config?.SecondaryHighlightIds == null || svc == null)
        {
            return;
        }

        foreach (var id in config.SecondaryHighlightIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var rect = svc.GetTargetRect(id);
            if (rect != null)
            {
                _secondaryPassRects.Add(rect);
            }
        }
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (_allowTap)
        {
            return true;
        }

        if (!_gateOthers)
        {
            return false;
        }

        if (IsPointInside(_currentHighlight, sp, eventCamera))
        {
            return false;
        }

        for (int i = 0; i < _secondaryPassRects.Count; i++)
        {
            if (IsPointInside(_secondaryPassRects[i], sp, eventCamera))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPointInside(RectTransform rectTransform, Vector2 screenPoint, Camera eventCamera)
    {
        if (rectTransform == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, eventCamera);
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Local helper for uniform debug prefix
#endif
partial class TutorialOverlayView
{
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void D(string msg)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"[TutorialOverlayView] {msg}", this);
    }
}

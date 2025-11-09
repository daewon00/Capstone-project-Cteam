using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 단계의 하이라이트 대상들을 오버레이 좌표계로 투영하기 위해
/// WorldRectProxy들을 생성/관리하고, 해당 타깃의 FocusRect를 프록시로 연결합니다.
/// 배틀 카드와 같은 월드/카메라 기반 타깃에서 정확한 화면 사각을 제공합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HighlightProxyCoordinator : MonoBehaviour
{
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private float padding = 12f;
    [SerializeField] private bool enableLogs = true;

    private readonly List<WorldRectProxy> _proxies = new();
    private readonly Dictionary<TutorialTarget, RectTransform> _originalFocus = new();
    private readonly List<TutorialTarget> _syntheticTargets = new();

    private void Awake()
    {
        if (overlayRoot == null) overlayRoot = transform as RectTransform;
    }

    private void OnEnable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null)
        {
            svc.OnStepVisualChanged += HandleStepVisualChanged;
        }
    }

    private void OnDisable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null)
        {
            svc.OnStepVisualChanged -= HandleStepVisualChanged;
        }
        ClearProxies();
    }

    private bool _applying;

    private void HandleStepVisualChanged(TutorialStepConfig config, RectTransform _)
    {
        if (_applying) return;
        _applying = true;
        try
        {
            ClearProxies();
            if (config == null)
            {
                return;
            }

            // 배틀 카드 단계 등 실제 월드 오브젝트를 강조할 필요가 있는 경우에만 프록시 적용
            if (config.RequiredAction != TutorialRequiredActionType.CardPlay)
            {
                return;
            }

            // 손패 전체 강조 또는 카드 강조
            if (string.Equals(config.HighlightTargetId, "hand-area", StringComparison.OrdinalIgnoreCase))
            {
                ApplyHandArea();
            }
            else
            {
                ApplyForId(config.HighlightTargetId);
            }
            if (config.SecondaryHighlightIds != null)
            {
                foreach (var id in config.SecondaryHighlightIds)
                {
                    ApplyForId(id);
                }
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void ApplyHandArea()
    {
        if (overlayRoot == null) return;
        // 카드 타깃들을 수집(card-hand-0..9)
        var srcs = new List<(Transform tf, Renderer rend, Collider col, RectTransform ui)>();
        for (int i = 0; i < 10; i++)
        {
            var id = $"card-hand-{i}";
            var t = FindTargetById(id);
            if (t == null) continue;
            var rend = t.GetComponentInChildren<MeshRenderer>(true);
            var col = t.GetComponentInChildren<Collider>(true);
            RectTransform ui = null; try { ui = t.FocusRect; } catch { }
            srcs.Add((t.transform, rend, col, ui));
        }
        if (srcs.Count == 0)
        {
            D("ApplyHandArea: no card-hand-* targets found");
            return;
        }

        Camera cam = Camera.main;
        // 프록시 GO
        var go = new GameObject("WorldProxy:hand-area", typeof(RectTransform));
        var rt = go.transform as RectTransform; rt.SetParent(overlayRoot, false);
        var proxy = go.AddComponent<MultiWorldRectProxy>();
        proxy.Bind(overlayRoot, cam, padding);
        foreach (var s in srcs) proxy.AddSource(s.tf, s.rend, s.col, s.ui);

        // 합성 TutorialTarget 생성 및 등록
        var tgtGo = new GameObject("SyntheticTarget:hand-area");
        tgtGo.transform.SetParent(overlayRoot, false);
        var tgt = tgtGo.AddComponent<TutorialTarget>();
        tgt.SetId("hand-area");
        tgt.SetFocusRect(rt);
        _syntheticTargets.Add(tgt);
        D($"HandArea proxy & synthetic target created. sources={srcs.Count}");
    }

    private void ApplyForId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var target = FindTargetById(id);
        if (target == null)
        {
            D($"Target not found for id='{id}'");
            return;
        }

        // 원본 소스 추출: MeshRenderer → Collider → UI Rect(FocusRect)
        var rend = target.GetComponentInChildren<MeshRenderer>(true);
        var coll = target.GetComponentInChildren<Collider>(true);
        RectTransform uiRect = null;
        try { uiRect = target.FocusRect; } catch { }

        // 투영 카메라: UI Rect가 있으면 해당 Canvas.worldCamera, 아니면 MainCamera
        Camera cam = null;
        if (uiRect != null)
        {
            var cv = uiRect.GetComponentInParent<Canvas>();
            if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = cv.worldCamera;
            }
        }
        if (cam == null) cam = Camera.main;

        // 프록시 생성
        var go = new GameObject($"WorldProxy:{id}", typeof(RectTransform));
        var rt = go.transform as RectTransform;
        rt.SetParent(overlayRoot, false);
        var proxy = go.AddComponent<WorldRectProxy>();
        proxy.Bind(overlayRoot, target.transform, rend, coll, uiRect, cam, padding);
        _proxies.Add(proxy);

        // 타깃의 FocusRect를 프록시로 대체 연결(복구를 위해 원본 저장)
        if (!_originalFocus.ContainsKey(target))
        {
            _originalFocus[target] = target.FocusRect;
        }
        target.SetFocusRect(rt);

        D($"Proxy bound id='{id}' src=({(rend!=null?"Renderer":"")}{(coll!=null?" Collider":"")}{(uiRect!=null?" UI":"")}) cam={(cam!=null?cam.name:"<null>")}");
    }

    private void ClearProxies()
    {
        // FocusRect 복구
        foreach (var kv in _originalFocus)
        {
            var t = kv.Key;
            if (t != null)
            {
                try { t.SetFocusRect(kv.Value); } catch {}
            }
        }
        _originalFocus.Clear();

        // 프록시 파괴
        for (int i = 0; i < _proxies.Count; i++)
        {
            if (_proxies[i] != null)
            {
                try { Destroy(_proxies[i].gameObject); } catch {}
            }
        }
        _proxies.Clear();

        // 합성 타깃 파괴(서비스 등록 해제는 OnDisable에서 처리됨)
        for (int i = 0; i < _syntheticTargets.Count; i++)
        {
            if (_syntheticTargets[i] != null)
            {
                try { Destroy(_syntheticTargets[i].gameObject); } catch { }
            }
        }
        _syntheticTargets.Clear();
    }

    private static TutorialTarget FindTargetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var all = GameObject.FindObjectsOfType<TutorialTarget>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t != null && string.Equals(t.TargetId, id, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }
        return null;
    }

    private void D(string msg)
    {
        if (!enableLogs) return;
        Debug.Log($"[HighlightProxyCoordinator] {msg}", this);
    }
}

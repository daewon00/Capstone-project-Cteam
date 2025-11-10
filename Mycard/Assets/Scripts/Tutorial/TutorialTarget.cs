using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼에서 강조하거나 입력을 허용할 UI 요소에 부착하는 식별자입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId = string.Empty;
    [SerializeField] private RectTransform explicitRect;
    [SerializeField] private string[] aliasIds = Array.Empty<string>();

    public string TargetId => targetId;
    public RectTransform FocusRect => explicitRect != null ? explicitRect : transform as RectTransform;
    public IReadOnlyList<string> Aliases => aliasIds;

    private void OnEnable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        svc?.RegisterTarget(this);
    }

    private void OnDisable()
    {
        var svc = ServiceRegistry.Get<ITutorialService>();
        svc?.UnregisterTarget(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (explicitRect == null)
        {
            explicitRect = GetComponent<RectTransform>();
        }
    }
#endif

    public void SetId(string id)
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[TutorialTarget] SetId: {targetId} -> {id} ({gameObject.name})", this);
        #endif
        if (string.Equals(targetId, id, System.StringComparison.Ordinal))
        {
            return;
        }

        // 서비스에 등록된 키를 갱신하기 위해 기존 키로 먼저 해제한 뒤, 새 키로 재등록합니다.
        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null && !string.IsNullOrEmpty(targetId))
        {
            // 현재 targetId는 '이전' 키이므로, 변경 전에 해제하면 정확히 제거됩니다.
            svc.UnregisterTarget(this);
        }

        targetId = id;

        if (svc != null && isActiveAndEnabled)
        {
            svc.RegisterTarget(this);
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[TutorialTarget] Re-registered with new id: {targetId}", this);
        #endif
        }
    }

    public void SetFocusRect(RectTransform rect)
    {
        if (rect == null) return;
        if (explicitRect == rect) return;
        explicitRect = rect;
        // FocusRect 변경은 서비스 재등록 없이도 Dimmer가 최신 Rect를 참조합니다.
        // (루프 방지: RegisterTarget를 호출하지 않습니다.)
    }

    public void AddAlias(string aliasId)
    {
        if (string.IsNullOrEmpty(aliasId)) return;
        if (Array.Exists(aliasIds, id => string.Equals(id, aliasId, System.StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Array.Resize(ref aliasIds, aliasIds.Length + 1);
        aliasIds[^1] = aliasId;

        var svc = ServiceRegistry.Get<ITutorialService>();
        if (svc != null && isActiveAndEnabled)
        {
            svc.RegisterTarget(this);
        }
    }
}

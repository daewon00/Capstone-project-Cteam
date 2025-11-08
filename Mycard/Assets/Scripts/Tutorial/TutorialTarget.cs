using UnityEngine;

/// <summary>
/// 튜토리얼에서 강조하거나 입력을 허용할 UI 요소에 부착하는 식별자입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId = string.Empty;
    [SerializeField] private RectTransform explicitRect;

    public string TargetId => targetId;
    public RectTransform FocusRect => explicitRect != null ? explicitRect : transform as RectTransform;

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
        }
    }

    public void SetFocusRect(RectTransform rect)
    {
        // 씬에서 이미 명시적으로 지정된 포커스 Rect가 있다면 건드리지 않습니다.
        if (explicitRect != null)
        {
            return;
        }
        if (rect == null) return;
        explicitRect = rect;
        var svc = ServiceRegistry.Get<ITutorialService>();
        svc?.RegisterTarget(this);
    }
}

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
        targetId = id;
    }

    public void SetFocusRect(RectTransform rect)
    {
        explicitRect = rect;
    }
}

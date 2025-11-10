using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼에서 필드 드래그 영역이 강조될 때 색상을 일시적으로 부여합니다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class FieldGestureOverlayHighlighter : MonoBehaviour
{
    [SerializeField] private Image overlayImage;
    [SerializeField] private string targetId = "field-swipe-zone";
    [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 1f, 0.22f);
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0f);

    private ITutorialService _tutorial;

    private void Awake()
    {
        if (overlayImage == null)
        {
            overlayImage = GetComponent<Image>();
        }

        if (overlayImage != null)
        {
            overlayImage.color = inactiveColor;
        }
    }

    private void OnEnable()
    {
        _tutorial = ServiceRegistry.Get<ITutorialService>();
        if (_tutorial != null)
        {
            _tutorial.OnStepVisualChanged += HandleStepVisualChanged;
            HandleStepVisualChanged(_tutorial.CurrentConfig, _tutorial.CurrentHighlight);
        }
    }

    private void OnDisable()
    {
        if (_tutorial != null)
        {
            _tutorial.OnStepVisualChanged -= HandleStepVisualChanged;
        }
        SetColor(inactiveColor);
    }

    private void HandleStepVisualChanged(TutorialStepConfig config, RectTransform _)
    {
        bool isActive = config != null
                        && !string.IsNullOrEmpty(config.HighlightTargetId)
                        && string.Equals(config.HighlightTargetId, targetId, StringComparison.OrdinalIgnoreCase);
        SetColor(isActive ? activeColor : inactiveColor);
    }

    private void SetColor(Color color)
    {
        if (overlayImage != null)
        {
            overlayImage.color = color;
        }
    }
}

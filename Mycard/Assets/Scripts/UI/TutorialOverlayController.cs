using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼 단계에 맞춰 간단한 안내 문구를 표시합니다.
/// </summary>
public class TutorialOverlayController : MonoBehaviour
{
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private TMP_Text messageLabel;

    [TextArea] [SerializeField] private string companionSelectionCopy = "동료를 선택해주세요.";
    [TextArea] [SerializeField] private string firstBattleCopy = "첫 전투로 바로 진입합니다!";
    [TextArea] [SerializeField] private string mapMoveCopy = "다음 노드를 선택해 앞으로 나아가세요.";

    private ITutorialService _tutorialService;

    private void Awake()
    {
        _tutorialService = ServiceRegistry.Get<ITutorialService>();
        if (_tutorialService != null)
        {
            _tutorialService.OnStepChanged += HandleStepChanged;
            HandleStepChanged(_tutorialService.CurrentStep);
        }
        else
        {
            SetVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (_tutorialService != null)
        {
            _tutorialService.OnStepChanged -= HandleStepChanged;
        }
    }

    private void HandleStepChanged(TutorialStep step)
    {
        if (_tutorialService == null)
        {
            SetVisible(false);
            return;
        }

        if (!_tutorialService.IsActive)
        {
            SetVisible(false);
            return;
        }

        string copy = step switch
        {
            TutorialStep.CompanionSelection => companionSelectionCopy,
            TutorialStep.FirstBattlePending => firstBattleCopy,
            TutorialStep.MapMovePending => mapMoveCopy,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(copy))
        {
            SetVisible(false);
            return;
        }

        if (messageLabel != null)
        {
            messageLabel.text = copy;
        }

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (rootGroup == null)
        {
            return;
        }

        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.interactable = visible;
        rootGroup.blocksRaycasts = visible;
    }
}

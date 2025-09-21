using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 진행 중인 런을 삭제하기 전에 확인을 받기 위한 간단한 모달 컨트롤러입니다.
/// </summary>
public class RunResetConfirmModal : MonoBehaviour
{
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Selectable initialSelection;

    private Action _onConfirm;
    private Action _onCancel;

    void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    /// <summary>
    /// 모달을 표시하고 버튼 콜백을 연결합니다.
    /// </summary>
    public void Show(string message, Action onConfirm, Action onCancel)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (messageLabel != null)
        {
            messageLabel.text = message ?? string.Empty;
        }

        gameObject.SetActive(true);
        FocusInitialSelectable();
    }

    /// <summary>
    /// 배경 터치/ESC에 연동하기 위한 취소 트리거입니다.
    /// </summary>
    public void Cancel()
    {
        OnCancelClicked();
    }

    private void OnConfirmClicked()
    {
        try
        {
            _onConfirm?.Invoke();
        }
        finally
        {
            Close();
        }
    }

    private void OnCancelClicked()
    {
        try
        {
            _onCancel?.Invoke();
        }
        finally
        {
            Close();
        }
    }

    private void Close()
    {
        _onConfirm = null;
        _onCancel = null;
        Destroy(gameObject);
    }

    private void FocusInitialSelectable()
    {
        var target = initialSelection != null
            ? initialSelection.gameObject
            : confirmButton != null
                ? confirmButton.gameObject
                : cancelButton != null
                    ? cancelButton.gameObject
                    : null;

        if (target != null)
        {
            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(target);
            }
        }
    }
}

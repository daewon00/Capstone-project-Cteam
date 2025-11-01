using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save;

/// <summary>
/// 2단계: 선택된 카드의 강화 전/후 미리보기를 보여주고 확인/취소를 받는 다이얼로그.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeConfirmDialogController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private CardUpgradePreviewPanel previewPanel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    private CardRuntimeState _currentState;
    private CardScriptableObject _currentSo;
    private Action<CardRuntimeState> _onConfirm;
    private Action _onCancel;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        WireButtons();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    private void WireButtons()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancel);
        if (closeButton != null) closeButton.onClick.AddListener(HandleCancel);
    }

    private void UnwireButtons()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
        if (closeButton != null) closeButton.onClick.RemoveListener(HandleCancel);
    }

    /// <summary>
    /// 다이얼로그를 열어 전/후 미리보기를 표시하고 확인 콜백을 대기합니다.
    /// </summary>
    public void Show(CardRuntimeState state, CardScriptableObject so, Action<CardRuntimeState> onConfirm, Action onCancel = null)
    {
        _currentState = state;
        _currentSo = so;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (titleText != null && string.IsNullOrEmpty(titleText.text))
            titleText.text = "강화 확인";

        if (cardNameText != null)
        {
            string name = so != null ? so.GetDisplayName(false) : (state?.CardId ?? string.Empty);
            cardNameText.text = name ?? string.Empty;
        }

        if (previewPanel != null)
        {
            if (state != null && so != null)
                previewPanel.Show(so, state);
            else
                previewPanel.Clear();
        }

        if (confirmButton != null)
            confirmButton.interactable = (state != null && so != null);

        if (panelRoot != null)
        {
            bool before = panelRoot.activeSelf;
            panelRoot.SetActive(true);
            Debug.Log($"[UpgradeConfirmDialog] Show (active: {before} -> {panelRoot.activeSelf}) state={(state!=null?state.InstanceId:"null")} card={(so!=null?so.CardId:"null")}", this);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        previewPanel?.Clear();
        _currentState = null;
        _currentSo = null;
        _onConfirm = null;
        _onCancel = null;
    }

    public void HideImmediate()
    {
        Hide();
    }

    private void HandleConfirm()
    {
        var handler = _onConfirm;
        var selected = _currentState;
        Hide();
        try { handler?.Invoke(selected); } catch { }
    }

    private void HandleCancel()
    {
        var handler = _onCancel;
        Hide();
        try { handler?.Invoke(); } catch { }
    }
}


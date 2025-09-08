using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardOverlayUI : MonoBehaviour, IRewardUI
{
    [Header("UI Elements")]
    [SerializeField] private GameObject rootPanel;          // 전체 오버레이 루트
    [SerializeField] private TextMeshProUGUI goldText;       // 골드 보상 표기
    [SerializeField] private Button closeButton;             // 확인/닫기 버튼

    private Action _onClosedCallback;

    private void Awake()
    {
        // 시작 시 비활성화 상태 권장
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    public void Show(RewardContainer rewards, Action onClosed)
    {
        _onClosedCallback = onClosed;

        if (rewards == null)
        {
            Debug.LogWarning("[RewardOverlayUI] rewards is null");
            goldText?.SetText(string.Empty);
        }
        else
        {
            // 간단한 골드 보상 표시
            var gold = rewards.Items != null ? rewards.Items.Find(i => i != null && i.Type == "Gold") : null;
            if (goldText != null)
            {
                goldText.text = gold != null ? $"골드 +{gold.Amount}" : string.Empty;
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (rootPanel != null) rootPanel.SetActive(true);
    }

    private void Close()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        _onClosedCallback?.Invoke();
        _onClosedCallback = null;
    }
}


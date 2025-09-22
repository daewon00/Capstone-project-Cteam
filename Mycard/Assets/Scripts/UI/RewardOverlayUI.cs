using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 보상(골드/카드)을 표시하고 선택된 카드를 덱에 추가하는 오버레이 UI입니다.
/// </summary>
public class RewardOverlayUI : MonoBehaviour, IRewardUI
{
    [Header("UI Elements")]
    [SerializeField] private GameObject rootPanel;          // 전체 오버레이 루트
    [SerializeField] private TextMeshProUGUI goldText;       // 골드 보상 표기
    [SerializeField] private Button closeButton;             // 확인/닫기 버튼

    [Header("Card Reward Elements")]
    [SerializeField] private GameObject cardChoiceArea;      // 카드 슬롯 부모
    [SerializeField] private CardRewardSlot cardSlotPrefab;  // 카드 슬롯 프리팹

    private Action _onClosedCallback;
    private IDeckService _deckService;

    private void Awake()
    {
        // 시작 시 비활성화 상태 권장
        if (rootPanel != null) rootPanel.SetActive(false);
        _deckService = ServiceRegistry.Get<IDeckService>();
    }

    /// <summary>
    /// 보상 컨테이너를 오버레이에 표시하고 닫힘 콜백을 설정합니다.
    /// </summary>
    public void Show(RewardContainer rewards, Action onClosed)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!enabled)
        {
            enabled = true;
        }

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

        // 카드 선택지 표시
        if (cardChoiceArea != null)
        {
            foreach (Transform t in cardChoiceArea.transform) Destroy(t.gameObject);
            bool hasCards = rewards != null && rewards.SelectableCards != null && rewards.SelectableCards.Count > 0;
            cardChoiceArea.SetActive(hasCards);
            if (hasCards && cardSlotPrefab != null)
            {
                foreach (var option in rewards.SelectableCards)
                {
                    var slot = Instantiate(cardSlotPrefab, cardChoiceArea.transform);
                    slot.Init(option);
                    slot.OnCardSelected += HandleCardSelection;
                }
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

    private void HandleCardSelection(RewardCardOption selected)
    {
        if (_deckService == null)
        {
            Debug.LogWarning("[RewardOverlayUI] IDeckService not available; cannot add card.");
        }
        else if (selected != null && !string.IsNullOrEmpty(selected.CardId))
        {
            _deckService.AddCardToDeckById(selected.CardId, selected.IsUpgraded);
        }
        Close();
    }
}

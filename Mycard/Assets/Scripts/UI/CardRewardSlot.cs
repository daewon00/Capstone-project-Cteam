using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Save;

/// <summary>
/// 카드 보상 선택지를 표시하고 선택 이벤트를 발생시키는 UI 슬롯입니다.
/// </summary>
public class CardRewardSlot : MonoBehaviour
{
    [Header("Card Display")]
    [SerializeField] private CardDisplay cardDisplay;
    [SerializeField] private CardDisplay cardDisplayPrefab;
    [SerializeField] private RectTransform cardDisplayRoot;

    [SerializeField] private Image cardArt;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    [SerializeField] private Button selectButton;

    private RewardCardOption _cardOption;
    private Color _defaultNameColor = Color.white;
    private bool _hasDefaultNameColor;

    public event Action<RewardCardOption> OnCardSelected;

    /// <summary>
    /// 카드 보상 옵션을 슬롯에 바인딩하고 버튼을 설정합니다.
    /// </summary>
    public void Init(RewardCardOption option)
    {
        _cardOption = option;
        var catalog = ServiceRegistry.Get<ICardCatalog>();
        var data = catalog?.GetCardData(option.CardId);
        bool upgraded = option != null && option.IsUpgraded;
        var previewState = CreatePreviewState(option);

        EnsureCardDisplay();

        _tooltipTitle = data != null ? data.GetDisplayName(upgraded) : (option != null && option.IsUpgraded ? $"{option.CardId}+" : option?.CardId ?? string.Empty);
        _tooltipDescription = data != null ? data.actionDescription : string.Empty;

        if (cardDisplay != null)
        {
            if (data != null || previewState != null)
            {
                cardDisplay.Bind(data, previewState);
            }
            else
            {
                cardDisplay.Clear();
            }

            if (cardName != null) cardName.text = string.Empty;
            if (cardDescription != null) cardDescription.text = string.Empty;
            if (cardArt != null)
            {
                cardArt.sprite = null;
                cardArt.enabled = false;
            }

            SetLegacyElementsActive(false);
            EnsureTooltipComponents();
        }
        else
        {
            SetLegacyElementsActive(true);
        }

        if (cardName != null && !_hasDefaultNameColor)
        {
            _defaultNameColor = cardName.color;
            _hasDefaultNameColor = true;
        }

        if (cardDisplay == null)
        {
            if (cardName != null)
            {
                cardName.text = _tooltipTitle;
                cardName.color = upgraded ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }

            if (cardDescription != null)
                cardDescription.text = _tooltipDescription;

            if (cardArt != null)
                cardArt.sprite = data != null ? data.characterSprite : null;

            EnsureTriggerForLegacy();
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => OnCardSelected?.Invoke(_cardOption));
        }

    }

    private void EnsureCardDisplay()
    {
        if (cardDisplay != null)
            return;

        if (cardDisplayPrefab == null || cardDisplayRoot == null)
            return;

        var instance = Instantiate(cardDisplayPrefab, cardDisplayRoot);
        cardDisplay = instance;

        var rect = instance.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        cardDisplay.gameObject.SetActive(true);
    }

    private void SetLegacyElementsActive(bool active)
    {
        if (cardName != null)
            cardName.gameObject.SetActive(active);

        if (cardDescription != null)
            cardDescription.gameObject.SetActive(active);

        if (cardArt != null)
        {
            cardArt.gameObject.SetActive(active);
            cardArt.enabled = active;
        }
    }

    private static CardRuntimeState CreatePreviewState(RewardCardOption option)
    {
        if (option == null || string.IsNullOrEmpty(option.CardId))
            return null;

        var state = new CardRuntimeState
        {
            InstanceId = option.CardId,
            CardId = option.CardId,
            ModifiersJson = string.Empty
        };
        state.SetUpgraded(option.IsUpgraded);
        return state;
    }

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipAnchor;
    private CardTooltipTriggerUI _trigger;
    private string _tooltipTitle;
    private string _tooltipDescription;

    private void EnsureTooltipComponents()
    {
        if (cardDisplay == null)
            return;

        var source = cardDisplay.GetComponent<CardDisplayTooltipSource>();
        if (source == null)
            source = cardDisplay.gameObject.AddComponent<CardDisplayTooltipSource>();
        source.SetDisplay(cardDisplay);
        source.SetUseHandOffsets(false);

        var trigger = cardDisplay.GetComponent<CardTooltipTriggerUI>();
        if (trigger == null)
            trigger = cardDisplay.gameObject.AddComponent<CardTooltipTriggerUI>();
        trigger.SetSource(source);
    }

    private void EnsureTriggerForLegacy()
    {
        if (cardDisplay != null)
            return;

        if (_trigger == null)
        {
            _trigger = gameObject.AddComponent<CardTooltipTriggerUI>();
            _trigger.SetSource(new LegacyTooltipSource(this));
        }
    }

    private class LegacyTooltipSource : ICardTooltipSource
    {
        private readonly CardRewardSlot _slot;
        public LegacyTooltipSource(CardRewardSlot slot) => _slot = slot;
        public CardTooltipData GetTooltipData() => new CardTooltipData(_slot._tooltipTitle ?? string.Empty, _slot._tooltipDescription ?? string.Empty);
        public Vector3 GetTooltipAnchorWorldPos()
        {
            if (_slot.tooltipAnchor != null)
                return _slot.tooltipAnchor.position;
            return _slot.transform.position;
        }
        public bool ShouldUseHandOffset => false;
        public bool IsTooltipValid => _slot.isActiveAndEnabled;
    }
}

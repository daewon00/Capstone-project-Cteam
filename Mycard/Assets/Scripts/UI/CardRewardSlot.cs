using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 보상 선택지를 표시하고 선택 이벤트를 발생시키는 UI 슬롯입니다.
/// </summary>
public class CardRewardSlot : MonoBehaviour
{
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

        if (cardName != null && !_hasDefaultNameColor)
        {
            _defaultNameColor = cardName.color;
            _hasDefaultNameColor = true;
        }

        if (data != null)
        {
            if (cardName != null)
            {
                cardName.text = data.GetDisplayName(upgraded);
                cardName.color = upgraded && data.UpgradeEnabled ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }
            if (cardDescription != null) cardDescription.text = data.actionDescription;
            if (cardArt != null) cardArt.sprite = data.characterSprite;
        }
        else
        {
            if (cardName != null)
            {
                string fallbackName = upgraded ? $"{option.CardId}+" : option.CardId;
                cardName.text = fallbackName;
                cardName.color = upgraded ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }
            if (cardDescription != null) cardDescription.text = string.Empty;
            if (cardArt != null) cardArt.sprite = null;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => OnCardSelected?.Invoke(_cardOption));
        }
    }
}

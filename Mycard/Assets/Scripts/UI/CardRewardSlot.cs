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

    public event Action<RewardCardOption> OnCardSelected;

    /// <summary>
    /// 카드 보상 옵션을 슬롯에 바인딩하고 버튼을 설정합니다.
    /// </summary>
    public void Init(RewardCardOption option)
    {
        _cardOption = option;
        var catalog = ServiceRegistry.Get<ICardCatalog>();
        var data = catalog?.GetCardData(option.CardId);
        if (data != null)
        {
            if (cardName != null) cardName.text = data.cardName;
            if (cardDescription != null) cardDescription.text = data.actionDescription;
            if (cardArt != null) cardArt.sprite = data.characterSprite;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => OnCardSelected?.Invoke(_cardOption));
        }
    }
}


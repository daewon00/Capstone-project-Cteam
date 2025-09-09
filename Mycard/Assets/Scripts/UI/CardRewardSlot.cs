using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRewardSlot : MonoBehaviour
{
    [SerializeField] private Image cardArt;
    [SerializeField] private TextMeshProUGUI cardName;
    [SerializeField] private TextMeshProUGUI cardDescription;
    [SerializeField] private Button selectButton;

    private RewardCardOption _cardOption;

    public event Action<RewardCardOption> OnCardSelected;

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


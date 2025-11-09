using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐러셀에서 선택된 동료의 세부 정보를 표시하고 덱/보너스 정보를 갱신합니다.
/// </summary>
public class CompanionDetailView : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Stat Rows")]
    [SerializeField] private GameObject hpRow;
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private GameObject goldRow;
    [SerializeField] private TMP_Text goldValueText;
    [SerializeField] private GameObject energyRow;
    [SerializeField] private TMP_Text energyValueText;

    [Header("Deck Preview")]
    [SerializeField] private Transform deckContentRoot;
    [SerializeField] private DeckCardItemView deckCardPrefab;

    private readonly List<DeckCardItemView> _cardPool = new();
    private ICardCatalog _cardCatalog;

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public RectTransform RectTransform => _rectTransform;
    public CanvasGroup CanvasGroup => canvasGroup;

    /// <summary>
    /// 전달된 동료 데이터를 기준으로 UI 요소를 갱신합니다.
    /// </summary>
    public void SetData(CompanionDefinition companion)
    {
        if (companion == null)
        {
            Clear();
            return;
        }

        EnsureCatalog();

        if (portraitImage != null)
        {
            portraitImage.sprite = companion.Portrait;
            portraitImage.enabled = companion.Portrait != null;
        }

        if (nameText != null)
        {
            nameText.text = companion.DisplayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = companion.Description;
        }

        UpdateStatRow(hpRow, hpValueText, companion.MaxHpBonus);
        UpdateStatRow(goldRow, goldValueText, companion.GoldBonus);
        UpdateStatRow(energyRow, energyValueText, companion.EnergyMaxBonus);

        RefreshDeckPreview(companion);
    }

    public void Clear()
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }

        if (nameText != null) nameText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;

        UpdateStatRow(hpRow, hpValueText, 0);
        UpdateStatRow(goldRow, goldValueText, 0);
        UpdateStatRow(energyRow, energyValueText, 0);

        foreach (var cardView in _cardPool)
        {
            if (cardView != null)
            {
                cardView.gameObject.SetActive(false);
                cardView.Clear();
            }
        }
    }

    public void SetAnchoredPosition(Vector2 position)
    {
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = position;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void RefreshDeckPreview(CompanionDefinition companion)
    {
        if (deckContentRoot == null || deckCardPrefab == null)
            return;

        foreach (var cardView in _cardPool)
        {
            if (cardView != null)
            {
                cardView.gameObject.SetActive(false);
                cardView.Clear();
            }
        }

        var previewEntries = CompanionDeckPreviewBuilder.BuildEntries(companion);
        if (previewEntries == null || previewEntries.Count == 0)
        {
            return;
        }

        int index = 0;
        foreach (var entry in previewEntries)
        {
            var cardView = GetOrCreateCardView(index++);
            if (cardView == null)
                continue;

            var cardData = LoadCardData(entry.CardId);
            cardView.gameObject.SetActive(true);
            cardView.Bind(cardData, cardState: null, groupedCount: entry.Count);
        }
    }

    private DeckCardItemView GetOrCreateCardView(int index)
    {
        while (_cardPool.Count <= index)
        {
            var instance = Instantiate(deckCardPrefab, deckContentRoot);
            _cardPool.Add(instance);
        }

        return _cardPool[index];
    }

    private void EnsureCatalog()
    {
        if (_cardCatalog != null)
            return;

        _cardCatalog = ServiceRegistry.Get<ICardCatalog>();
    }

    private CardScriptableObject LoadCardData(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return null;

        if (_cardCatalog != null && _cardCatalog.TryGetCardData(cardId, out var card))
        {
            return card;
        }

        return Resources.Load<CardScriptableObject>($"Cards/{cardId}");
    }

    private static void UpdateStatRow(GameObject row, TMP_Text valueText, int value)
    {
        if (row != null)
        {
            bool hasValue = value != 0;
            row.SetActive(hasValue);
        }

        if (valueText != null)
        {
            valueText.text = value > 0 ? $"+{value}" : value.ToString();
        }
    }
}

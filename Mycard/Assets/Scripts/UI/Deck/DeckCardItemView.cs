using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save; // CardRuntimeState

// 역할: 카드 한 장의 UI를 제어하고, 데이터를 받아서 내용을 채워넣는다.
[DisallowMultipleComponent]
public class DeckCardItemView : MonoBehaviour, ICardTooltipSource
{
    [Header("UI Elements")]
    [SerializeField] private CardDisplay cardDisplay;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image cardArtImage;
    [SerializeField] private Image cardBackgroundImage;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image effectIconImage;
    [SerializeField] private TMP_Text effectValueText;
    [SerializeField] private GameObject countBadge;   // (선택사항) 카드 매수 표시용
    [SerializeField] private TMP_Text countText;      // (선택사항)
    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipAnchor;

    // 바인딩에 사용된 최근 데이터(디버깅/툴팁용)
    public string BoundInstanceId { get; private set; }
    public string BoundCardId { get; private set; }

    private EffectIconDatabase _iconDatabase;
    private Color _defaultNameColor = Color.white;
    private bool _hasDefaultNameColor;

    private CardTooltipTriggerUI _tooltipTrigger;
    private string _tooltipTitle;
    private string _tooltipDescription;

    private void Awake()
    {
        EnsureTooltipTrigger();
    }

    public void Clear()
    {
        BoundInstanceId = null;
        BoundCardId = null;
        _tooltipTitle = string.Empty;
        _tooltipDescription = string.Empty;
        if (cardDisplay != null)
        {
            cardDisplay.Clear();
            if (countBadge != null) countBadge.SetActive(false);
            return;
        }
        if (cardNameText != null)
        {
            cardNameText.text = string.Empty;
            if (_hasDefaultNameColor)
                cardNameText.color = _defaultNameColor;
        }
        if (costText != null) costText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (attackText != null) attackText.text = string.Empty;
        if (healthText != null) healthText.text = string.Empty;
        if (cardArtImage != null) cardArtImage.sprite = null;
        if (cardBackgroundImage != null) cardBackgroundImage.sprite = null;
        HideEffectUI();
        if (countBadge != null) countBadge.SetActive(false);
    }

    /// <summary>
    /// 카드 데이터를 받아서 UI에 내용을 채워넣는 메인 함수.
    /// CardScriptableObject와 현재 런타임 상태를 기반으로 표시 요소를 채웁니다.
    /// </summary>
    public void Bind(CardScriptableObject cardSO, CardRuntimeState cardState, int? groupedCount = null)
    {
        BoundInstanceId = cardState?.InstanceId ?? string.Empty;
        BoundCardId = cardSO?.CardId ?? cardState?.CardId ?? string.Empty;

        if (cardDisplay != null)
        {
            cardDisplay.Bind(cardSO, cardState);
        }
        else
        {
        bool upgraded = cardState != null && cardState.IsUpgraded();

        if (cardSO != null)
        {
            if (!_hasDefaultNameColor && cardNameText != null)
            {
                _defaultNameColor = cardNameText.color;
                _hasDefaultNameColor = true;
            }
            if (cardNameText != null)
            {
                cardNameText.text = cardSO.GetDisplayName(upgraded);
                cardNameText.color = upgraded && cardSO.UpgradeEnabled ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }

            if (costText != null)
                costText.text = cardSO.GetManaCost(upgraded).ToString();

            if (descriptionText != null)
                descriptionText.text = cardSO.actionDescription;

            if (cardArtImage != null)
                cardArtImage.sprite = cardSO.characterSprite;

            if (cardBackgroundImage != null)
                cardBackgroundImage.sprite = cardSO.bgSprite;
        }
        else
        {
            if (cardNameText != null)
            {
                if (!_hasDefaultNameColor)
                {
                    _defaultNameColor = cardNameText.color;
                    _hasDefaultNameColor = true;
                }
                string fallbackName = upgraded ? $"{BoundCardId}+" : BoundCardId;
                cardNameText.text = fallbackName;
                cardNameText.color = upgraded ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }
            if (costText != null) costText.text = string.Empty;
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (cardArtImage != null) cardArtImage.sprite = null;
            if (cardBackgroundImage != null) cardBackgroundImage.sprite = null;
        }

        UpdateStatTexts(cardSO, upgraded);
        UpdateEffectIcon(cardSO);

        }

        // (선택사항) 카드 매수 표시 로직
        if (countBadge != null)
        {
            if (groupedCount.HasValue && groupedCount.Value > 1)
            {
                countBadge.SetActive(true);
                if (countText != null) countText.text = $"x{groupedCount.Value}";
            }
            else
            {
                countBadge.SetActive(false);
            }
        }

        bool upgradedFlag = cardState != null && cardState.IsUpgraded();
        _tooltipTitle = cardSO != null ? cardSO.GetDisplayName(upgradedFlag) : (upgradedFlag ? $"{BoundCardId}+" : BoundCardId);
        _tooltipDescription = cardSO != null ? cardSO.actionDescription : string.Empty;

        EnsureTooltipTrigger();
    }

    private void UpdateStatTexts(CardScriptableObject cardSO, bool upgraded)
    {
        int attack = cardSO != null ? cardSO.GetAttackPower(upgraded) : 0;
        int health = cardSO != null ? cardSO.GetHealth(upgraded) : 0;

        if (attackText != null)
            attackText.text = Mathf.Max(0, attack).ToString();

        if (healthText != null)
            healthText.text = Mathf.Max(0, health).ToString();
    }

    private void UpdateEffectIcon(CardScriptableObject cardSO)
    {
        if (effectIconImage == null && effectValueText == null)
            return;

        if (cardSO == null)
        {
            HideEffectUI();
            return;
        }

        var effects = cardSO.Effects;
        if (effects == null || effects.Count == 0)
        {
            HideEffectUI();
            return;
        }

        var primary = effects[0];
        if (primary == null || primary.Type == CardEffectType.None)
        {
            HideEffectUI();
            return;
        }

        EnsureIconDatabase();
        if (_iconDatabase == null)
        {
            HideEffectUI();
            return;
        }

        var icon = _iconDatabase.GetIcon(primary.Type);
        if (icon == null)
        {
            HideEffectUI();
            return;
        }

        int displayValue = primary.Value != 0 ? primary.Value : primary.Potency;

        if (effectIconImage != null)
        {
            effectIconImage.sprite = icon;
            effectIconImage.gameObject.SetActive(true);
        }

        if (effectValueText != null)
        {
            if (displayValue != 0)
            {
                effectValueText.text = displayValue > 0 ? $"+{displayValue}" : displayValue.ToString();
                effectValueText.gameObject.SetActive(true);
            }
            else
            {
                effectValueText.text = string.Empty;
                effectValueText.gameObject.SetActive(false);
            }
        }
    }

    private void HideEffectUI()
    {
        if (effectIconImage != null)
        {
            effectIconImage.sprite = null;
            effectIconImage.gameObject.SetActive(false);
        }

        if (effectValueText != null)
        {
            effectValueText.text = string.Empty;
            effectValueText.gameObject.SetActive(false);
        }
    }

    private void EnsureIconDatabase()
    {
        if (_iconDatabase != null)
            return;

        _iconDatabase = ServiceRegistry.Get<EffectIconDatabase>();
        if (_iconDatabase == null)
        {
            _iconDatabase = Resources.Load<EffectIconDatabase>("Cards/EffectIconDatabase");
            if (_iconDatabase != null)
            {
                ServiceRegistry.Register<EffectIconDatabase>(_iconDatabase);
            }
        }
    }

    public CardTooltipData GetTooltipData()
    {
        return new CardTooltipData(_tooltipTitle ?? string.Empty, _tooltipDescription ?? string.Empty);
    }

    public Vector3 GetTooltipAnchorWorldPos()
    {
        if (tooltipAnchor != null)
            return tooltipAnchor.position;
        if (cardDisplay != null)
            return cardDisplay.transform.position;
        return transform.position;
    }

    public bool ShouldUseHandOffset => true;

    public bool IsTooltipValid => isActiveAndEnabled && !string.IsNullOrEmpty(_tooltipTitle);

    private void EnsureTooltipTrigger()
    {
        if (_tooltipTrigger == null)
        {
            _tooltipTrigger = GetComponent<CardTooltipTriggerUI>();
            if (_tooltipTrigger == null)
                _tooltipTrigger = gameObject.AddComponent<CardTooltipTriggerUI>();
        }

        _tooltipTrigger.SetSource(this);
    }
}

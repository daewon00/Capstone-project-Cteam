using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save; // CardRuntimeState

// 역할: 카드 한 장의 UI를 제어하고, 데이터를 받아서 내용을 채워넣는다.
[DisallowMultipleComponent]
public class DeckCardItemView : MonoBehaviour
{
    [Header("UI Elements")]
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

    // 바인딩에 사용된 최근 데이터(디버깅/툴팁용)
    public string BoundInstanceId { get; private set; }
    public string BoundCardId { get; private set; }

    private EffectIconDatabase _iconDatabase;

    public void Clear()
    {
        BoundInstanceId = null;
        BoundCardId = null;
        if (cardNameText != null) cardNameText.text = string.Empty;
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

        if (cardSO != null)
        {
            if (cardNameText != null)
                cardNameText.text = cardSO.cardName;

            if (costText != null)
                costText.text = Mathf.Max(0, cardSO.manaCost).ToString();

            if (descriptionText != null)
                descriptionText.text = cardSO.actionDescription;

            if (cardArtImage != null)
                cardArtImage.sprite = cardSO.characterSprite;

            if (cardBackgroundImage != null)
                cardBackgroundImage.sprite = cardSO.bgSprite;
        }
        else
        {
            if (cardNameText != null) cardNameText.text = BoundCardId;
            if (costText != null) costText.text = string.Empty;
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (cardArtImage != null) cardArtImage.sprite = null;
            if (cardBackgroundImage != null) cardBackgroundImage.sprite = null;
        }

        UpdateStatTexts(cardSO);
        UpdateEffectIcon(cardSO);

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
    }

    private void UpdateStatTexts(CardScriptableObject cardSO)
    {
        int attack = cardSO != null ? cardSO.attackPower : 0;
        int health = cardSO != null ? cardSO.currentHealth : 0;

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
}


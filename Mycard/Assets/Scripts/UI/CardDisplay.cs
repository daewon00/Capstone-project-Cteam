using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save;

/// <summary>
/// Battle card visuals packaged for UI usage (deck view, rewards, etc).
/// </summary>
[DisallowMultipleComponent]
public sealed class CardDisplay : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Images")]
    [SerializeField] private Image characterArtImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frontOverlayImage;
    [SerializeField] private Image rarityEmblemImage;
    [SerializeField] private Image effectIconImage;
    [SerializeField] private Image manaIconImage;

    [Header("Effect Value")]
    [SerializeField] private TMP_Text effectValueText;

    [Header("Configuration")]
    [SerializeField] private EffectIconDatabase iconDatabaseOverride;
    [SerializeField] private CardVisualProfile visualProfileOverride;

    private EffectIconDatabase _iconDatabase;
    private CardVisualProfile _visualProfile;
    private Color _defaultNameColor = Color.white;
    private Color _defaultAttackColor = Color.white;
    private Color _defaultHealthColor = Color.white;
    private Color _defaultCostColor = Color.white;
    private bool _hasDefaultNameColor;
    private bool _hasDefaultAttackColor;
    private bool _hasDefaultHealthColor;
    private bool _hasDefaultCostColor;

    private void Awake()
    {
        CacheDefaultColors();
    }

    public void Clear()
    {
        CacheDefaultColors();

        if (cardNameText != null)
        {
            cardNameText.text = string.Empty;
            if (_hasDefaultNameColor) cardNameText.color = _defaultNameColor;
        }

        if (costText != null)
        {
            costText.text = string.Empty;
            if (_hasDefaultCostColor) costText.color = _defaultCostColor;
        }

        if (attackText != null)
        {
            attackText.text = string.Empty;
            if (_hasDefaultAttackColor) attackText.color = _defaultAttackColor;
        }

        if (healthText != null)
        {
            healthText.text = string.Empty;
            if (_hasDefaultHealthColor) healthText.color = _defaultHealthColor;
        }

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        if (characterArtImage != null)
            characterArtImage.sprite = null;

        if (backgroundImage != null)
            backgroundImage.sprite = null;

        if (frontOverlayImage != null)
            frontOverlayImage.sprite = null;

        if (rarityEmblemImage != null)
        {
            rarityEmblemImage.sprite = null;
            rarityEmblemImage.enabled = false;
        }

        if (effectIconImage != null)
        {
            effectIconImage.sprite = null;
            effectIconImage.enabled = false;
        }

        if (effectValueText != null)
            effectValueText.text = string.Empty;
    }

    public void Bind(CardScriptableObject cardSO, CardRuntimeState runtimeState)
    {
        CacheDefaultColors();

        bool upgraded = runtimeState != null && runtimeState.IsUpgraded();
        string fallbackId = cardSO != null ? cardSO.CardId : (runtimeState?.CardId ?? string.Empty);

        if (cardNameText != null)
        {
            if (cardSO != null)
            {
                cardNameText.text = cardSO.GetDisplayName(upgraded);
                cardNameText.color = upgraded && cardSO.UpgradeEnabled
                    ? CardScriptableObject.UpgradeNameColor
                    : _defaultNameColor;
            }
            else
            {
                string displayName = upgraded ? $"{fallbackId}+" : fallbackId;
                cardNameText.text = displayName;
                cardNameText.color = upgraded ? CardScriptableObject.UpgradeNameColor : _defaultNameColor;
            }
        }

        if (costText != null)
        {
            if (cardSO != null)
            {
                costText.text = Mathf.Max(0, cardSO.GetManaCost(upgraded)).ToString();
                if (_hasDefaultCostColor) costText.color = _defaultCostColor;
            }
            else
            {
                costText.text = string.Empty;
            }
        }

        if (descriptionText != null)
            descriptionText.text = cardSO != null ? cardSO.actionDescription : string.Empty;

        UpdateStats(cardSO, upgraded);
        ApplyArtwork(cardSO);
        ApplyFrontVisuals(cardSO, upgraded);
        UpdateEffectDisplay(cardSO);
    }

    private void UpdateStats(CardScriptableObject cardSO, bool upgraded)
    {
        int baseAttack = cardSO != null ? cardSO.GetAttackPower(upgraded) : 0;
        int baseHealth = cardSO != null ? cardSO.GetHealth(upgraded) : 0;

        if (attackText != null)
        {
            attackText.text = Mathf.Max(0, baseAttack).ToString();
            if (_hasDefaultAttackColor) attackText.color = _defaultAttackColor;
        }

        if (healthText != null)
        {
            healthText.text = Mathf.Max(0, baseHealth).ToString();
            if (_hasDefaultHealthColor) healthText.color = _defaultHealthColor;
        }
    }

    private void ApplyArtwork(CardScriptableObject cardSO)
    {
        if (characterArtImage != null)
        {
            characterArtImage.sprite = cardSO != null ? cardSO.characterSprite : null;
            characterArtImage.enabled = cardSO != null && cardSO.characterSprite != null;
        }

        if (backgroundImage != null)
        {
            backgroundImage.sprite = cardSO != null ? cardSO.bgSprite : null;
            backgroundImage.enabled = cardSO != null && cardSO.bgSprite != null;
        }
    }

    private void ApplyFrontVisuals(CardScriptableObject cardSO, bool upgraded)
    {
        if (frontOverlayImage == null && rarityEmblemImage == null)
            return;

        if (cardSO == null)
        {
            if (frontOverlayImage != null)
            {
                frontOverlayImage.sprite = null;
                frontOverlayImage.enabled = false;
            }
            if (rarityEmblemImage != null)
            {
                rarityEmblemImage.sprite = null;
                rarityEmblemImage.enabled = false;
            }
            return;
        }

        EnsureVisualProfile();

        if (frontOverlayImage != null)
        {
            var front = _visualProfile != null ? _visualProfile.GetFront(cardSO.Rarity, upgraded) : null;
            frontOverlayImage.sprite = front;
            frontOverlayImage.enabled = front != null;
        }

        if (rarityEmblemImage != null)
        {
            var emblem = _visualProfile != null ? _visualProfile.GetEmblem(cardSO.Rarity) : null;
            rarityEmblemImage.sprite = emblem;
            rarityEmblemImage.enabled = emblem != null;
        }
    }

    private void UpdateEffectDisplay(CardScriptableObject cardSO)
    {
        if (effectIconImage == null && effectValueText == null)
            return;

        if (cardSO == null || cardSO.Effects == null || cardSO.Effects.Count == 0)
        {
            HideEffect();
            return;
        }

        var primary = cardSO.Effects[0];
        if (primary == null || primary.Type == CardEffectType.None)
        {
            HideEffect();
            return;
        }

        EnsureIconDatabase();
        if (_iconDatabase == null)
        {
            HideEffect();
            return;
        }

        var icon = _iconDatabase.GetIcon(primary.Type);
        if (icon == null)
        {
            HideEffect();
            return;
        }

        int value = primary.Value != 0 ? primary.Value : primary.Potency;

        if (effectIconImage != null)
        {
            effectIconImage.sprite = icon;
            effectIconImage.enabled = true;
        }

        if (effectValueText != null)
        {
            if (value != 0)
            {
                effectValueText.text = value > 0 ? $"+{value}" : value.ToString();
                effectValueText.gameObject.SetActive(true);
            }
            else
            {
                effectValueText.text = string.Empty;
                effectValueText.gameObject.SetActive(false);
            }
        }
    }

    private void HideEffect()
    {
        if (effectIconImage != null)
        {
            effectIconImage.sprite = null;
            effectIconImage.enabled = false;
        }

        if (effectValueText != null)
        {
            effectValueText.text = string.Empty;
            effectValueText.gameObject.SetActive(false);
        }
    }

    private void CacheDefaultColors()
    {
        if (cardNameText != null && !_hasDefaultNameColor)
        {
            _defaultNameColor = cardNameText.color;
            _hasDefaultNameColor = true;
        }

        if (attackText != null && !_hasDefaultAttackColor)
        {
            _defaultAttackColor = attackText.color;
            _hasDefaultAttackColor = true;
        }

        if (healthText != null && !_hasDefaultHealthColor)
        {
            _defaultHealthColor = healthText.color;
            _hasDefaultHealthColor = true;
        }

        if (costText != null && !_hasDefaultCostColor)
        {
            _defaultCostColor = costText.color;
            _hasDefaultCostColor = true;
        }
    }

    private void EnsureIconDatabase()
    {
        if (_iconDatabase != null)
            return;

        _iconDatabase = iconDatabaseOverride != null
            ? iconDatabaseOverride
            : ServiceRegistry.Get<EffectIconDatabase>();

        if (_iconDatabase == null)
        {
            _iconDatabase = Resources.Load<EffectIconDatabase>("Cards/EffectIconDatabase");
            if (_iconDatabase != null)
            {
                ServiceRegistry.Register(_iconDatabase);
            }
        }
    }

    private void EnsureVisualProfile()
    {
        if (_visualProfile != null)
            return;

        _visualProfile = visualProfileOverride != null
            ? visualProfileOverride
            : CardVisualRegistry.Profile;
    }
}

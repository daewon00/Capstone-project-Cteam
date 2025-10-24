using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "card", order = 1)]
public class CardScriptableObject : ScriptableObject
{

    public string cardName;
    public string CardId; //Id 카드 고유 Id
    //public CardType cardType; //추가1
    //public DamageType damageType;//추가2

    [TextArea]
    public string actionDescription, cardLore;
    
    [Tooltip("카드 마나 비용")]
    public int manaCost;
    [Tooltip("카드 공격력")]
    public int attackPower;
    [Tooltip("카드 체력")]
    public int currentHealth;

    public Sprite characterSprite, bgSprite;

    [SerializeField]
    private CardRarity rarity = CardRarity.Common;

    [SerializeField]
    private List<CardEffectDefinition> effects = new();

    [Header("강화 설정")]
    [SerializeField] private CardUpgradeDefinition upgradeSettings = new CardUpgradeDefinition();

    public CardRarity Rarity => rarity;

    public IReadOnlyList<CardEffectDefinition> Effects => effects;

    [Tooltip("전투 종료 시 덱에서 제거할 임시 카드인지 여부")]
    public bool removeAfterCombat;

    public bool UpgradeEnabled => upgradeSettings != null && upgradeSettings.Enabled;

    public int GetManaCost(bool upgraded) => Mathf.Max(0, upgraded && UpgradeEnabled ? upgradeSettings.ManaCost : manaCost);

    public int GetAttackPower(bool upgraded) => upgraded && UpgradeEnabled ? upgradeSettings.AttackPower : attackPower;

    public int GetHealth(bool upgraded) => upgraded && UpgradeEnabled ? upgradeSettings.Health : currentHealth;

    public string GetDisplayName(bool upgraded)
    {
        if (string.IsNullOrEmpty(cardName))
            return upgraded && UpgradeEnabled ? $"{CardId}+" : CardId;
        return upgraded && UpgradeEnabled ? $"{cardName}+" : cardName;
    }

    public static readonly Color UpgradeNameColor = new Color(0.3f, 0.95f, 0.45f);

    private void OnValidate()
    {
        if (upgradeSettings == null)
            upgradeSettings = new CardUpgradeDefinition();

        upgradeSettings.OnValidate(manaCost, attackPower, currentHealth);
    }

    [Serializable]
    private class CardUpgradeDefinition
    {
        [SerializeField, Tooltip("카드가 강화될 수 있는지 여부입니다. 활성화하면 아래 수치를 입력할 수 있습니다.")] private bool enabled;
        [SerializeField, Tooltip("강화 후 카드의 코스트입니다. 기본값은 원래 코스트이며 0 이하로 내려갈 경우 0으로 자동 보정됩니다.")] private int manaCost;
        [SerializeField, Tooltip("강화 후 카드의 공격력입니다. 기본값은 원래 공격력으로 채워집니다.")] private int attackPower;
        [SerializeField, Tooltip("강화 후 카드의 체력입니다. 기본값은 원래 체력으로 채워집니다.")] private int health;
        [HideInInspector] public bool initialized;

        public bool Enabled => enabled;
        public int ManaCost => Mathf.Max(0, manaCost);
        public int AttackPower => attackPower;
        public int Health => health;

        public void OnValidate(int baseCost, int baseAttack, int baseHealth)
        {
            if (!enabled)
            {
                initialized = false;
                return;
            }

            if (!initialized)
            {
                manaCost = baseCost;
                attackPower = baseAttack;
                health = baseHealth;
                initialized = true;
            }

            manaCost = Mathf.Max(0, manaCost);
        }
    }

    /*
    public enum CardType //추가1
    {
        Fire,
        Ice,
        Wind,
        electric,
        Light,
        Dark

    }
    public enum DamageType //추가2
    {
        Fire,
        Ice,
        Wind,
        electric,
        Light,
        Dark
    }
    */
}

public enum CardRarity
{
    Common = 0,
    Rare = 1,
    Heroic = 2,
    Legendary = 3
}

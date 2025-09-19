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
    
    public int currentHealth, attackPower, manaCost;

    public Sprite characterSprite, bgSprite;

    [SerializeField]
    private List<CardEffectDefinition> effects = new();

    public IReadOnlyList<CardEffectDefinition> Effects => effects;

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

using System;
using UnityEngine;

public static class GameEvents
{
    // Battle flow
    public static event Action OnBattleStart;
    public static event Action OnBattleEnd;

    // Turn flow
    public static event Action<bool> OnTurnStart;
    public static event Action<bool> OnTurnEnd;

    // Card flow
    public static event Action<Card> OnCardDrawn;
    public static event Action<Card> OnCardPlayed;
    public static event Action<int, bool> OnDamageDealt;
    public static event Action OnPlayerAttackModifiersChanged;
    public static event Action OnEnemyAttackModifiersChanged;
    public static event Action OnCardManaCostModifiersChanged;
    public static event Action OnCardHealthModifiersChanged;
    public static event Action OnCardAttackModifiersChanged;

    // Raise helpers
    public static void RaiseBattleStart() => OnBattleStart?.Invoke();
    public static void RaiseBattleEnd() => OnBattleEnd?.Invoke();
    public static void RaiseTurnStart(bool isPlayerTurn) => OnTurnStart?.Invoke(isPlayerTurn);
    public static void RaiseTurnEnd(bool isPlayerTurn) => OnTurnEnd?.Invoke(isPlayerTurn);
    public static void RaiseCardDrawn(Card card) => OnCardDrawn?.Invoke(card);
    public static void RaiseCardPlayed(Card card) => OnCardPlayed?.Invoke(card);
    public static void RaiseDamageDealt(int damage, bool isFromPlayer) => OnDamageDealt?.Invoke(damage, isFromPlayer);
    public static void RaisePlayerAttackModifiersChanged() => OnPlayerAttackModifiersChanged?.Invoke();
    public static void RaiseEnemyAttackModifiersChanged() => OnEnemyAttackModifiersChanged?.Invoke();
    public static void RaiseCardManaCostModifiersChanged() => OnCardManaCostModifiersChanged?.Invoke();
    public static void RaiseCardHealthModifiersChanged() => OnCardHealthModifiersChanged?.Invoke();
    public static void RaiseCardAttackModifiersChanged() => OnCardAttackModifiersChanged?.Invoke();
    // Modifiers
    public static event Func<int, int> ModifyPlayerAttack;
    public static event Func<int, int> ModifyEnemyAttack;
    public static event Func<int, int> ModifyPlayerMana;
    public static event Func<Card, int, int> ModifyCardManaCost;
    public static event Func<Card, int, int> ModifyCardHealth;
    public static event Func<Card, int, int> ModifyCardAttack;

    public static int ApplyPlayerAttackModifiers(int baseValue)
        => ApplyModifierChain(ModifyPlayerAttack, baseValue);

    public static int ApplyEnemyAttackModifiers(int baseValue)
        => ApplyModifierChain(ModifyEnemyAttack, baseValue);

    public static int ApplyPlayerManaModifiers(int baseValue)
        => ApplyModifierChain(ModifyPlayerMana, baseValue);

    public static int ApplyCardManaCostModifiers(Card card, int baseValue)
        => ApplyModifierChain(ModifyCardManaCost, card, baseValue);
        
    public static int ApplyCardHealthModifiers(Card card, int baseValue)
         => ApplyModifierChain(ModifyCardHealth, card, baseValue);

    public static int ApplyCardAttackModifiers(Card card, int baseValue)
        => ApplyModifierChain(ModifyCardAttack, card, baseValue);


    private static int ApplyModifierChain(Func<int, int> chain, int baseValue)
    {
        if (chain == null)
            return baseValue;

        int value = baseValue;
        foreach (var handler in chain.GetInvocationList())
        {
            try
            {
                value = ((Func<int, int>)handler)(value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameEvents] Modifier handler threw exception: {ex.Message}");
            }
        }
        return value;
    }

    private static int ApplyModifierChain(Func<Card, int, int> chain, Card card, int baseValue)
    {
        if (chain == null)
            return baseValue;

        int value = baseValue;
        foreach (var handler in chain.GetInvocationList())
        {
            try
            {
                value = ((Func<Card, int, int>)handler)(card, value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameEvents] Card stat modifier handler threw exception: {ex.Message}");
            }
        }
        return value;
    }
}

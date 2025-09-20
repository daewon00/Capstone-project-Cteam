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

    // Raise helpers
    public static void RaiseBattleStart() => OnBattleStart?.Invoke();
    public static void RaiseBattleEnd() => OnBattleEnd?.Invoke();
    public static void RaiseTurnStart(bool isPlayerTurn) => OnTurnStart?.Invoke(isPlayerTurn);
    public static void RaiseTurnEnd(bool isPlayerTurn) => OnTurnEnd?.Invoke(isPlayerTurn);
    public static void RaiseCardDrawn(Card card) => OnCardDrawn?.Invoke(card);
    public static void RaiseCardPlayed(Card card) => OnCardPlayed?.Invoke(card);
    public static void RaiseDamageDealt(int damage, bool isFromPlayer) => OnDamageDealt?.Invoke(damage, isFromPlayer);

    // Modifiers
    public static event Func<int, int> ModifyPlayerAttack;
    public static event Func<int, int> ModifyEnemyAttack;
    public static event Func<int, int> ModifyPlayerMana;

    public static int ApplyPlayerAttackModifiers(int baseValue)
        => ApplyModifierChain(ModifyPlayerAttack, baseValue);

    public static int ApplyEnemyAttackModifiers(int baseValue)
        => ApplyModifierChain(ModifyEnemyAttack, baseValue);

    public static int ApplyPlayerManaModifiers(int baseValue)
        => ApplyModifierChain(ModifyPlayerMana, baseValue);

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
}

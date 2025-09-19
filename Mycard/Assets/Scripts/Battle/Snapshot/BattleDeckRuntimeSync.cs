using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleDeckRuntimeSync
{
    public static void UpdateCardState(Card card)
    {
        if (card == null) return;
        var deckService = ServiceRegistry.Get<IDeckService>();
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        if (deckService == null) return;

        var battleState = new BattleCardState
        {
            instanceId = card.GetBattleInstanceId(),
            cardId = card.cardSO != null ? card.cardSO.CardId : string.Empty,
            currentHp = card.currentHealth,
            attack = card.attackPower,
            slotIndex = ResolveSlotIndex(card),
            isPlayer = card.isPlayer,
            effectState = effectService?.CaptureCardState(card)
        };

        var location = ResolveLocation(card);
        deckService.UpdateBattleCardState(battleState, location);
    }

    public static BattleCardState ParseModifiers(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<BattleCardState>(json);
        }
        catch
        {
            return null;
        }
    }

    private static CardLocation ResolveLocation(Card card)
    {
        if (card.inHand)
            return CardLocation.Hand;

        if (card.assignedPlace != null)
            return card.isPlayer ? CardLocation.PlayerField : CardLocation.EnemyField;

        return CardLocation.DiscardPile;
    }

    private static int ResolveSlotIndex(Card card)
    {
        if (card.assignedPlace == null) return -1;
        var board = CardPointsController.instance;
        if (board == null) return -1;

        var arr = card.isPlayer ? board.playerCardPoints : board.enemyCardPoints;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == card.assignedPlace) return i;
        }
        var bench = board.enemyStayPoints;
        if (!card.isPlayer && bench != null)
        {
            for (int i = 0; i < bench.Length; i++)
            {
                if (bench[i] == card.assignedPlace) return i;
            }
        }
        return -1;
    }
}

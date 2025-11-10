using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraDrawRelic : Relic
{
    public ExtraDrawRelic(RelicData data) : base(data) { }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) return;

        var deck = GameServices.Deck;
        if (deck == null)
        {
            GameLog.Warn("[ExtraDrawRelic] GameServices.Deck ϵ ʾҽϴ.");
            return;
        }

        deck.DrawCards(Mathf.Max(1, Stacks), DrawReason.CardEffect);
    }
}
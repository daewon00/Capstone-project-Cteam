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
            Debug.LogWarning("[ExtraDrawRelic] GameServices.Deck이 등록되지 않았습니다.");
            return;
        }

        deck.DrawCards(Mathf.Max(1, Stacks), DrawReason.CardEffect);
    }
}
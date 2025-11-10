using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManaDiscountRelic : Relic
{
    // ī庰 츮  η  (п ǹ )
    private readonly Dictionary<Card, int> applied = new Dictionary<Card, int>();

    public ManaDiscountRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        //  ÷̾  ο øŭ  
        ApplyToEntireHand(Stacks);
        GameLog.Info($"[Relic] {Data.displayName} ȹ.  ڽƮ -{Stacks}");
    }

    protected override void OnStacksChanged()
    {
        //  ϸ,   ī ǥ η = Stacks 
        ApplyToEntireHand(Stacks);
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) return;
        //    а ٲ   (ߺ )
        ApplyToEntireHand(Stacks);
    }

    public override void OnCardDrawn(Card card)
    {
        //   ÷̾ ī忡  
        if (card != null && card.isPlayer) ApplyDiscount(card, Stacks);
    }

    public override void OnCardPlayed(Card card)
    {
        // տ  ī  ̻   
        if (card != null) applied.Remove(card);
    }

    public override void OnRemove()
    {
        // п ִ ī鿡 츮  θŭ ǵ
        foreach (var kv in SnapshotAlive())
        {
            var c = kv.Key; var have = kv.Value;
            if (c == null) continue;
            if (have > 0) { c.manaCost += have; c.UpdateCardDisplay(); }
        }
        applied.Clear();
        GameLog.Info($"[Relic] {Data.displayName} .  ڽƮ ");
    }

    // ---------- helpers ----------
    private void ApplyToEntireHand(int targetDiscountPerCard)
    {
        CleanupNulls();
        var hand = HandController.instance?.heldCards;
        if (hand == null) return;

        foreach (var c in hand)
        {
            if (c == null || !c.isPlayer) continue;
            ApplyDiscount(c, targetDiscountPerCard);
        }

        // տ  ī尡 ִٸ  
        var toRemove = new List<Card>();
        foreach (var kv in applied)
        {
            var c = kv.Key;
            if (c == null) { toRemove.Add(c); continue; }
            if (!hand.Contains(c)) toRemove.Add(c);
        }
        foreach (var c in toRemove) applied.Remove(c);
    }

    private void ApplyDiscount(Card c, int target)
    {
        //  ī忡 츮  η
        applied.TryGetValue(c, out int cur);
        int delta = target - cur; // þ (+) Ǵ  (-)
        if (delta == 0) return;

        if (delta > 0)
        {
            //  ø: ڽƮ deltaŭ ߵ 0 ̸ 
            int newCost = Mathf.Max(0, c.manaCost - delta);
            int realDelta = c.manaCost - newCost; //   (0~delta)
            if (realDelta > 0)
            {
                c.manaCost = newCost;
                c.UpdateCardDisplay();
                applied[c] = cur + realDelta;
            }
        }
        else
        {
            //  (): (-delta)ŭ ڽƮ 
            int addBack = -delta;
            c.manaCost += addBack;
            c.UpdateCardDisplay();
            applied[c] = Mathf.Max(0, cur - addBack);
            if (applied[c] == 0) applied.Remove(c);
        }
    }

    private void CleanupNulls()
    {
        var dead = new List<Card>();
        foreach (var kv in applied) if (kv.Key == null) dead.Add(kv.Key);
        foreach (var d in dead) applied.Remove(d);
    }

    private List<KeyValuePair<Card, int>> SnapshotAlive()
    {
        CleanupNulls();
        return new List<KeyValuePair<Card, int>>(applied);
    }
}
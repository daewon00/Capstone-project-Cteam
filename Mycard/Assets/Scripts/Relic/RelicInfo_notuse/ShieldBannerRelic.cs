using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldBannerRelic : Relic
{
    // ī庰 츮 " HP" 
    private readonly Dictionary<Card, int> granted = new Dictionary<Card, int>();
    private int lastStacks = 0;

    public ShieldBannerRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        lastStacks = Stacks;
        BuffAllPlayerBoardCards(Stacks);
        GameLog.Info($"[Relic] {Data.displayName} ȹ. ={Stacks}");
    }

    protected override void OnStacksChanged()
    {
        int delta = Stacks - lastStacks;
        lastStacks = Stacks;
        if (delta == 0) return;

        if (delta > 0)  //    ü ߰
        {
            foreach (var kv in SnapshotAliveGranted())
                ApplyHp(kv.Key, delta);
        }
        else            //  /  ü Ϻ ȸ(ּ 1HP )
        {
            foreach (var kv in SnapshotAliveGranted())
                RemoveHpSafe(kv.Key, -delta);
        }
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) return;
        // Ȥ  ߰   ī尡 ִٸ     
        BuffAllPlayerBoardCards(Stacks);
    }

    // ī尡    ݿϰ   (Ʒ Card.cs   )
    public override void OnCardPlayed(Card card)
    {
        if (card != null && card.isPlayer)
            ApplyHp(card, Stacks);
    }

    public override void OnRemove()
    {
        // 츮 οߴ 縸 ϰ ȸ(ּ 1HP )
        foreach (var kv in SnapshotAliveGranted())
            RemoveHpSafe(kv.Key, kv.Value);
        granted.Clear();
        GameLog.Info($"[Relic] {Data.displayName} .");
    }

    // ---------------- helpers ----------------
    private void BuffAllPlayerBoardCards(int amount)
    {
        var cpc = CardPointsController.instance;
        if (cpc == null || cpc.playerCardPoints == null) return;

        CleanupDead();
        foreach (var p in cpc.playerCardPoints)
        {
            var c = p?.activeCard;
            if (c == null || !c.isPlayer) continue;

            // ߺ  :    HP  ÷ٸ ÷ش
            if (!granted.ContainsKey(c))
                ApplyHp(c, amount);
        }
    }

    private void ApplyHp(Card card, int amount)
    {
        if (card == null || amount <= 0) return;
        card.currentHealth += amount;
        card.UpdateCardDisplay();
        if (granted.TryGetValue(card, out var already))
            granted[card] = already + amount;
        else
            granted[card] = amount;
        // ʿϸ Ʈ/ ߰ 
    }

    private void RemoveHpSafe(Card card, int amount)
    {
        if (card == null || amount <= 0) return;
        if (!granted.TryGetValue(card, out var gave)) return;

        int toTake = Mathf.Min(amount, gave);
        // ּ 1HP (ϸ     ٲټ)
        int minLeft = Mathf.Max(1, card.currentHealth - toTake);
        int realTake = card.currentHealth - minLeft;

        if (realTake > 0)
        {
            card.currentHealth -= realTake;
            card.UpdateCardDisplay();
            granted[card] = gave - realTake;
            if (granted[card] <= 0) granted.Remove(card);
        }
    }

    private void CleanupDead()
    {
        // ı ī/ 
        var dead = new List<Card>();
        foreach (var kv in granted)
            if (kv.Key == null) dead.Add(kv.Key);
        foreach (var d in dead) granted.Remove(d);
    }

    private IEnumerable<KeyValuePair<Card, int>> SnapshotAliveGranted()
    {
        CleanupDead();
        // foreach ȸ    
        return new List<KeyValuePair<Card, int>>(granted);
    }
}
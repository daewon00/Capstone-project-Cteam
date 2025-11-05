using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldBannerRelic : Relic
{
    // 카드별로 우리가 "보태준 HP"를 기억
    private readonly Dictionary<Card, int> granted = new Dictionary<Card, int>();
    private int lastStacks = 0;

    public ShieldBannerRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        lastStacks = Stacks;
        BuffAllPlayerBoardCards(Stacks);
        Debug.Log($"[Relic] {Data.displayName} 획득. 스택={Stacks}");
    }

    protected override void OnStacksChanged()
    {
        int delta = Stacks - lastStacks;
        lastStacks = Stacks;
        if (delta == 0) return;

        if (delta > 0)  // 스택 증가 → 체력 추가
        {
            foreach (var kv in SnapshotAliveGranted())
                ApplyHp(kv.Key, delta);
        }
        else            // 스택 감소/너프 → 체력 일부 회수(최소 1HP는 보장)
        {
            foreach (var kv in SnapshotAliveGranted())
                RemoveHpSafe(kv.Key, -delta);
        }
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) return;
        // 혹시 턴 중간에 새로 깔린 카드가 있다면 다음 턴 시작 때라도 보정
        BuffAllPlayerBoardCards(Stacks);
    }

    // 카드가 깔릴 때 즉시 반영하고 싶을 때 사용(아래 Card.cs 훅이 있을 경우)
    public override void OnCardPlayed(Card card)
    {
        if (card != null && card.isPlayer)
            ApplyHp(card, Stacks);
    }

    public override void OnRemove()
    {
        // 우리가 부여했던 양만 안전하게 회수(최소 1HP는 유지)
        foreach (var kv in SnapshotAliveGranted())
            RemoveHpSafe(kv.Key, kv.Value);
        granted.Clear();
        Debug.Log($"[Relic] {Data.displayName} 제거.");
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

            // 중복 적용 방지: 아직 이 유물로 HP를 안 올려줬다면 올려준다
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
        // 필요하면 이펙트/사운드 추가 가능
    }

    private void RemoveHpSafe(Card card, int amount)
    {
        if (card == null || amount <= 0) return;
        if (!granted.TryGetValue(card, out var gave)) return;

        int toTake = Mathf.Min(amount, gave);
        // 최소 1HP 보장(원하면 제거 시 즉사 허용으로 바꾸세요)
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
        // 파괴된 카드/참조 정리
        var dead = new List<Card>();
        foreach (var kv in granted)
            if (kv.Key == null) dead.Add(kv.Key);
        foreach (var d in dead) granted.Remove(d);
    }

    private IEnumerable<KeyValuePair<Card, int>> SnapshotAliveGranted()
    {
        CleanupDead();
        // foreach 순회 중 수정 방지용 스냅샷
        return new List<KeyValuePair<Card, int>>(granted);
    }
}
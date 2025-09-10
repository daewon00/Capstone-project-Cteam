using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManaDiscountRelic : Relic
{
    // 카드별로 우리가 적용한 할인량을 추적 (손패에서만 의미 있음)
    private readonly Dictionary<Card, int> applied = new Dictionary<Card, int>();

    public ManaDiscountRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        // 현재 플레이어 손패 전부에 스택만큼 할인 적용
        ApplyToEntireHand(Stacks);
        Debug.Log($"[Relic] {Data.displayName} 획득. 손패 코스트 -{Stacks}");
    }

    protected override void OnStacksChanged()
    {
        // 스택이 변하면, 손패 각 카드의 목표 할인량 = Stacks로 재조정
        ApplyToEntireHand(Stacks);
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) return;
        // 턴 시작 시점에 손패가 바뀌었을 수 있으니 재적용(중복 안전)
        ApplyToEntireHand(Stacks);
    }

    public override void OnCardDrawn(Card card)
    {
        // 새로 뽑힌 플레이어 카드에 즉시 적용
        if (card != null && card.isPlayer) ApplyDiscount(card, Stacks);
    }

    public override void OnCardPlayed(Card card)
    {
        // 손에서 나간 카드는 더 이상 관리 안 함
        if (card != null) applied.Remove(card);
    }

    public override void OnRemove()
    {
        // 손패에 남아있는 카드들에서 우리가 준 할인만큼 되돌림
        foreach (var kv in SnapshotAlive())
        {
            var c = kv.Key; var have = kv.Value;
            if (c == null) continue;
            if (have > 0) { c.manaCost += have; c.UpdateCardDisplay(); }
        }
        applied.Clear();
        Debug.Log($"[Relic] {Data.displayName} 제거. 손패 코스트 복구");
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

        // 손에서 빠져나간 카드가 있다면 추적에서 제거
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
        // 현재 카드에 우리가 적용한 할인량
        applied.TryGetValue(c, out int cur);
        int delta = target - cur; // 늘어날 할인(+) 또는 줄일 할인(-)
        if (delta == 0) return;

        if (delta > 0)
        {
            // 할인 늘림: 코스트를 delta만큼 낮추되 0 미만은 방지
            int newCost = Mathf.Max(0, c.manaCost - delta);
            int realDelta = c.manaCost - newCost; // 실제로 내려간 값(0~delta)
            if (realDelta > 0)
            {
                c.manaCost = newCost;
                c.UpdateCardDisplay();
                applied[c] = cur + realDelta;
            }
        }
        else
        {
            // 할인 축소(복구): (-delta)만큼 코스트 증가
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
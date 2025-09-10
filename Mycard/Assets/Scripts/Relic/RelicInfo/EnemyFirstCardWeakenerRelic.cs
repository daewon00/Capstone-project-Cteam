using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public sealed class EnemyFirstCardWeakenerRelic : Relic
{
    private bool _usedThisTurn;

    public EnemyFirstCardWeakenerRelic(RelicData data) : base(data) { }

    // 적 턴이 시작될 때마다 "이번 턴에 이미 적용했는가?" 리셋
    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) _usedThisTurn = false;
    }

    // 카드가 "보드에" 플레이되면 호출됨 위에서 적도 이벤트를 쏘게 만들었음
    public override void OnCardPlayed(Card card)
    {
        if (_usedThisTurn || card == null || card.isPlayer) return;

        int reduce = Mathf.Max(1, Stacks);        // 스택만큼 감소 (기본 1)
        int before = card.attackPower;
        card.attackPower = Mathf.Max(0, before - reduce);
        card.UpdateCardDisplay();                 

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnemyFirstCardWeakener] {card.cardSO?.cardName}: {before} → {card.attackPower} (-{reduce})");
#endif
        _usedThisTurn = true;
    }
}
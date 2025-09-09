using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFirstCardWeakenerRelic : Relic
{
    // 이번 적 턴에 이미 1장 약화했는지 여부
    private bool _usedThisEnemyTurn = false;

    public EnemyFirstCardWeakenerRelic(RelicData data) : base(data) { }

    // 턴 시작마다 상태 초기화
    public override void OnTurnStart(bool isPlayerTurn)
    {
        if (!isPlayerTurn) _usedThisEnemyTurn = false; // 적 턴 시작 시 리셋
    }

    // 카드가 “필드에 성공적으로 놓였을 때” 호출됨
    public override void OnCardPlayed(Card card)
    {
        if (_usedThisEnemyTurn || card == null) return;

        // 지금이 적 액티브 턴인지 확인
        var bc = BattleController.instance;
        if (bc == null || bc.currentPhase != BattleController.TurnOrder.enemyActive) return;

        // 이 카드가 적 카드인지 확인(안전하게 두 가지 기준으로 체크)
        bool isEnemyCard =
            (card.isPlayer == false) ||
            (card.assignedPlace != null && card.assignedPlace.isPlayerPoint == false);

        if (!isEnemyCard) return;

        // 공격력 1 감소(스택 적용 원하면 +Stacks 로 바꾸면 됨)
        card.attackPower = Mathf.Max(0, card.attackPower - 1);
        card.UpdateCardDisplay();

        _usedThisEnemyTurn = true;
    }
}
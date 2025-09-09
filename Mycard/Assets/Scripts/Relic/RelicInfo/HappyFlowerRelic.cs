using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HappyFlowerRelic : Relic
{
    private int grantedTotal = 0;

    public HappyFlowerRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        ApplyOrAdjust();  // 획득 즉시 +10 (스택이면 10 * Stacks)
        Debug.Log($"[Relic] {Data.displayName} 획득 → PlayerHealth +{grantedTotal}");
    }

    public override void OnRemove()
    {
        // 유물 제거 시 되돌리기(선택: 원복이 싫으면 이 블록을 지워도 됨)
        var bc = BattleController.instance;
        if (bc != null && grantedTotal != 0)
        {
            bc.playerHealth -= grantedTotal;
            grantedTotal = 0;
            UIController.instance?.setPlayerHealthText(bc.playerHealth);
        }
        Debug.Log($"[Relic] {Data.displayName} 제거 → 보정 해제");
    }

    protected override void OnStacksChanged()
    {
        // 스택이 늘거나 줄었을 때 차액만큼 조정(+10 per stack)
        ApplyOrAdjust();
        Debug.Log($"[Relic] {Data.displayName} 스택:{Stacks} → 누적 +{grantedTotal}");
    }

    // 현재 스택에 맞춰 (10 * Stacks) 가 되도록 체력을 증감시킴
    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        int target = 10 * Stacks;
        int delta = target - grantedTotal;   // 지금까지 준 것과의 차액
        if (delta != 0)
        {
            bc.playerHealth += delta;
            grantedTotal += delta;
            UIController.instance?.setPlayerHealthText(bc.playerHealth);
        }
    }
}

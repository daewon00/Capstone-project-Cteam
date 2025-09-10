using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManaGem : Relic
{
    // 지금까지 실제로 적용한 총 증가량(= 스택 수와 동일하게 유지)
    private int applied = 0;

    public ManaGem(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        ApplyOrAdjust();
        Debug.Log($"[Relic] {Data.displayName} 획득 → playerMax/playerMana +{applied}");
    }

    protected override void OnStacksChanged()
    {
        ApplyOrAdjust();
        Debug.Log($"[Relic] {Data.displayName} 스택:{Stacks} → 누적 +{applied}");
    }

    public override void OnRemove()
    {
        var bc = BattleController.instance;
        if (bc != null && applied != 0)
        {
            bc.playermaxMana -= applied;
            bc.playerMana = Mathf.Max(0, bc.playerMana - applied);
            UpdateManaUI();
            applied = 0;
        }
        Debug.Log($"[Relic] {Data.displayName} 제거 → 보정 해제");
    }

    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        int target = Stacks;          // 스택 1당 +1
        int delta = target - applied; // 차액만 반영
        if (delta == 0) return;

        bc.playermaxMana += delta;    // 최대 마나 상한 +delta
        bc.playerMana += delta;    // 현재 마나도 즉시 +delta

        applied = target;
        UpdateManaUI();
    }

    private void UpdateManaUI()
    {
        var bc = BattleController.instance;
        if (bc != null) {
            UIController.instance?.SetPlayerManaText(bc.playerMana); 

        
        }
    }
}
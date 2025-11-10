using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HappyFlowerRelic : Relic
{
    private int grantedTotal = 0;

    public HappyFlowerRelic(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        ApplyOrAdjust();  // ȹ  +10 (̸ 10 * Stacks)
        GameLog.Info($"[Relic] {Data.displayName} ȹ  PlayerHealth +{grantedTotal}");
    }

    public override void OnRemove()
    {
        //    ǵ(:      )
        var bc = BattleController.instance;
        if (bc != null && grantedTotal != 0)
        {
            bc.playerHealth -= grantedTotal;
            grantedTotal = 0;
            UIController.instance?.setPlayerHealthText(bc.playerHealth);
        }
        GameLog.Info($"[Relic] {Data.displayName}    ");
    }

    protected override void OnStacksChanged()
    {
        //  ðų پ  ׸ŭ (+10 per stack)
        ApplyOrAdjust();
        GameLog.Info($"[Relic] {Data.displayName} :{Stacks}   +{grantedTotal}");
    }

    //  ÿ  (10 * Stacks)  ǵ ü Ŵ
    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        int target = 10 * Stacks;
        int delta = target - grantedTotal;   // ݱ  Ͱ 
        if (delta != 0)
        {
            bc.playerHealth += delta;
            grantedTotal += delta;
            UIController.instance?.setPlayerHealthText(bc.playerHealth);
        }
    }
}

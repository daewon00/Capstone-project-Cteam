using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManaGem : Relic
{
    // ݱ    (=   ϰ )
    private int applied = 0;

    public ManaGem(RelicData data) : base(data) { }

    public override void OnAdd()
    {
        ApplyOrAdjust();
        GameLog.Info($"[Relic] {Data.displayName} ȹ  playerMax/playerMana +{applied}");
    }

    protected override void OnStacksChanged()
    {
        ApplyOrAdjust();
        GameLog.Info($"[Relic] {Data.displayName} :{Stacks}   +{applied}");
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
        GameLog.Info($"[Relic] {Data.displayName}    ");
    }

    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        int target = Stacks;          //  1 +1
        int delta = target - applied; // ׸ ݿ
        if (delta == 0) return;

        bc.playermaxMana += delta;    // ִ   +delta
        bc.playerMana += delta;    //    +delta

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
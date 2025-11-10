using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManaLeechRelic : Relic
{
    // ݱ   ҷ( ȭ/ )
    private int applied = 0;

    public EnemyManaLeechRelic(RelicData data) : base(data) { }

    public override void OnAdd() => ApplyOrAdjust();
    protected override void OnStacksChanged() => ApplyOrAdjust();

    public override void OnRemove()
    {
        var bc = BattleController.instance;
        if (bc == null) { applied = 0; return; }

        if (applied != 0)
        {
            // 
            bc.currentEnemyMaxMana = Mathf.Max(0, bc.enemyMana + applied);
            bc.enemymaxMana = Mathf.Max(0, bc.enemymaxMana + applied);
            bc.startingEnemeyMana = Mathf.Max(0, bc.startingEnemeyMana + applied);
            applied = 0;
            GameLog.Info($"[Relic] {Data.displayName}   enemymaxMana/startingEnemeyMana ");
        }
    }

    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        // ǥ ҷ =  (ô -1)
        int target = Mathf.Max(0, Stacks);
        int delta = target - applied;   // (+  , - Ϻ )
        if (delta == 0) return;

        //  ġ  縸ŭ 
        bc.enemymaxMana = Mathf.Max(0, bc.enemymaxMana - delta);
        bc.startingEnemeyMana = Mathf.Max(0, bc.startingEnemeyMana - delta);
        bc.currentEnemyMaxMana = Mathf.Max(0, bc.enemyMana - delta);

        applied = target;

        GameLog.Info($"[Relic] {Data.displayName} : enemymaxMana={bc.enemymaxMana}, startingEnemeyMana={bc.startingEnemeyMana} (={Stacks})");
    }
}
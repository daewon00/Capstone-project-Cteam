using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarBannerRelic : Relic
{
    public WarBannerRelic(RelicData data) : base(data) { }
    
    public override int ModifyPlayerAttack(int baseAttack)
    {
        // ô +1
        return baseAttack + Stacks;
    }

    public override void OnAdd()
    {
        GameLog.Info($"[Relic] {Data.displayName} ȹ. : {Stacks}");
    }

}

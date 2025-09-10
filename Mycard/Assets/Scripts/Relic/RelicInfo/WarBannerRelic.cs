using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarBannerRelic : Relic
{
    public WarBannerRelic(RelicData data) : base(data) { }
    
    public override int ModifyPlayerAttack(int baseAttack)
    {
        // Ω∫≈√¥Á +1
        return baseAttack + Stacks;
    }

    public override void OnAdd()
    {
        Debug.Log($"[Relic] {Data.displayName} »πµÊ. Ω∫≈√: {Stacks}");
    }

}

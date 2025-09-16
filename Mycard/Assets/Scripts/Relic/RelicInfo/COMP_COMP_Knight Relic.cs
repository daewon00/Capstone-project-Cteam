using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class COMP_COMP_KnightRelic : Relic
{
    public COMP_COMP_KnightRelic(RelicData data) : base(data) { }

    // Start is called before the first frame update
    public override int ModifyPlayerAttack(int baseAttack)
    {
        // Ω∫≈√¥Á +2
        return baseAttack + Stacks*2;
    }

    public override void OnAdd()
    {
        Debug.Log($"[Relic] {Data.displayName} »πµÊ. Ω∫≈√: {Stacks}");
    }
}

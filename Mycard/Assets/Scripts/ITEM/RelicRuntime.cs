using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class RelicRuntime
{

    //public static int TotalAttackBonus { get; private set; }
    //public static int ExtraDrawPerTurn { get; private set; }
    //public static int ExtraManaPerTurn { get; private set; }
    public static void Apply(string relicId, int delta)
    {
        switch (relicId)
        {

            case "quick_thinking":    // 예: 매 턴 시작 드로우 +1
                //ExtraDrawPerTurn += 1 * delta;
                break;

            case "mana_font":         // 예: 매 턴 시작 추가 마나 +1
                //ExtraManaPerTurn += 1 * delta;
                break;

            default:
                // 아직 효과 미정인 Relic ID
                break;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Companion_", menuName = "Game/Companion Definition")]
public class CompanionDefinition : ScriptableObject
{
    [Header("Identity")]
    public string CompanionId;           // 예: "COMP_WARRIOR"
    public string DisplayName;           // 예: "전사"
    [TextArea] public string Description;
    public Sprite Portrait;

    [Header("Start Loadout")]
    public List<string> StartingCardIds = new();     // 예: "CARD_STRIKE", "CARD_DEFEND"
    public List<string> StartingRelicIds = new();    // 일반 유물 (동료 표식 유물은 자동 추가됨)
    public List<string> StartingPotionIds = new();   // 선택 (없어도 됨)

    [Header("Base Stats Mods (optional)")]
    public int MaxHpBonus;
    public int GoldBonus;
    public int EnergyMaxBonus;
}
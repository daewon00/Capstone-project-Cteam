using UnityEngine;

public enum ValueKind { Flat, Percentage }
public enum StackingMode { Additive, Multiplicative }

[CreateAssetMenu(menuName = "Progression/New Perk", fileName = "NewPerk")]
public class PerkDefinition : ScriptableObject
{
    public string Id;               // e.g., PERK_STARTING_GOLD_FLAT
    public string DisplayName;
    [TextArea] public string Description;
    public int Cost = 1;
    public int MaxLevel = 1;

    // Effect
    public string EffectKey;        // e.g., STARTING_GOLD
    public StackingMode StackingMode;
    public ValueKind Kind;
    public float PerLevelValue = 1f;
}


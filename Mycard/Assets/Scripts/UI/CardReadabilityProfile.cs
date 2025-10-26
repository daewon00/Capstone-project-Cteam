using System;
using UnityEngine;

// Simple profile to allow artists to tweak text outlines/colors if desired.
// If no asset exists under Resources/Cards/CardReadabilityProfile.asset,
// systems should silently fall back to defaults without logging.
[CreateAssetMenu(fileName = "CardReadabilityProfile", menuName = "Cards/Readability Profile", order = 0)]
public class CardReadabilityProfile : ScriptableObject
{
    [Header("Outline (TextMeshPro)")]
    public bool enableOutline = true;
    [Range(0f, 1f)] public float outlineWidth = 0.18f;
    public Color outlineColor = new Color(0f, 0f, 0f, 0.65f);
    public bool outlineAttack = true;
    public bool outlineHealth = true;
    public bool outlineCost = true;
    public bool outlineEffectValue = true;
    public bool outlineName = false;

    [Header("Cost Affordability Color")]
    public bool colorizeCostByAffordability = true;
    public Color unaffordableCostColor = new Color(1f, 0.45f, 0.45f);

    [Header("Attack Buff/Debuff Colors")]
    public bool colorizeAttackByModifier = true;
    public Color buffAttackColor = new Color(0.3f, 0.95f, 0.45f);
    public Color debuffAttackColor = new Color(1f, 0.4f, 0.4f);

    [Header("Health Damaged Color (optional)")]
    public bool colorizeHealthWhenDamaged = true;
    public Color damagedHealthColor = new Color(1f, 0.8f, 0.3f);
}


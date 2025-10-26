using System;
using UnityEngine;

/// <summary>
/// 카드 숫자 가독성(외곽선/색상) 설정을 데이터로 관리하는 프로필입니다.
/// Resources/Cards/CardReadabilityProfile.asset 로 배치해 런타임에 로드합니다.
/// </summary>
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


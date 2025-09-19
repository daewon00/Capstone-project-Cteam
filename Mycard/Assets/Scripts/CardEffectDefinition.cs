using System;
using UnityEngine;

/// <summary>
/// Authoring-time definition of a single card effect. This information lives on
/// <see cref="CardScriptableObject"/> and is interpreted at runtime by
/// <see cref="ICardEffectService"/>.
/// </summary>
[Serializable]
public class CardEffectDefinition
{
    [SerializeField] private CardEffectType type = CardEffectType.None;
    [SerializeField] private CardEffectTiming timing = CardEffectTiming.OnPlay;
    [SerializeField] private CardEffectTarget target = CardEffectTarget.Self;
    [SerializeField] private int potency = 0;
    [SerializeField] private int value = 0;
    [SerializeField] private string payloadId = string.Empty;
    [SerializeField] private bool stackable = true;
    [SerializeField] private int maxStacks = 0;

    /// <summary>대상 효과 유형.</summary>
    public CardEffectType Type => type;

    /// <summary>효과가 발동할 타이밍.</summary>
    public CardEffectTiming Timing => timing;

    /// <summary>효과가 적용될 대상 범위.</summary>
    public CardEffectTarget Target => target;

    /// <summary>효과의 강도(숫자 기반 값).</summary>
    public int Potency => potency;

    /// <summary>스택으로 누적 가능한지 여부.</summary>
    public bool Stackable => stackable;

    /// <summary>누적 가능한 최대 스택 수(0이면 무제한).</summary>
    public int MaxStacks => maxStacks;

    /// <summary>효과에서 사용하는 보조 값(숫자).</summary>
    public int Value => value;

    /// <summary>토큰 카드 ID 등 문자열 페이로드.</summary>
    public string PayloadId => payloadId;
}

/// <summary>카드 효과의 발동 타이밍.</summary>
public enum CardEffectTiming
{
    OnPlay,
    OnAttackSuccess,
    OnDamaged,
    OnOwnerTurnEnd,
    AuraWhileAlive,
    Passive
}

/// <summary>카드 효과의 대상 범위.</summary>
public enum CardEffectTarget
{
    Self,
    AttackedEnemy,
    AdjacentEnemies,
    Attacker,
    OwnerLeader,
    OpponentLeader,
    AdjacentEmptySlots
}

/// <summary>지원 예정 카드 효과 유형.</summary>
public enum CardEffectType
{
    [InspectorName("없음")] None = 0,
    [InspectorName("보호막")] AddShield,
    [InspectorName("독성(즉사)")] DestroyTarget,
    [InspectorName("연쇄 피해")] DamageAdjacent,
    [InspectorName("관통 공격")] Pierce,
    [InspectorName("반격")] CounterDamage,
    [InspectorName("흡혈")] HealLeader,
    [InspectorName("소환")] SummonToken,
    [InspectorName("이동")] Move,
    [InspectorName("추가 드로우")] DrawCard,
    [InspectorName("격려(공격력 +1)")] RallyAttackBonus
}

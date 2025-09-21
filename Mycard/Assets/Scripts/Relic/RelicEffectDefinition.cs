using System;
using UnityEngine;

/// <summary>
/// Authoring-time definition for a single relic effect. Designers can wire
/// these up on <see cref=\"RelicData\"/> to drive behaviour without adding
/// bespoke code for each relic.
/// </summary>
[Serializable]
public sealed class RelicEffectDefinition
{
    // ScriptableObject에서 유물 효과를 데이터로 정의하는 컨테이너입니다.
    [SerializeField] private RelicEffectTrigger trigger = RelicEffectTrigger.OnAdd;
    [SerializeField] private RelicEffectType type = RelicEffectType.None;
    [SerializeField] private int value = 0;
    [SerializeField] private bool scaleByStacks = true;
    [SerializeField] private string payloadId = string.Empty;

    /// <summary>언제 효과가 실행되어야 하는지.</summary>
    public RelicEffectTrigger Trigger => trigger;

    /// <summary>트리거가 발동했을 때 실행할 동작.</summary>
    public RelicEffectType Type => type;

    /// <summary>효과의 기본 수치 값.</summary>
    public int Value => value;

    /// <summary><see cref=\"Value\"/> 스택의 배수 만큼 적용할지 여부.</summary>
    public bool ScaleByStacks => scaleByStacks;

    /// <summary>선택적 보조 식별자 (card id, payload key, etc).</summary>
    public string PayloadId => payloadId;

    /// <summary>지정된 스택 수에 대한 실효 값을 가져옵니다.</summary>
    public int ResolveValue(int stacks)
    {
        if (!scaleByStacks)
            return value;
        return value * Mathf.Max(1, stacks);
    }
}

/// <summary>유물 효과가 연결될 수 있는 지원 훅 지점.</summary>
public enum RelicEffectTrigger
{
    // 유물 효과가 발동할 타이밍을 지정합니다.
    OnAdd,
    OnRemove,
    OnStacksChanged,
    OnBattleStart,
    OnBattleEnd,
    OnTurnStart,
    OnTurnEnd,
    OnCardDrawn,
    OnCardPlayed,
    OnDamageDealt,
    ModifyPlayerAttack,
    ModifyPlayerMana
}

/// <summary>데이터 기반 유물 효과가 지원하는 동작.</summary>
public enum RelicEffectType
{
    // 데이터 기반으로 실행할 수 있는 유물 효과 종류를 분류합니다.
    None = 0,
    AdjustPlayerManaCapacity,
    AdjustPlayerHealth,
    ModifyPlayerAttackFlat,
    ModifyPlayerManaFlat,
    GainPlayerMana,
    DrawCards,
    GainGold
}


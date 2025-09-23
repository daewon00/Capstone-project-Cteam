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
    OnStacksChanged, //유물 스택이 바뀔때
    OnBattleStart, //배틀 시작시
    OnBattleEnd, //배틀 종료시
    OnTurnStart, // 턴시작시
    OnTurnEnd, //턴 종료시
    OnCardDrawn, //카드 드로우됨
    OnCardPlayed, //card.isplayer일때 발동
    OnDamageDealt, //플레이어데미지를 받을때
    ModifyPlayerAttack, //카드의 공격력을 높일때 사용 ModifyPlayerAttackFlat과 같이 사용
    ModifyPlayerMana, //플레이어 마나가 채워질때 발동
    ModifyCardManaCost //카드의 마나를 줄일때 사용 -10으로 사용해주세요 +10하면 마나 값이 더해진다
}

/// <summary>데이터 기반 유물 효과가 지원하는 동작.</summary>
public enum RelicEffectType
{
    // 데이터 기반으로 실행할 수 있는 유물 효과 종류를 분류합니다.
    None = 0,
    AdjustPlayerManaCapacity, // 플레이어의 최대 마나 용량을 증가 감소
    AdjustPlayerHealth, // 플레이어의 체력(HP) 총량
    ModifyPlayerAttackFlat, //트리거될 때마다 계산된 공격력 수치를 누적하여 적용합니다. ModifyPlayerAttack과 함께 사용합니다
    ModifyPlayerManaFlat, // FillPlayerMana에서 사용하는 마나 값에 지정된 수치만큼 더해집니다.
    ModifyCardManaCostFlat,//카드 코스트 줄이기 ModifyCardManaCost과 함께 사용
    GainPlayerMana, // 즉시 마나를 회복시키는 트리거
    DrawCards, // 지정된 수의 카드를 플레이어에게 드로우하도록 요청하는 트리거 효과
    GainGold // 골드를 획득하는 트리거 효과
}


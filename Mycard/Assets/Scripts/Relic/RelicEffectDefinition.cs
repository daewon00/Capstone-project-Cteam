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
    [SerializeField] private RelicTriggerCondition triggerCondition = new RelicTriggerCondition();
    [SerializeField] private RelicDurationSettings duration = new RelicDurationSettings();
    [SerializeField] private RelicCooldownSettings cooldown = new RelicCooldownSettings();
    [SerializeField] private RelicTargetingSettings targeting = new RelicTargetingSettings();

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
    /// <summary>추가 발동 조건(쿨다운, HP 조건 등)을 담습니다.</summary>
    public RelicTriggerCondition TriggerCondition => triggerCondition;

    /// <summary>효과 유지 시간(턴 수) 설정입니다.</summary>
    public RelicDurationSettings Duration => duration;

    /// <summary>효과 재사용 대기시간 설정입니다.</summary>
    public RelicCooldownSettings Cooldown => cooldown;

    /// <summary>카드 대상 선택 규칙입니다.</summary>
    public RelicTargetingSettings Targeting => targeting;

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
    OnAdd, //유물이 추가될때
    OnRemove, //유물이 삭제될때
    OnStacksChanged, //유물 스택이 바뀔때
    OnBattleStart, //배틀 시작시
    OnBattleEnd, //배틀 종료시
    OnTurnStart, // 턴시작시
    OnTurnEnd, //턴 종료시
    OnCardDrawn, //카드 드로우했을때
    OnCardPlayed, //카드가 필드에 놓아졌을때
    OnDamageDealt, //플레이어데미지를 받을때
    ModifyPlayerAttack, //카드의 공격력을 높일때 사용 ModifyPlayerAttackFlat과 같이 사용
    ModifyPlayerMana, //플레이어 마나가 채워질때 발동
    ModifyCardManaCost, //카드의 마나를 줄일때 사용 -10으로 사용해주세요 +10하면 마나 값이 더해진다
    ModifyCardHealth, //카드 체력을 수정할 때 사용. ModifyCardHealthFlat과 함께 사용
    ModifyCardAttack //카드 공격력을 수정할 때 사용. ModifyCardAttackFlat과 함께 사용
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
    ModifyCardHealthFlat,//카드 체력을 평평하게 더하거나 뺍니다. ModifyCardHealth와 함께 사용
    ModifyCardAttackFlat,//카드 공격력을 고정 수치만큼 더하거나 뺍니다. ModifyCardAttack과 함께 사용
    GainPlayerMana, // 즉시 마나를 회복시키는 트리거
    DrawCards, // 지정된 수의 카드를 플레이어에게 드로우하도록 요청하는 트리거 효과
    GainGold, // 골드를 획득하는 트리거 효과
    AdjustTargetCardManaCostFlat, // Targeting 설정으로 선택된 카드의 코스트를 직접 증감합니다. OnTurnStart등과 사용해주세요 modify...와 사용시 즉시 적용될겁니다
    AdjustTargetCardHealthFlat, // Targeting 설정으로 선택된 카드의 체력을 직접 증감합니다.
    AdjustTargetCardAttackFlat // Targeting 설정으로 선택된 카드의 공격력을 직접 증감합니다.
}

/// <summary>
/// 발동 조건을 데이터로 지정하는 구조체입니다.
/// conditionType을 통해 발동 타이밍을 제어하고, 필요한 경우 hpThreshold/turnInterval/startTurnOffset 값을 입력합니다.
/// countEnemyTurns가 true이면 적 턴까지 포함해 턴 간격을 계산합니다.
/// </summary>
[Serializable]
public sealed class RelicTriggerCondition
{
    [SerializeField] private RelicTriggerConditionType conditionType = RelicTriggerConditionType.Always;
    [Tooltip("체력이 어느정도 남았는지 확인 PlayerHpBelowOrEqua과 함께 사용")]
    [SerializeField] private int hpThreshold = 0;
    [Tooltip("N턴마다 발동 EveryNthTurn함께사용")]
    [SerializeField] private int turnInterval = 1;
    [Tooltip("N턴마다 발동시 효과 발동을 몇턴뒤로 할지 정합니다")]
    [SerializeField] private int startTurnOffset = 0;
    [SerializeField] private bool countEnemyTurns = false;

    public RelicTriggerConditionType ConditionType => conditionType;
    public int HpThreshold => hpThreshold;
    public int TurnInterval => turnInterval;
    public int StartTurnOffset => startTurnOffset;
    public bool CountEnemyTurns => countEnemyTurns;
}

/// <summary>
/// 유물 발동 조건 종류입니다.
/// - Always: 언제나 발동.
/// - PlayerTurnOnly / EnemyTurnOnly: 해당 진영의 턴에서만 발동.
/// - PlayerHpBelowOrEqual: 플레이어 HP가 특정 값 이하일 때만 발동.
/// - EveryNthTurn: 매 N번째 턴마다 발동(Interval/Offset 조정 가능).
/// </summary>
public enum RelicTriggerConditionType
{
    Always = 0,
    PlayerTurnOnly,
    EnemyTurnOnly,
    PlayerHpBelowOrEqual,
    EveryNthTurn
}

/// <summary>
/// 지속 시간(턴 단위)을 정의하는 설정입니다.
/// useDuration이 true이면 turnCount 동안 효과를 유지하며, countEnemyTurns로 감소 기준을 결정합니다.
/// </summary>
[Serializable]
public sealed class RelicDurationSettings
{
    [SerializeField] private bool useDuration = false;
    [Tooltip("지속시간을 나타냅니다 countenemyturns체크시 상대턴까지 셉니다")]
    [SerializeField] private int turnCount = 1;
    [SerializeField] private bool countEnemyTurns = false;

    public bool UseDuration => useDuration;
    public int TurnCount => Mathf.Max(1, turnCount);
    public bool CountEnemyTurns => countEnemyTurns;
}

/// <summary>
/// 쿨다운(턴 단위)을 정의하는 설정입니다.
/// useCooldown이 true이면 turnCount 만큼 대기 후 다시 발동할 수 있습니다.
/// countEnemyTurns가 true이면 적 턴도 쿨다운 감소에 포함됩니다.
/// </summary>
[Serializable]
public sealed class RelicCooldownSettings
{
    [SerializeField] private bool useCooldown = false;
    [Tooltip("몇턴동안 쿨다운 될지 나타냅니다")]
    [SerializeField] private int turnCount = 1;
    [SerializeField] private bool countEnemyTurns = false;

    public bool UseCooldown => useCooldown;
    public int TurnCount => Mathf.Max(1, turnCount);
    public bool CountEnemyTurns => countEnemyTurns;
}

/// <summary>
/// 카드 타겟팅 규칙입니다.
/// mode로 무작위/전체 등을 고르고, ownerFilter로 플레이어/적 핸드를 지정할 수 있습니다.
/// randomCount는 무작위 선택 시 뽑을 카드 수를 의미합니다.
/// </summary>
[Serializable]
public sealed class RelicTargetingSettings
{
    [SerializeField] private RelicTargetingMode mode = RelicTargetingMode.None;
    [SerializeField] private RelicTargetingOwner ownerFilter = RelicTargetingOwner.PlayerHand;
    [Tooltip("몇개의 카드에 적용시킬지 allowduplication은 중복을 나타냅니다")]
    [SerializeField] private int randomCount = 1;
    [SerializeField] private bool allowDuplicates = false;

    public RelicTargetingMode Mode => mode;
    public RelicTargetingOwner OwnerFilter => ownerFilter;
    public int RandomCount => Mathf.Max(1, randomCount);
    public bool AllowDuplicates => allowDuplicates;
}

/// <summary>타겟팅 모드.</summary>
public enum RelicTargetingMode
{
    None = 0,
    RandomHandCard,
    AllHandCards
}

/// <summary>타겟팅 시 어떤 진영의 카드를 대상으로 할지 정의합니다.</summary>
public enum RelicTargetingOwner
{
    PlayerHand = 0,
    EnemyHand,
    AnyHand
}

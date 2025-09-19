using UnityEngine;

/// <summary>
/// 특전 값이 추가형인지 비율형인지 구분합니다.
/// </summary>
public enum ValueKind { Flat, Percentage }
/// <summary>
/// 특전이 누적되는 방식을 정의합니다.
/// </summary>
public enum StackingMode { Additive, Multiplicative }

/// <summary>
/// 특전 정의와 스택 방식, 비용을 저장하는 스크립터블 오브젝트입니다.
/// </summary>
[CreateAssetMenu(menuName = "Progression/New Perk", fileName = "NewPerk")]
public class PerkDefinition : ScriptableObject
{
    /// <summary>
    /// 특전 식별자(예: PERK_STARTING_GOLD_FLAT).
    /// </summary>
    public string Id;               // e.g., PERK_STARTING_GOLD_FLAT
    /// <summary>
    /// UI에 표시될 특전 이름입니다.
    /// </summary>
    public string DisplayName;
    /// <summary>
    /// 특전 설명입니다.
    /// </summary>
    [TextArea] public string Description;
    /// <summary>
    /// 특전 한 레벨당 소모 포인트입니다.
    /// </summary>
    public int Cost = 1;
    /// <summary>
    /// 특전의 최대 레벨입니다.
    /// </summary>
    public int MaxLevel = 1;

    // 효과 정의
    /// <summary>
    /// 집계 시 사용할 효과 키(예: STARTING_GOLD).
    /// </summary>
    public string EffectKey;        // e.g., STARTING_GOLD
    public StackingMode StackingMode;
    public ValueKind Kind;
    /// <summary>
    /// 레벨당 증감 값입니다.
    /// </summary>
    public float PerLevelValue = 1f;
}

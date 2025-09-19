using UnityEngine;

/// <summary>
/// 업적의 메타 정보를 정의하는 스크립터블 오브젝트입니다.
/// </summary>
[CreateAssetMenu(menuName = "Progression/New Achievement", fileName = "NewAchievement")]
public class AchievementDefinition : ScriptableObject
{
    /// <summary>
    /// 업적 식별자(예: ACH_FIRST_WIN).
    /// </summary>
    public string Id;               // e.g., ACH_FIRST_WIN
    /// <summary>
    /// UI에 노출될 업적 이름입니다.
    /// </summary>
    public string DisplayName;
    /// <summary>
    /// 업적 설명입니다.
    /// </summary>
    [TextArea] public string Description;
    /// <summary>
    /// 숨김 업적인지 여부입니다.
    /// </summary>
    public bool Hidden;
    /// <summary>
    /// 달성 시 지급할 특전 포인트입니다.
    /// </summary>
    public int PointsReward = 1;
    /// <summary>
    /// 진행형 업적의 목표치(1이면 이진 업적을 의미).
    /// </summary>
    public int ProgressTarget = 1; // 1 for binary achievements
}

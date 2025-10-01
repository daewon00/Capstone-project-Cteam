using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 업적의 메타 정보를 정의하는 스크립터블 오브젝트입니다.
/// </summary>
[CreateAssetMenu(menuName = "Progression/New Achievement", fileName = "NewAchievement")]
public class AchievementDefinition : ScriptableObject
{
    [System.Serializable]
    public class Tier
    {
        [Min(1)] public int goal = 1;
        public TierReward reward = new TierReward();
        [Tooltip("필수가 아니지만, 토스트/UI에 노출할 티어명이나 설명을 작성할 수 있습니다.")]
        public string displayName;
        [Tooltip("이 티어 달성 시 토스트를 띄울지 여부 (기본: true)")]
        public bool announce = true;
    }

    [System.Serializable]
    public class TierReward
    {
        [Tooltip("티어 달성 시 지급할 특전 포인트")] public int perkPoints;
        [Tooltip("추가 커스텀 보상 타입(확장용). 예: Gold, Item, Deck 등")]
        public string rewardType;
        [Tooltip("커스텀 보상 타입에 필요한 파라미터 문자열.")]
        public string rewardPayload;
    }

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
    /// 최종 티어 달성 시 지급할 기본 특전 포인트(티어 보상이 별도 설정되어 있으면 추가로 지급됩니다).
    /// </summary>
    public int PointsReward = 1;
    /// <summary>
    /// 진행형 업적의 목표치(1이면 최종 티어를 의미). 티어 목록이 비어 있으면 이 값이 기본 목표로 사용됩니다.
    /// </summary>
    public int ProgressTarget = 1; // fallback for binary achievements

    [Tooltip("티어 단위 목표 및 보상 구성. 비어 있으면 ProgressTarget만 사용합니다.")]
    public List<Tier> Tiers = new List<Tier>();

    /// <summary>
    /// 최종 목표치(티어가 존재하면 마지막 티어 목표)를 반환합니다.
    /// </summary>
    public int GetFinalGoal()
    {
        if (Tiers != null && Tiers.Count > 0)
        {
            return Mathf.Max(1, Tiers[Tiers.Count - 1].goal);
        }
        return Mathf.Max(1, ProgressTarget);
    }
}

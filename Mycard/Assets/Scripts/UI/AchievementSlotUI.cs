using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 개별 업적 정보를 표시하고 진행도에 따라 뷰를 업데이트하는 슬롯 UI입니다.
/// </summary>
public class AchievementSlotUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject unlockedBadge;
    [SerializeField] private TMP_Text unlockedAtText;
    [SerializeField] private TMP_Text cumulativeRewardText;
    [SerializeField] private TMP_Text nextRewardText;
    [SerializeField] private GameObject newTag;
    [SerializeField] private TMP_Text tierText;

    /// <summary>
    /// 업적 정의와 진행도를 받아 슬롯을 초기화합니다.
    /// </summary>
    public void Init(AchievementDefinition def, AchievementTierProgressInfo info, bool isNew, int newlyUnlockedTier)
    {
        var progress = info.Progress;
        int totalTiers = Mathf.Max(1, info.TotalTiers);
        int unlockedTierCount = Mathf.Clamp(progress.HighestTierUnlocked, 0, totalTiers);
        bool finalCompleted = info.IsFinalTierCompleted || progress.IsUnlocked;

        string displayName = def.Hidden && !finalCompleted ? "???" : def.DisplayName;
        string description = def.Hidden && !finalCompleted ? "???" : def.Description;

        if (nameText) nameText.text = displayName;
        if (descText) descText.text = description;
        if (rewardText) rewardText.text = $"최종 보상: +{Mathf.Max(0, def.PointsReward)} pt";

        // 슬라이더 및 기본 진행 텍스트
        int currentTarget = Mathf.Max(1, info.CurrentTierTarget);
        int currentValue = Mathf.Clamp(info.ProgressWithinCurrentTier, 0, currentTarget);

        if (progressSlider)
        {
            bool showSlider = !finalCompleted && info.HasNextTier && currentTarget > 0;
            progressSlider.gameObject.SetActive(showSlider);
            if (showSlider)
            {
                progressSlider.minValue = 0;
                progressSlider.maxValue = currentTarget;
                progressSlider.value = currentValue;
            }
        }

        if (progressText)
        {
            if (finalCompleted)
            {
                progressText.gameObject.SetActive(true);
                progressText.text = $"{progress.Progress} / {info.CurrentTierGoal} (완료)";
            }
            else
            {
                progressText.gameObject.SetActive(true);
                progressText.text = $"{currentValue} / {currentTarget}";
            }
        }

        if (unlockedBadge) unlockedBadge.SetActive(finalCompleted);

        if (unlockedAtText)
        {
            if (progress.IsUnlocked && !string.IsNullOrEmpty(progress.UnlockedAtUtc))
            {
                unlockedAtText.text = System.DateTime.TryParse(progress.UnlockedAtUtc, out var dt)
                    ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : progress.UnlockedAtUtc;
            }
            else
            {
                unlockedAtText.text = string.Empty;
            }
        }

        if (cumulativeRewardText)
        {
            cumulativeRewardText.text = $"누적 보상: +{info.CumulativeRewardPoints} pt";
        }

        if (nextRewardText)
        {
            if (finalCompleted)
            {
                nextRewardText.text = "추가 보상 없음";
            }
            else
            {
                int nextReward = Mathf.Max(0, info.NextRewardPoints);
                string label = info.HasNextTier ? "다음 보상" : "최종 보상";
                nextRewardText.text = nextReward > 0
                    ? $"{label}: +{nextReward} pt"
                    : $"{label}: 없음";
            }
        }

        if (tierText)
        {
            if (totalTiers <= 1)
            {
                tierText.gameObject.SetActive(false);
            }
            else
            {
                tierText.gameObject.SetActive(true);
                StringBuilder stars = new StringBuilder(totalTiers);
                for (int i = 0; i < totalTiers; i++)
                {
                    stars.Append(i < unlockedTierCount ? '★' : '☆');
                }

                if (newlyUnlockedTier > 0)
                {
                    tierText.text = $"티어: {stars}  ↑ {newlyUnlockedTier}/{totalTiers}";
                }
                else if (finalCompleted)
                {
                    tierText.text = $"티어: {stars} (완료)";
                }
                else
                {
                    tierText.text = $"티어: {stars}";
                }
            }
        }

        if (newTag) newTag.SetActive(isNew || newlyUnlockedTier > 0);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save;

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
    [SerializeField] private GameObject newTag;

    /// <summary>
    /// 업적 정의와 진행도를 받아 슬롯을 초기화합니다.
    /// </summary>
    public void Init(AchievementDefinition def, AchievementProgress prog, bool isNew)
    {
        bool hiddenAndLocked = def.Hidden && (prog == null || !prog.IsUnlocked);
        string displayName = hiddenAndLocked ? "???" : def.DisplayName;
        string desc = hiddenAndLocked ? "???" : def.Description;

        if (nameText) nameText.text = displayName;
        if (descText) descText.text = desc;
        if (rewardText) rewardText.text = $"+{def.PointsReward} pt";

        int target = Mathf.Max(1, def.ProgressTarget);
        int value = Mathf.Clamp(prog?.Progress ?? 0, 0, target);
        bool unlocked = prog?.IsUnlocked == true;

        if (progressSlider)
        {
            progressSlider.gameObject.SetActive(!unlocked);
            progressSlider.minValue = 0;
            progressSlider.maxValue = target;
            progressSlider.value = value;
        }
        if (progressText)
        {
            progressText.gameObject.SetActive(!unlocked);
            progressText.text = $"{value} / {target}";
        }
        if (unlockedBadge) unlockedBadge.SetActive(unlocked);
        if (unlockedAtText)
        {
            if (unlocked && !string.IsNullOrEmpty(prog.UnlockedAtUtc))
                unlockedAtText.text = System.DateTime.TryParse(prog.UnlockedAtUtc, out var dt) ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : prog.UnlockedAtUtc;
            else
                unlockedAtText.text = string.Empty;
        }
        if (newTag) newTag.SetActive(isNew);
    }
}


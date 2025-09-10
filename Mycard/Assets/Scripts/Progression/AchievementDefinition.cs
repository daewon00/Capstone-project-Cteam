using UnityEngine;

[CreateAssetMenu(menuName = "Progression/New Achievement", fileName = "NewAchievement")]
public class AchievementDefinition : ScriptableObject
{
    public string Id;               // e.g., ACH_FIRST_WIN
    public string DisplayName;
    [TextArea] public string Description;
    public bool Hidden;
    public int PointsReward = 1;
    public int ProgressTarget = 1; // 1 for binary achievements
}


using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab-side view for achievement toast. Designer-friendly:
/// drop this on root of prefab and wire fields in Inspector.
/// </summary>
public sealed class AchievementToastView : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup CanvasGroup => canvasGroup;

    public void Bind(MetaEvents.AchievementUnlockedPayload p, string titleOverride = null, string subtitleOverride = null, string secondaryOverride = null)
    {
        if (titleText)
        {
            var title = titleOverride ?? (!string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : p.AchievementId);
            titleText.text = title;
        }

        if (subtitleText)
        {
            var subtitle = subtitleOverride ?? (!string.IsNullOrEmpty(p.Description) ? p.Description : string.Empty);
            if (string.IsNullOrEmpty(subtitle)) subtitleText.gameObject.SetActive(false);
            else subtitleText.text = subtitle;
        }

        if (pointsText)
        {
            if (!string.IsNullOrEmpty(secondaryOverride))
            {
                pointsText.text = secondaryOverride;
            }
            else if (p.Points > 0)
            {
                pointsText.text = $"+{Mathf.Max(0, p.Points)}";
            }
            else if (p.TierCount > 1)
            {
                int tierIndex = p.TierIndex <= 0 ? 1 : p.TierIndex;
                int tierCount = p.TierCount <= 0 ? 1 : p.TierCount;
                pointsText.text = $"Tier {tierIndex}/{tierCount}";
            }
            else
            {
                pointsText.gameObject.SetActive(false);
            }
        }
        // icon is optional: if left wired with a default sprite in prefab, it shows as-is.
        // If designers want per-achievement icon later, payload can be extended to include a sprite id.
    }
}

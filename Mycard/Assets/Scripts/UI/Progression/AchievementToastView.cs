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

    public void Bind(MetaEvents.AchievementUnlockedPayload p)
    {
        if (titleText) titleText.text = string.IsNullOrEmpty(p.DisplayName) ? p.AchievementId : p.DisplayName;
        if (subtitleText)
        {
            if (!string.IsNullOrEmpty(p.Description)) subtitleText.text = p.Description;
            else subtitleText.gameObject.SetActive(false);
        }
        if (pointsText)
        {
            pointsText.text = $"+{Mathf.Max(0, p.Points)}";
        }
        // icon is optional: if left wired with a default sprite in prefab, it shows as-is.
        // If designers want per-achievement icon later, payload can be extended to include a sprite id.
    }
}


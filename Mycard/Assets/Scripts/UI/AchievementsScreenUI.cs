using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

/// <summary>
/// 업적 목록 화면을 구성하고 새로 해금된 업적을 강조 표시하는 UI 컨트롤러입니다.
/// </summary>
public class AchievementsScreenUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private AchievementSlotUI slotPrefab;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect; // optional: auto-found if not bound

    private IAchievementService _achievements;
    private string _profileId = "P1";
    private bool _initialized;

    private List<AchievementDefinition> _defs = new();
    private Dictionary<string, AchievementTierProgressInfo> _displayInfo = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _newlyAchievements = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _newTierUnlocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 필요한 서비스와 UI 바인딩을 지연 초기화합니다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _achievements = ServiceRegistry.GetRequired<IAchievementService>();
        _profileId = GameContext.I != null ? GameContext.I.ProfileId : "P1";

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (scrollRect == null)
        {
            // Auto-bind first ScrollRect under screenRoot if not wired in Inspector
            var root = screenRoot != null ? screenRoot.transform : transform;
            scrollRect = root.GetComponentInChildren<ScrollRect>(true);
        }

        if (screenRoot) screenRoot.SetActive(false);
        _initialized = true;
    }

    /// <summary>
    /// 업적 화면을 표시하고 내용을 갱신합니다.
    /// </summary>
    public void Show()
    {
        EnsureInitialized();
        if (screenRoot) screenRoot.SetActive(true);
        RefreshUI();
    }

    /// <summary>
    /// 업적 화면을 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (screenRoot) screenRoot.SetActive(false);
    }

    /// <summary>
    /// 업적 정의와 진행도 스냅샷을 새로 로드하여 슬롯을 재구성합니다.
    /// </summary>
    private void RefreshUI()
    {
        EnsureInitialized();
        if (slotsContainer == null || slotPrefab == null)
        {
            Debug.LogWarning("[AchievementsScreenUI] Missing bindings");
            return;
        }

        _defs = _achievements.GetAllDefinitions().ToList();
        var infoSnapshot = _achievements.GetTierInfoSnapshot(_profileId);
        _displayInfo = infoSnapshot.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        _newlyAchievements = new HashSet<string>(_achievements.GetNewlyUnlockedSinceLastFlush(consume: true) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        _newTierUnlocks.Clear();
        var tierUnlocks = _achievements.GetNewlyUnlockedTiers(consume: true);
        if (tierUnlocks != null)
        {
            foreach (var info in tierUnlocks)
            {
                if (string.IsNullOrEmpty(info.AchievementId)) continue;
                _newTierUnlocks[info.AchievementId] = Mathf.Max(_newTierUnlocks.TryGetValue(info.AchievementId, out var cur) ? cur : 0, info.TierIndex);
            }
        }

        // Clear children
        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(slotsContainer.GetChild(i).gameObject);
        }

        // Stats
        int unlockedCount = 0;
        foreach (var def in _defs)
        {
            if (_displayInfo.TryGetValue(def.Id, out var info) && (info.Progress.IsUnlocked || info.IsFinalTierCompleted))
            {
                unlockedCount++;
            }
        }

        // Sort: New → 진행도 높은 순 → 완료 → 이름
        var sorted = _defs
            .OrderByDescending(d => _newlyAchievements.Contains(d.Id) || _newTierUnlocks.ContainsKey(d.Id))
            .ThenByDescending(d =>
            {
                if (!_displayInfo.TryGetValue(d.Id, out var info)) return 0f;
                if (info.IsFinalTierCompleted) return 0f;
                return info.CurrentTierTarget > 0
                    ? (float)info.ProgressWithinCurrentTier / info.CurrentTierTarget
                    : 0f;
            })
            .ThenByDescending(d => _displayInfo.TryGetValue(d.Id, out var info) && info.IsFinalTierCompleted)
            .ThenBy(d => d.DisplayName);

        foreach (var def in sorted)
        {
            if (!_displayInfo.TryGetValue(def.Id, out var info)) continue;
            bool isNewAchievement = _newlyAchievements.Contains(def.Id);
            _newTierUnlocks.TryGetValue(def.Id, out var newTierReached);
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.Init(def, info, isNewAchievement, newTierReached);
        }

        // Refresh layout + reset scroll to top
        try
        {
            Canvas.ForceUpdateCanvases();
            if (slotsContainer is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f; // top
            }
        }
        catch { }

        if (summaryText)
        {
            summaryText.text = $"총 업적: {_defs.Count} / 달성: {unlockedCount}";
        }
    }
}

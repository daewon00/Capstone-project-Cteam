using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using Game.Save;
using System; // for StringComparer
using UnityEngine.UI;

public class AchievementsScreenUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private AchievementSlotUI slotPrefab;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [SerializeField] private ScrollRect scrollRect; // optional: auto-found if not bound

    private IAchievementService _achievements;
    private IDatabase _db;
    private string _profileId = "P1";
    private bool _initialized;

    private List<AchievementDefinition> _defs = new();
    private Dictionary<string, AchievementProgress> _progress = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _newly = new(StringComparer.OrdinalIgnoreCase);

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _achievements = ServiceRegistry.GetRequired<IAchievementService>();
        _db = ServiceRegistry.GetRequired<IDatabase>();
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

    public void Show()
    {
        EnsureInitialized();
        if (screenRoot) screenRoot.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        if (screenRoot) screenRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        EnsureInitialized();
        if (slotsContainer == null || slotPrefab == null) { Debug.LogWarning("[AchievementsScreenUI] Missing bindings"); return; }

        _defs = _achievements.GetAllDefinitions().ToList();
        var snapshot = _achievements.GetProgressSnapshot(_profileId);
        _progress = snapshot.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        _newly = new HashSet<string>(_achievements.GetNewlyUnlockedSinceLastFlush() ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        // Clear children
        for (int i = slotsContainer.childCount - 1; i >= 0; i--) Destroy(slotsContainer.GetChild(i).gameObject);

        // Stats
        int unlockedCount = 0;
        foreach (var d in _defs)
        {
            if (_progress.TryGetValue(d.Id, out var p) && p.IsUnlocked) unlockedCount++;
        }

        // Sort: New → Locked(by ratio desc) → Unlocked → Name
        var sorted = _defs
            .OrderByDescending(d => _newly.Contains(d.Id))
            .ThenByDescending(d =>
            {
                _progress.TryGetValue(d.Id, out var p);
                bool unlocked = p?.IsUnlocked == true;
                return unlocked ? 0 : (float)(p?.Progress ?? 0) / Mathf.Max(1, d.ProgressTarget);
            })
            .ThenByDescending(d => _progress.TryGetValue(d.Id, out var p) && (p?.IsUnlocked == true))
            .ThenBy(d => d.DisplayName);

        foreach (var def in sorted)
        {
            _progress.TryGetValue(def.Id, out var prog);
            bool isNew = _newly.Contains(def.Id);
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.Init(def, prog, isNew);
        }

        // Refresh layout + reset scroll to top
        try
        {
            Canvas.ForceUpdateCanvases();
            var rt = slotsContainer as RectTransform;
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                // Some layout setups need two passes
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

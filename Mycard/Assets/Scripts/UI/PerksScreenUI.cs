using UnityEngine;
using TMPro;
using System.Linq;
using Game.Save;

public class PerksScreenUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject screenRoot; // 전체 켜기/끄기
    [SerializeField] private Transform slotsContainer; // ScrollView Content
    [SerializeField] private PerkSlotUI perkSlotPrefab; // 슬롯 프리팹
    [SerializeField] private TextMeshProUGUI totalPointsText; // 보유 포인트 표시
    [SerializeField] private TextMeshProUGUI noticeText; // "다음 런부터 적용" 안내(선택)
    [SerializeField] private TextMeshProUGUI summaryText; // 변경 요약 표시(선택)
    [SerializeField] private UnityEngine.UI.Button applyButton;    // 적용(커밋)
    [SerializeField] private UnityEngine.UI.Button resetAllButton; // 전체 초기화(스테이징 0)
    [SerializeField] private UnityEngine.UI.Button closeButton;    // 닫기(항상 활성)

    private IPerkService _perkService;
    private IDatabase _database;
    private string _profileId = "P1";
    private bool _initialized;

    // Staging: 장바구니 (PerkId -> 목표 레벨)
    private System.Collections.Generic.Dictionary<string, int> _stagedLevels = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private System.Collections.Generic.Dictionary<string, int> _currentLevelsCache = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private System.Collections.Generic.Dictionary<string, PerkDefinition> _defsCache = new System.Collections.Generic.Dictionary<string, PerkDefinition>(System.StringComparer.OrdinalIgnoreCase);

    // 지연 초기화: 화면이 처음 열릴 때 필요한 의존성/텍스트를 준비합니다.
    private void EnsureInitialized()
    {
        if (_initialized) return;
        // 필수 서비스는 GetRequired로 즉시 검출
        _perkService = ServiceRegistry.GetRequired<IPerkService>();
        _database = ServiceRegistry.GetRequired<IDatabase>();
        _profileId = GameContext.I != null ? GameContext.I.ProfileId : "P1";

        if (noticeText != null)
            noticeText.text = "특전 효과는 다음 런부터 적용됩니다.";

        if (screenRoot != null) screenRoot.SetActive(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var dup = FindObjectsOfType<PerksScreenUI>();
        if (dup != null && dup.Length > 1)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[PerksScreenUI] Multiple instances detected: {dup.Length}. Ensure only one is active in the scene.");
            for (int i = 0; i < dup.Length; i++)
            {
                sb.AppendLine($"  - #{i + 1}: {GetHierarchyPath(dup[i].transform)}");
            }
            GameLog.Warn(sb.ToString());
        }
#endif

        // 버튼 리스너
        if (applyButton)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(OnClickApply);
        }
        if (resetAllButton)
        {
            resetAllButton.onClick.RemoveAllListeners();
            resetAllButton.onClick.AddListener(OnClickResetAll);
        }
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        _initialized = true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        var cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
#endif

    public void Show()
    {
        EnsureInitialized();
        if (screenRoot != null) screenRoot.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        // 닫을 때는 장바구니를 폐기하여 다음에 열면 항상 DB 상태에서 시작
        _stagedLevels.Clear();
        if (screenRoot != null) screenRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        EnsureInitialized();
        // 방어 코드
        if (_perkService == null || _database == null || slotsContainer == null || perkSlotPrefab == null)
        {
            GameLog.Warn("[PerksScreenUI] Missing bindings or services.");
            return;
        }

        // 정의/현재 상태/스테이징 초기화(초입 또는 취소 후)
        var defs = _perkService.GetAllDefinitions();
        _defsCache = defs.ToDictionary(d => d.Id, d => d, System.StringComparer.OrdinalIgnoreCase);

        _currentLevelsCache = _perkService.GetAllocations(_profileId)
            .ToDictionary(a => a.PerkId, a => a.Level, System.StringComparer.OrdinalIgnoreCase);

        var profile = _database.LoadProfile(_profileId);
        int currentPoints = profile?.UnspentPerkPoints ?? 0;

        // 스테이징은 변경된 키만 포함할 수 있습니다. 누락된 키는 현재 레벨을 의미합니다.

        // 최종 포인트 계산(스테이징 반영)
        int finalPoints = ComputeFinalPoints(currentPoints);
        int delta = finalPoints - currentPoints;

        // 1) 기존 슬롯 정리
        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
            Destroy(slotsContainer.GetChild(i).gameObject);

        // 3) UX 정렬: 고정 정렬(이름 오름차순). 위치가 변하지 않도록 유지
        var sorted = defs.OrderBy(d => d.DisplayName);

        // 4) 슬롯 생성
        foreach (var d in sorted)
        {
            var slot = Instantiate(perkSlotPrefab, slotsContainer);
            _currentLevelsCache.TryGetValue(d.Id, out int curLevel);
            int stagedLevel = _stagedLevels.ContainsKey(d.Id) ? _stagedLevels[d.Id] : curLevel;
            // 남은 포인트(스테이징 적용 후)를 전달하여 + 버튼 가능 여부 판단
            slot.Init(d, curLevel, stagedLevel, finalPoints, OnAdjustRequested);
        }

        // 5) 포인트 갱신
        if (totalPointsText != null)
            totalPointsText.text = $"보유 포인트: {currentPoints}";

        // 6) 요약/버튼 상태
        bool changed = defs.Any(def =>
        {
            _currentLevelsCache.TryGetValue(def.Id, out var cur);
            int st = _stagedLevels.ContainsKey(def.Id) ? _stagedLevels[def.Id] : cur;
            return cur != st;
        });

        if (summaryText != null)
        {
            var txt = changed ? $"최종 포인트: {currentPoints} → {finalPoints} ({(delta >= 0 ? "+" : "")}{delta})" : "변경 없음";
            summaryText.text = txt;
        }
        if (applyButton) applyButton.interactable = changed && finalPoints >= 0;
        if (resetAllButton) resetAllButton.interactable = true;
        if (closeButton) closeButton.interactable = true;       // 닫기: 항상 활성
    }

    private int ComputeFinalPoints(int currentPoints)
    {
        int totalCost = 0;
        int totalRefund = 0;
        foreach (var kv in _defsCache)
        {
            var id = kv.Key; var def = kv.Value;
            _currentLevelsCache.TryGetValue(id, out var cur);
            int st = _stagedLevels.ContainsKey(id) ? _stagedLevels[id] : cur;
            cur = Mathf.Max(0, cur);
            st = Mathf.Clamp(st, 0, Mathf.Max(0, def.MaxLevel));
            if (st > cur) totalCost += (st - cur) * Mathf.Max(0, def.Cost);
            else if (st < cur) totalRefund += (cur - st) * Mathf.Max(0, def.Cost);
        }
        return currentPoints - totalCost + totalRefund;
    }

    private void OnAdjustRequested(string perkId, int delta)
    {
        if (!_defsCache.TryGetValue(perkId, out var def)) return;
        _currentLevelsCache.TryGetValue(perkId, out var cur);
        int st = _stagedLevels.ContainsKey(perkId) ? _stagedLevels[perkId] : cur;
        int target = Mathf.Clamp(st + delta, 0, Mathf.Max(0, def.MaxLevel));

        // 포인트 검증: +일 때만 검사(–는 항상 허용)
        if (delta > 0)
        {
            // 현재 스테이징 상태에서 1레벨 추가에 필요한 포인트가 남아있는지 검사
            var profile = _database.LoadProfile(_profileId);
            int currentPoints = profile?.UnspentPerkPoints ?? 0;
            int beforeFinal = ComputeFinalPoints(currentPoints);
            if (beforeFinal < def.Cost)
            {
                GameLog.Info("[PerksScreenUI] Not enough points for +1");
                return;
            }
        }

        _stagedLevels[perkId] = target;
        RefreshUI();
    }

    private void OnClickApply()
    {
        EnsureInitialized();
        string error;
        // 인스턴스 중복/동시변경에 의한 참조 문제를 피하기 위해 사본을 전달합니다.
        var targets = new System.Collections.Generic.Dictionary<string, int>(_stagedLevels, System.StringComparer.OrdinalIgnoreCase);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[PerksScreenUI] Applying adjustments: profile={_profileId}, stagedKeys={targets.Count}");
#endif
        if (_perkService.ApplyAdjustments(_profileId, targets, out error))
        {
            // 성공: 스테이징 초기화 후 재로드
            _stagedLevels.Clear();
            RefreshUI();
        }
        else
        {
            GameLog.Warn($"[PerksScreenUI] Apply failed: {error}");
        }
    }

    private void OnClickResetAll()
    {
        EnsureInitialized();
        // 모든 정의 대상 0으로
        _stagedLevels.Clear();
        foreach (var id in _defsCache.Keys) _stagedLevels[id] = 0;
        RefreshUI();
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using Game.Save; // CardRuntimeState, CardLocation

// 역할: 덱 보기 UI 전체의 생명주기(열기/닫기)를 관리하고, 데이터를 로드하여 카드 목록을 생성한다.
[DisallowMultipleComponent]
public class DeckOverlayController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;            // 켜고 끌 전체 패널
    [SerializeField] private Transform contentParent;         // 카드들이 생성될 부모 위치 (Scroll View의 Content)
    [SerializeField] private DeckCardItemView cardItemPrefab; // 생성할 카드 UI의 원본 프리팹
    [SerializeField] private TMP_Text deckCountText;          // "내 덱 (N장)" 텍스트
    [SerializeField] private TMP_Text statusText;             // 안내 메시지("진행 중인 런이 없습니다" 등)
    [SerializeField] private bool clearOnHide = true;

    // --- 서비스 및 데이터 ---
    private IDatabase _database;
    private ICardCatalog _cardCatalog;
    private readonly List<CardRuntimeState> _currentDeck = new();
    private readonly List<DeckCardItemView> _spawned = new();
    private readonly List<DeckPreviewEntry> _previewEntries = new();

    private enum OverlayMode { None, RunDeck, Preview }
    private OverlayMode _mode = OverlayMode.None;
    private string _previewHeader;

    public enum FilterMode { All, Draw, Hand, Discard, Exhaust }
    public enum SortMode { PileOrder, CostThenName, NameThenCost }

    [Header("Filter/Sort")]
    [SerializeField] private FilterMode _filter = FilterMode.All;
    [SerializeField] private SortMode _sort = SortMode.PileOrder;

    void Awake()
    {
        // UI 기본 상태: 패널은 꺼둠
        if (panelRoot != null) panelRoot.SetActive(false);

        // 서비스는 나중(Show 시점)에 한 번 더 시도해 연결
        _database = ServiceRegistry.Get<IDatabase>();
        _cardCatalog = ServiceRegistry.Get<ICardCatalog>();
    }

    private bool PrepareForDisplay()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!enabled)
        {
            enabled = true;
        }

        if (panelRoot == null || cardItemPrefab == null || contentParent == null)
        {
            GameLog.Error("[DeckOverlay] 필수 참조가 비어있습니다. panelRoot/contentParent/cardItemPrefab 확인", this);
            return false;
        }

        panelRoot.SetActive(true);
        return true;
    }

    // --- 공개 API: 버튼에 연결 ---
    public void Show()
    {
        if (!PrepareForDisplay())
            return;

        _mode = OverlayMode.RunDeck;
        _previewHeader = null;
        _previewEntries.Clear();
        _filter = FilterMode.All;

        // 서비스 재확보(부트 순서 변동 대비)
        _database = _database ?? ServiceRegistry.Get<IDatabase>();
        _cardCatalog = _cardCatalog ?? ServiceRegistry.Get<ICardCatalog>();

        var runId = PlayerPrefs.GetString("lastRunId", string.Empty);
        if (string.IsNullOrEmpty(runId))
        {
            SetStatus("진행 중인 게임이 없습니다.");
            UpdateDeckCount(0);
            ClearContent();
            return;
        }

        if (_database == null)
        {
            SetStatus("데이터베이스 서비스를 찾을 수 없습니다.");
            UpdateDeckCount(0);
            ClearContent();
            return;
        }

        _currentDeck.Clear();
        try
        {
            var list = _database.LoadCardRuntimeStates(runId) ?? new List<CardRuntimeState>();
            _currentDeck.AddRange(list);
        }
        catch (System.SystemException e)
        {
            GameLog.Error($"[DeckOverlay] 덱 로드 실패: {e.Message}");
            SetStatus("덱을 불러오지 못했습니다.");
            UpdateDeckCount(0);
            ClearContent();
            return;
        }

        // 기본 정렬 우선순위는 코스트→이름
        _sort = SortMode.CostThenName;
        RebuildList();
    }

    /// <summary>
    /// 런 시작 전(동료 선택 등) 임시 데이터로 덱을 보여줄 때 호출합니다.
    /// </summary>
    public void ShowPreview(string companionDisplayName, IReadOnlyList<DeckPreviewEntry> entries)
    {
        if (!PrepareForDisplay())
            return;

        _mode = OverlayMode.Preview;
        _filter = FilterMode.All;
        _sort = SortMode.CostThenName;
        _previewHeader = string.IsNullOrWhiteSpace(companionDisplayName)
            ? "시작 덱"
            : $"{companionDisplayName} 시작 덱";

        _previewEntries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.CardId))
                    continue;
                _previewEntries.Add(new DeckPreviewEntry(entry.CardId, entry.Count, entry.Upgraded));
            }
        }

        _currentDeck.Clear();
        _cardCatalog = _cardCatalog ?? ServiceRegistry.Get<ICardCatalog>();
        RebuildList();
    }

    public void Hide()
    {
        if (clearOnHide)
            ClearContent();
        if (panelRoot != null)
            panelRoot.SetActive(false);
        _mode = OverlayMode.None;
        _previewHeader = null;
        _previewEntries.Clear();
    }

    public void SetFilterAll() { SetFilter(FilterMode.All); }
    public void SetFilterDraw() { SetFilter(FilterMode.Draw); }
    public void SetFilterHand() { SetFilter(FilterMode.Hand); }
    public void SetFilterDiscard() { SetFilter(FilterMode.Discard); }
    public void SetFilterExhaust() { SetFilter(FilterMode.Exhaust); }
    public void SetSortPileOrder() { SetSort(SortMode.PileOrder); }
    public void SetSortCostThenName() { SetSort(SortMode.CostThenName); }
    public void SetSortNameThenCost() { SetSort(SortMode.NameThenCost); }

    private void SetFilter(FilterMode f)
    {
        if (_mode == OverlayMode.Preview && f != FilterMode.All)
            return;
        if (_filter == f) return;
        _filter = f;
        if (panelRoot != null && panelRoot.activeInHierarchy) RebuildList();
    }

    private void SetSort(SortMode s)
    {
        if (_sort == s) return;
        _sort = s;
        if (panelRoot != null && panelRoot.activeInHierarchy) RebuildList();
    }

    // --- 내부 로직 ---
    private void RebuildList()
    {
        ClearContent();

        if (_mode == OverlayMode.Preview)
        {
            BuildPreviewList();
            return;
        }

        if (_currentDeck.Count == 0)
        {
            SetStatus("덱에 카드가 없습니다.");
            UpdateDeckCount(0);
            return;
        }

        var filtered = ApplyFilter(_currentDeck, _filter);
        var ordered = ApplySort(filtered, _sort);

        int spawned = 0;
        foreach (var state in ordered)
        {
            var so = _cardCatalog != null ? _cardCatalog.GetCardData(state.CardId) : null;
            if (so == null)
            {
                GameLog.Warn($"[DeckOverlay] CardId({state.CardId})에 대한 CardScriptableObject를 찾을 수 없습니다.");
            }
            var view = Instantiate(cardItemPrefab, contentParent);
            view.Bind(so, state);
            _spawned.Add(view);
            spawned++;
        }

        SetStatus(string.Empty);
        UpdateDeckCount(spawned);
    }

    private void BuildPreviewList()
    {
        if (_previewEntries.Count == 0)
        {
            SetStatus("시작 덱 정보가 없습니다.");
            UpdateDeckCount(0);
            return;
        }

        var ordered = ApplyPreviewSort(_previewEntries, _sort);
        int totalCards = 0;

        foreach (var entry in ordered)
        {
            var so = _cardCatalog != null ? _cardCatalog.GetCardData(entry.CardId) : null;
            if (so == null)
            {
                GameLog.Warn($"[DeckOverlay] 프리뷰 카드({entry.CardId})에 대한 CardScriptableObject를 찾을 수 없습니다.");
            }

            var view = Instantiate(cardItemPrefab, contentParent);
            view.Bind(so, cardState: null, groupedCount: Mathf.Max(1, entry.Count));
            _spawned.Add(view);
            totalCards += Mathf.Max(1, entry.Count);
        }

        SetStatus(string.Empty);
        UpdateDeckCount(totalCards);
    }

    private static IEnumerable<CardRuntimeState> ApplyFilter(IEnumerable<CardRuntimeState> source, FilterMode f)
    {
        switch (f)
        {
            case FilterMode.Draw:    return source.Where(c => c.Location == CardLocation.DrawPile);
            case FilterMode.Hand:    return source.Where(c => c.Location == CardLocation.Hand);
            case FilterMode.Discard: return source.Where(c => c.Location == CardLocation.DiscardPile);
            case FilterMode.Exhaust: return source.Where(c => c.Location == CardLocation.ExhaustPile);
            default:                 return source;
        }
    }

    private IEnumerable<CardRuntimeState> ApplySort(IEnumerable<CardRuntimeState> source, SortMode s)
    {
        switch (s)
        {
            case SortMode.CostThenName:
                return source.OrderBy(c => GetCost(c.CardId))
                             .ThenBy(c => GetName(c.CardId), System.StringComparer.Ordinal);
            case SortMode.NameThenCost:
                return source.OrderBy(c => GetName(c.CardId), System.StringComparer.Ordinal)
                             .ThenBy(c => GetCost(c.CardId));
            case SortMode.PileOrder:
            default:
                // DB 조회 시 OrderInPile DESC이므로, 그대로 두거나 필요 시 다시 정렬
                return source.OrderByDescending(c => c.OrderInPile);
        }
    }

    private IEnumerable<DeckPreviewEntry> ApplyPreviewSort(IEnumerable<DeckPreviewEntry> source, SortMode s)
    {
        switch (s)
        {
            case SortMode.CostThenName:
                return source.OrderBy(c => GetCost(c.CardId))
                             .ThenBy(c => GetName(c.CardId), System.StringComparer.Ordinal);
            case SortMode.NameThenCost:
                return source.OrderBy(c => GetName(c.CardId), System.StringComparer.Ordinal)
                             .ThenBy(c => GetCost(c.CardId));
            case SortMode.PileOrder:
            default:
                return source;
        }
    }

    private int GetCost(string cardId)
    {
        if (_cardCatalog == null) return int.MaxValue;
        var so = _cardCatalog.GetCardData(cardId);
        return so != null ? so.manaCost : int.MaxValue;
    }

    private string GetName(string cardId)
    {
        if (_cardCatalog == null) return cardId ?? string.Empty;
        var so = _cardCatalog.GetCardData(cardId);
        return so != null ? (so.cardName ?? string.Empty) : (cardId ?? string.Empty);
    }

    private void ClearContent()
    {
        if (_spawned.Count > 0)
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
        }
    }

    private void UpdateDeckCount(int count)
    {
        if (deckCountText == null)
            return;

        if (_mode == OverlayMode.Preview && !string.IsNullOrEmpty(_previewHeader))
        {
            deckCountText.text = $"{_previewHeader} ({count}장)";
        }
        else
        {
            deckCountText.text = $"내 덱 ({count}장)";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText == null) return;
        statusText.text = message ?? string.Empty;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}

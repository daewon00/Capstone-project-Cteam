using System;
using System.Collections.Generic;
using System.Linq;
using Game.Save;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 캠프파이어 등에서 강화 가능한 카드 목록을 표시하고 선택을 받아 확정합니다.
/// </summary>
public class DeckUpgradeSelectionPanel : MonoBehaviour
{
    public sealed class CardSelectionConfirmContext
    {
        public string Title;
        public string Guidance;
        public string ConfirmLabel;
        public string CancelLabel;
        public bool ShowUpgradePreview = true;
        public string BeforePreviewTitle;
        public string AfterPreviewTitle;
        public string CenterPreviewTitle;
        public bool? ShowCenterPreview;
        public TextAlignmentOptions? CenterPreviewAlignment;
        public bool UseCenterSlot;
    }

    [Header("List")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject cardItemPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Controls")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private TMP_Text headerLabel;

    [Header("Preview")]
    [SerializeField] private CardUpgradePreviewPanel previewPanel;

    private readonly List<CardDisplaySelectionItem> _spawnedItems = new();
    private readonly List<(CardRuntimeState state, CardScriptableObject card)> _candidates = new();

    private IDeckService _deckService;
    private ICardCatalog _cardCatalog;
    private CardDisplaySelectionItem _currentSelection;
    private Action<CardRuntimeState> _onConfirm;
    private Action _onCancel;
    private bool _isInitialized;
    // 단일 확인 모드 지원
    private bool _singleMode;
    private CardRuntimeState _singleSelectedState;
    private TMP_Text _confirmButtonLabel;
    private TMP_Text _cancelButtonLabel;
    private string _defaultConfirmLabel;
    private string _defaultCancelLabel;
    private string _defaultHeaderLabel;

    private void Awake()
    {
        // 리스트 UI는 더 이상 필수가 아니므로 초기 바인딩을 강제하지 않습니다.
        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancel);
        _confirmButtonLabel = ExtractButtonLabel(confirmButton);
        _cancelButtonLabel = ExtractButtonLabel(cancelButton);
        _defaultConfirmLabel = _confirmButtonLabel != null ? _confirmButtonLabel.text : string.Empty;
        _defaultCancelLabel = _cancelButtonLabel != null ? _cancelButtonLabel.text : string.Empty;
        ResolveHeaderLabelIfNeeded();
        _defaultHeaderLabel = headerLabel != null ? headerLabel.text : string.Empty;
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(HandleConfirm);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancel);
    }

    /// <summary>
    /// 패널을 초기화하고 표시합니다.
    /// </summary>
    /// <returns>강화 가능한 카드가 하나 이상이면 true, 아니면 false</returns>
    public bool Show(Action<CardRuntimeState> onConfirm, Action onCancel = null)
    {
        // 목록 모드는 현재 프리팹에서 제거되었습니다. 사용을 방지하고 조용히 폴백하도록 false 반환합니다.
        _singleMode = false;
        _singleSelectedState = null;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        GameLog.Warn("[DeckUpgradeSelection] 목록 모드는 더 이상 사용되지 않습니다. Show()는 false를 반환합니다.", this);
        HideImmediate();
        return false;
    }

    /// <summary>
    /// 외부에서 선택된 카드 1장을 전/후 프리뷰만으로 확인/취소하는 모드로 표시합니다.
    /// </summary>
    public bool ShowSingle(CardRuntimeState state, Action<CardRuntimeState> onConfirm, Action onCancel = null, CardSelectionConfirmContext context = null)
    {
        AcquireServices();
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        // 단일 모드 활성화: 목록 관련 바인딩 없이 프리뷰만 사용
        _singleMode = true;
        _singleSelectedState = state;
        bool showPreview = context?.ShowUpgradePreview ?? true;
        string guidance = context?.Guidance;

        if (!EnsureBindings())
        {
            GameLog.Error("[DeckUpgradeSelection] ShowSingle EnsureBindings 실패", this);
            HideImmediate();
            return false;
        }

        if (scrollRect != null) scrollRect.gameObject.SetActive(false);
        if (contentRoot != null) contentRoot.gameObject.SetActive(false);
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(false);

        _spawnedItems.Clear();
       _candidates.Clear();
       _currentSelection = null;
       previewPanel?.Clear();

        CardScriptableObject so = null;
        if (state != null && _cardCatalog != null)
            _cardCatalog.TryGetCardData(state.CardId, out so);

        if (so != null && state != null) previewPanel?.Show(so, state, showPreview, guidance, context);
        else previewPanel?.Clear();
        if (confirmButton != null) confirmButton.interactable = (state != null);
        ApplyButtonLabels(context);
        ApplyHeaderTitle(context);

        gameObject.SetActive(true);
        return true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ClearCandidates();
        _onConfirm = null;
        _onCancel = null;
        _currentSelection = null;
        previewPanel?.Clear();
        if (scrollRect != null) scrollRect.gameObject.SetActive(true);
        if (contentRoot != null) contentRoot.gameObject.SetActive(true);
        _singleMode = false;
        _singleSelectedState = null;
        RestoreDefaultLabels();
        RestoreHeaderTitle();
    }

    public void HideImmediate()
    {
        gameObject.SetActive(false);
        ClearCandidates();
        previewPanel?.Clear();
        _currentSelection = null;
        _onConfirm = null;
        _onCancel = null;
        if (scrollRect != null) scrollRect.gameObject.SetActive(true);
        if (contentRoot != null) contentRoot.gameObject.SetActive(true);
        _singleMode = false;
        _singleSelectedState = null;
        RestoreDefaultLabels();
        RestoreHeaderTitle();
    }

    private TMP_Text ExtractButtonLabel(Button button)
    {
        if (button == null) return null;
        return button.GetComponentInChildren<TMP_Text>(true);
    }

    private void ApplyButtonLabels(CardSelectionConfirmContext context)
    {
        if (context == null)
        {
            RestoreDefaultLabels();
            return;
        }

        if (_confirmButtonLabel != null)
        {
            _confirmButtonLabel.text = !string.IsNullOrEmpty(context.ConfirmLabel)
                ? context.ConfirmLabel
                : _defaultConfirmLabel;
        }

        if (_cancelButtonLabel != null)
        {
            _cancelButtonLabel.text = !string.IsNullOrEmpty(context.CancelLabel)
                ? context.CancelLabel
                : _defaultCancelLabel;
        }
    }

    private void RestoreDefaultLabels()
    {
        if (_confirmButtonLabel != null)
            _confirmButtonLabel.text = _defaultConfirmLabel;
        if (_cancelButtonLabel != null)
            _cancelButtonLabel.text = _defaultCancelLabel;
    }

    private void ApplyHeaderTitle(CardSelectionConfirmContext context)
    {
        ResolveHeaderLabelIfNeeded();
        if (headerLabel == null)
            return;

        if (context == null || string.IsNullOrEmpty(context.Title))
        {
            headerLabel.text = _defaultHeaderLabel;
            return;
        }

        headerLabel.text = context.Title;
    }

    private void RestoreHeaderTitle()
    {
        ResolveHeaderLabelIfNeeded();
        if (headerLabel != null)
        {
            headerLabel.text = _defaultHeaderLabel;
        }
    }

    private void ResolveHeaderLabelIfNeeded()
    {
        if (headerLabel != null)
            return;

        var previewTransform = previewPanel != null ? previewPanel.transform : null;
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text == null)
                continue;
            if (text == emptyLabel || text == _confirmButtonLabel || text == _cancelButtonLabel)
                continue;
            if (previewTransform != null && text.transform.IsChildOf(previewTransform))
                continue;

            headerLabel = text;
            break;
        }
    }

    private void AcquireServices()
    {
        if (_isInitialized)
            return;

        _deckService = ServiceRegistry.Get<IDeckService>();
        _cardCatalog = ServiceRegistry.Get<ICardCatalog>();
        _isInitialized = true;
    }

    private bool RefreshCandidates()
    {
        ClearCandidates();
        _currentSelection = null;
        previewPanel?.Clear();

        if (!EnsureBindings())
        {
            GameLog.Error("[DeckUpgradeSelection] RefreshCandidates 시점에 EnsureBindings 실패", this);
            return false;
        }

        if (_deckService == null || _cardCatalog == null)
        {
            ShowEmptyMessage("덱 정보를 불러올 수 없습니다.");
            return false;
        }

        var cards = _deckService.GetAllCardsSnapshot();
        if (cards == null || cards.Count == 0)
        {
            ShowEmptyMessage("덱에 카드가 없습니다.");
            return false;
        }

        foreach (var state in cards)
        {
            if (!CardUpgradeRules.TryGetUpgradeable(state, _cardCatalog, out var cardData))
                continue;
            _candidates.Add((state, cardData));
        }

        if (_candidates.Count == 0)
        {
            ShowEmptyMessage("강화 가능한 카드가 없습니다.");
            GameLog.Warn("[DeckUpgradeSelection] 강화 후보가 없습니다.", this);
            return false;
        }

        if (!BuildList())
        {
            ShowEmptyMessage("강화 후보를 표시할 수 없습니다.");
            GameLog.Error($"[DeckUpgradeSelection] BuildList 실패 - contentRoot={(contentRoot ? contentRoot.name : "null")}, cardItemPrefab={(cardItemPrefab ? cardItemPrefab.name : "null")}", this);
            return false;
        }

        UpdateConfirmState();
        return true;
    }

    private bool BuildList()
    {
        if (contentRoot == null || cardItemPrefab == null)
        {
        GameLog.Error("[DeckUpgradeSelection] 카드 리스트를 생성할 수 없습니다. contentRoot/cardItemPrefab을 확인하세요.", this);
            return false;
        }

        GameLog.Info($"[DeckUpgradeSelection] BuildList 시작 - 후보 {_candidates.Count}개, contentRoot={contentRoot.name}", this);

        foreach (var (state, card) in _candidates.OrderBy(c => GetSortKey(c.card), StringComparer.OrdinalIgnoreCase))
        {
            if (state == null)
            {
                GameLog.Warn("[DeckUpgradeSelection] null 상태 카드가 후보에 포함되어 무시합니다.", this);
                continue;
            }

            var instance = Instantiate(cardItemPrefab, contentRoot);
            if (instance == null)
            {
                GameLog.Error("[DeckUpgradeSelection] 카드 아이템 프리팹 인스턴스화 실패", this);
                continue;
            }

            var item = instance.GetComponent<CardDisplaySelectionItem>() ?? instance.AddComponent<CardDisplaySelectionItem>();
            GameLog.Info($"[DeckUpgradeSelection] 카드 생성 - cardId={state.CardId} instanceId={state.InstanceId}", item);
            item.Bind(card, state);
            item.Clicked += HandleItemClicked;
            _spawnedItems.Add(item);
        }

        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(false);
        }
        GameLog.Info($"[DeckUpgradeSelection] BuildList 완료 - contentChildren={contentRoot.childCount}", this);
        return _spawnedItems.Count > 0;
    }

    private void HandleItemClicked(CardDisplaySelectionItem item)
    {
        GameLog.Info($"[DeckUpgradeSelection] HandleItemClicked {(item != null ? item.RuntimeState?.InstanceId : "null")}", this);
        if (item == null)
            return;

        if (_currentSelection == item)
        {
            SetSelection(null);
        }
        else
        {
            SetSelection(item);
        }
    }

private void HandleConfirm()
    {
        if (_singleMode) {
            var selected = _singleSelectedState;
            GameLog.Info($"[DeckUpgradeSelection] Confirm(단일) 클릭 - selectedInstance={(selected != null ? selected.InstanceId : "null")}", this);
            var h = _onConfirm; Hide(); h?.Invoke(selected);
            return;
        }
        if (_currentSelection == null)
            return;

        var selectedState = _currentSelection.RuntimeState;
        GameLog.Info($"[DeckUpgradeSelection] Confirm 클릭 - selectedInstance={(selectedState != null ? selectedState.InstanceId : "null")}", this);
        var handler = _onConfirm;
        Hide();
        handler?.Invoke(selectedState);
    }

    private void HandleCancel()
    {
        var handler = _onCancel;
        Hide();
        handler?.Invoke();
    }

    private void SetSelection(CardDisplaySelectionItem item)
    {
        if (_currentSelection != null)
        {
            _currentSelection.SetSelected(false);
        }

        _currentSelection = item;

        if (_currentSelection != null)
        {
            _currentSelection.SetSelected(true);
            previewPanel?.Show(_currentSelection.CardData, _currentSelection.RuntimeState);
            GameLog.Info($"[DeckUpgradeSelection] 선택됨 -> {(_currentSelection.RuntimeState != null ? _currentSelection.RuntimeState.InstanceId : "null")}", this);
        }
        else
        {
            previewPanel?.Clear();
            GameLog.Info("[DeckUpgradeSelection] 선택 해제", this);
        }

        UpdateConfirmState();
    }

    private void UpdateConfirmState()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = _currentSelection != null;
            GameLog.Info($"[DeckUpgradeSelection] Confirm 버튼 interactable={confirmButton.interactable}", this);
        }
    }

    private void ShowEmptyMessage(string message)
    {
        if (emptyLabel != null)
        {
            emptyLabel.text = message ?? string.Empty;
            emptyLabel.gameObject.SetActive(true);
        }
    }

    private void ClearCandidates()
    {
        foreach (var item in _spawnedItems)
        {
            if (item == null) continue;
            item.Clicked -= HandleItemClicked;
            Destroy(item.gameObject);
        }
        _spawnedItems.Clear();
        _candidates.Clear();
        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(false);
            emptyLabel.text = string.Empty;
        }
    }

    private bool EnsureBindings()
    {
        // 단일 프리뷰 모드만 사용하므로 리스트 관련 바인딩은 필수가 아닙니다.
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }
        if (scrollRect != null && contentRoot == null)
        {
            contentRoot = scrollRect.content;
        }
        var cr = contentRoot != null ? contentRoot.name : "<none>";
        var pf = cardItemPrefab != null ? cardItemPrefab.name : "<none>";
        GameLog.Info($"[DeckUpgradeSelection] EnsureBindings 성공 - contentRoot={cr}, cardItemPrefab={pf}, singleMode={_singleMode}", this);
        return true;
    }

    private static string GetSortKey(CardScriptableObject card)
    {
        if (card == null)
            return string.Empty;
        return string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName;
    }
}

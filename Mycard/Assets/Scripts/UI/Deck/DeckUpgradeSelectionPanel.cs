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
    [Header("List")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject cardItemPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Controls")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text emptyLabel;

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

    private void Awake()
    {
        EnsureBindings();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancel);
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
        AcquireServices();
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (!EnsureBindings())
        {
            Debug.LogError($"[DeckUpgradeSelection] 초기 바인딩 실패 - contentRoot={(contentRoot ? contentRoot.name : "null")}, cardItemPrefab={(cardItemPrefab ? cardItemPrefab.name : "null")}, scrollRect={(scrollRect ? scrollRect.name : "null")}", this);
            HideImmediate();
            return false;
        }

        if (!RefreshCandidates())
        {
            Debug.LogWarning("[DeckUpgradeSelection] RefreshCandidates() 실패", this);
            HideImmediate();
            return false;
        }

        Debug.Log($"[DeckUpgradeSelection] Show() -> 패널 활성화 (activeBefore={gameObject.activeSelf})", this);
        gameObject.SetActive(true);
        Debug.Log($"[DeckUpgradeSelection] Show() -> 활성화 완료 (activeAfter={gameObject.activeSelf})", this);
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
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
    }

    public void HideImmediate()
    {
        gameObject.SetActive(false);
        ClearCandidates();
        previewPanel?.Clear();
        _currentSelection = null;
        _onConfirm = null;
        _onCancel = null;
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
            Debug.LogError("[DeckUpgradeSelection] RefreshCandidates 시점에 EnsureBindings 실패", this);
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
            Debug.LogWarning("[DeckUpgradeSelection] 강화 후보가 없습니다.", this);
            return false;
        }

        if (!BuildList())
        {
            ShowEmptyMessage("강화 후보를 표시할 수 없습니다.");
            Debug.LogError($"[DeckUpgradeSelection] BuildList 실패 - contentRoot={(contentRoot ? contentRoot.name : "null")}, cardItemPrefab={(cardItemPrefab ? cardItemPrefab.name : "null")}", this);
            return false;
        }

        UpdateConfirmState();
        return true;
    }

    private bool BuildList()
    {
        if (contentRoot == null || cardItemPrefab == null)
        {
        Debug.LogError("[DeckUpgradeSelection] 카드 리스트를 생성할 수 없습니다. contentRoot/cardItemPrefab을 확인하세요.", this);
            return false;
        }

        Debug.Log($"[DeckUpgradeSelection] BuildList 시작 - 후보 {_candidates.Count}개, contentRoot={contentRoot.name}", this);

        foreach (var (state, card) in _candidates.OrderBy(c => GetSortKey(c.card), StringComparer.OrdinalIgnoreCase))
        {
            if (state == null)
            {
                Debug.LogWarning("[DeckUpgradeSelection] null 상태 카드가 후보에 포함되어 무시합니다.", this);
                continue;
            }

            var instance = Instantiate(cardItemPrefab, contentRoot);
            if (instance == null)
            {
                Debug.LogError("[DeckUpgradeSelection] 카드 아이템 프리팹 인스턴스화 실패", this);
                continue;
            }

            var item = instance.GetComponent<CardDisplaySelectionItem>() ?? instance.AddComponent<CardDisplaySelectionItem>();
            Debug.Log($"[DeckUpgradeSelection] 카드 생성 - cardId={state.CardId} instanceId={state.InstanceId}", item);
            item.Bind(card, state);
            item.Clicked += HandleItemClicked;
            _spawnedItems.Add(item);
        }

        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(false);
        }
        Debug.Log($"[DeckUpgradeSelection] BuildList 완료 - contentChildren={contentRoot.childCount}", this);
        return _spawnedItems.Count > 0;
    }

    private void HandleItemClicked(CardDisplaySelectionItem item)
    {
        Debug.Log($"[DeckUpgradeSelection] HandleItemClicked {(item != null ? item.RuntimeState?.InstanceId : "null")}", this);
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
        if (_currentSelection == null)
            return;

        var selectedState = _currentSelection.RuntimeState;
        Debug.Log($"[DeckUpgradeSelection] Confirm 클릭 - selectedInstance={(selectedState != null ? selectedState.InstanceId : "null")}", this);
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
            Debug.Log($"[DeckUpgradeSelection] 선택됨 -> {(_currentSelection.RuntimeState != null ? _currentSelection.RuntimeState.InstanceId : "null")}", this);
        }
        else
        {
            previewPanel?.Clear();
            Debug.Log("[DeckUpgradeSelection] 선택 해제", this);
        }

        UpdateConfirmState();
    }

    private void UpdateConfirmState()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = _currentSelection != null;
            Debug.Log($"[DeckUpgradeSelection] Confirm 버튼 interactable={confirmButton.interactable}", this);
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
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                Debug.Log($"[DeckUpgradeSelection] scrollRect 자동 바인딩: {scrollRect.name}", this);
            }
        }

        if (scrollRect != null && contentRoot == null)
        {
            contentRoot = scrollRect.content;
            if (contentRoot != null)
            {
                Debug.Log($"[DeckUpgradeSelection] contentRoot 자동 바인딩: {contentRoot.name}", this);
            }
        }

        bool valid = true;
        if (contentRoot == null)
        {
            Debug.LogError("[DeckUpgradeSelection] contentRoot가 비어있습니다. 패널 Prefab 설정을 확인하세요.", this);
            valid = false;
        }

        if (cardItemPrefab == null)
        {
            Debug.LogError("[DeckUpgradeSelection] cardItemPrefab이 비어있습니다. CardDisplay 기반 프리팹을 연결해주세요.", this);
            valid = false;
        }

        if (valid)
        {
            Debug.Log($"[DeckUpgradeSelection] EnsureBindings 성공 - contentRoot={contentRoot.name}, cardItemPrefab={cardItemPrefab.name}", this);
        }

        return valid;
    }

    private static string GetSortKey(CardScriptableObject card)
    {
        if (card == null)
            return string.Empty;
        return string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName;
    }
}

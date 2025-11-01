using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save;

/// <summary>
/// 강화 가능한 카드만 덱 오버레이 스타일로 표시하는 1단계 선택 오버레이.
/// - 덱 오버레이와 동일한 레이아웃/룩앤필(디머 + 스크롤 + 그리드)을 사용합니다.
/// - 후보는 IDeckService에서 스냅샷을 받아 CardUpgradeRules로 필터링합니다.
/// - 아이템을 클릭하면 선택된 카드 상태/데이터를 콜백으로 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeCardOverlayController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;            // 전체 오버레이 루트(켜기/끄기)
    [SerializeField] private TMP_Text titleText;              // 상단 타이틀(기본: "강화 가능한 카드")
    [SerializeField] private ScrollRect scrollRect;           // 스크롤 뷰
    [SerializeField] private Transform contentParent;         // 아이템 생성 부모(ScrollRect.content)
    [SerializeField] private DeckCardItemView itemPrefab;     // 카드 아이템 프리팹(덱 오버레이와 동일 룩)
    [SerializeField] private Button closeButton;              // 닫기 버튼
    [SerializeField] private TMP_Text emptyLabel;             // 후보 없음 안내

    private IDeckService _deckService;
    private ICardCatalog _cardCatalog;
    private readonly List<DeckCardItemView> _spawned = new List<DeckCardItemView>();
    private Action<CardRuntimeState, CardScriptableObject> _onCardClicked;
    private Action _onClosed;

    private void Awake()
    {
        // 기본 비활성화로 시작
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // 닫기 버튼 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }
    }

    /// <summary>
    /// 오버레이를 표시하고, 카드 클릭 시 호출될 콜백을 등록합니다.
    /// </summary>
    public void Show(Action<CardRuntimeState, CardScriptableObject> onCardClicked, Action onClosed = null)
    {
        _onCardClicked = onCardClicked;
        _onClosed = onClosed;

        AcquireServices();

        if (!EnsureBindings())
        {
            Debug.LogError("[UpgradeOverlay] 필수 참조가 비어있습니다. panelRoot/scrollRect/contentParent/itemPrefab 확인", this);
            return;
        }

        if (titleText != null && string.IsNullOrEmpty(titleText.text))
        {
            titleText.text = "강화 가능한 카드";
        }

        var candidates = CollectUpgradeableCandidates();
        BuildList(candidates);

        if (panelRoot != null)
        {
            bool before = panelRoot.activeSelf;
            panelRoot.SetActive(true);
            Debug.Log($"[UpgradeOverlay] Show -> panel active: {before} → {panelRoot.activeSelf} (candidates={candidates.Count})", this);
        }

        // 스크롤 위치 초기화(상단)
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    /// <summary>
    /// 오버레이를 숨기고 생성된 아이템을 정리합니다.
    /// </summary>
    public void Hide()
    {
        ClearContent();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        _onCardClicked = null;
        var closed = _onClosed; _onClosed = null; // 재진입 방지
        try { closed?.Invoke(); } catch { }
    }

    /// <summary>
    /// 타이틀 텍스트를 동적으로 설정합니다.
    /// </summary>
    public void SetTitle(string title)
    {
        if (titleText != null)
            titleText.text = title ?? string.Empty;
    }

    private void HandleCloseClicked()
    {
        Hide();
    }

    private void AcquireServices()
    {
        _deckService = _deckService ?? ServiceRegistry.Get<IDeckService>();
        _cardCatalog = _cardCatalog ?? ServiceRegistry.Get<ICardCatalog>();
    }

    private bool EnsureBindings()
    {
        bool valid = true;
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect == null)
        {
            valid = false;
        }
        else if (contentParent == null)
        {
            contentParent = scrollRect.content != null ? scrollRect.content : contentParent;
        }

        if (panelRoot == null)
        {
            // panelRoot를 이 컴포넌트의 루트로 폴백
            panelRoot = gameObject;
        }

        if (itemPrefab == null)
        {
            valid = false;
        }

        if (!valid)
        {
            Debug.LogError(
                $"[UpgradeOverlay] EnsureBindings 실패 - panelRoot={(panelRoot?panelRoot.name:"null")}, scrollRect={(scrollRect?scrollRect.name:"null")}, content={(contentParent?contentParent.name:"null")}, itemPrefab={(itemPrefab?itemPrefab.name:"null")}",
                this);
        }
        return valid;
    }

    private List<(CardRuntimeState state, CardScriptableObject so)> CollectUpgradeableCandidates()
    {
        var result = new List<(CardRuntimeState, CardScriptableObject)>();

        if (_deckService == null || _cardCatalog == null)
        {
            Debug.LogWarning("[UpgradeOverlay] 서비스(IDeckService/ICardCatalog) 없음. 후보를 표시할 수 없습니다.", this);
            return result;
        }

        var snapshot = _deckService.GetAllCardsSnapshot();
        if (snapshot == null || snapshot.Count == 0)
        {
            Debug.Log("[UpgradeOverlay] 덱이 비어있습니다.", this);
            return result;
        }

        foreach (var state in snapshot)
        {
            if (CardUpgradeRules.TryGetUpgradeable(state, _cardCatalog, out var so))
            {
                result.Add((state, so));
            }
        }

        return result;
    }

    private void BuildList(List<(CardRuntimeState state, CardScriptableObject so)> candidates)
    {
        ClearContent();

        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(false);
        }

        if (candidates == null || candidates.Count == 0)
        {
            if (emptyLabel != null)
            {
                emptyLabel.text = "강화 가능한 카드가 없습니다.";
                emptyLabel.gameObject.SetActive(true);
            }
            Debug.LogWarning("[UpgradeOverlay] 후보가 없습니다.", this);
            return;
        }

        // 정렬: 이름 → 코스트(덱 오버레이 느낌 유사)
        candidates.Sort((a, b) =>
        {
            var an = GetName(a.so);
            var bn = GetName(b.so);
            int cmp = string.Compare(an, bn, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return GetCost(a.so).CompareTo(GetCost(b.so));
        });

        int built = 0;
        foreach (var (state, so) in candidates)
        {
            if (state == null)
                continue;

            var view = Instantiate(itemPrefab, contentParent);
            if (view == null)
            {
                Debug.LogError("[UpgradeOverlay] 아이템 인스턴스 생성 실패", this);
                continue;
            }

            view.Bind(so, state);
            _spawned.Add(view);

            // 클릭 가능하도록 버튼 보장
            EnsureClickable(view.gameObject, () =>
            {
                try
                {
                    Debug.Log($"[UpgradeOverlay] Item clicked: instance={(state!=null?state.InstanceId:"null")} cardId={(so!=null?so.CardId:state?.CardId)}", view);
                    _onCardClicked?.Invoke(state, so);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UpgradeOverlay] onCardClicked 처리 중 오류: {e.Message}");
                }
            });

            built++;
        }

        Debug.Log($"[UpgradeOverlay] BuildList 완료 - contentChildren={(contentParent!=null?contentParent.childCount:0)}, built={built}", this);
    }

    private void ClearContent()
    {
        if (_spawned.Count > 0)
        {
            foreach (var v in _spawned)
            {
                if (v != null)
                {
                    Destroy(v.gameObject);
                }
            }
            _spawned.Clear();
        }

        if (contentParent != null)
        {
            // 혹시 외부에서 추가된 것이 있으면 정리(안전망)
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                var child = contentParent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(false);
            emptyLabel.text = string.Empty;
        }
    }

    private static void EnsureClickable(GameObject go, Action onClick)
    {
        if (go == null) return;

        var button = go.GetComponent<Button>();
        if (button == null)
        {
            button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
        }

        // 타겟 그래픽 보장(투명 이미지)
        if (button.targetGraphic == null)
        {
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = true;
                button.targetGraphic = img;
            }
            else
            {
                graphic.raycastTarget = true;
                button.targetGraphic = graphic;
            }
        }

        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
    }

    private static string GetName(CardScriptableObject so)
    {
        if (so == null) return string.Empty;
        return string.IsNullOrEmpty(so.cardName) ? so.name : so.cardName;
    }

    private static int GetCost(CardScriptableObject so)
    {
        if (so == null) return int.MaxValue;
        return so.GetManaCost(false);
    }
}


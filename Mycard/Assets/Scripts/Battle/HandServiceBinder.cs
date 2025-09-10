using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

// IDeckService 이벤트를 구독해 HandController에 카드를 생성/제거하는 어댑터
[DisallowMultipleComponent]
public class HandServiceBinder : MonoBehaviour
{
    [SerializeField] private HandController _hand;
    [SerializeField] private Card _cardPrefab;               // 직접 지정 권장(DeckController 의존 제거)
    [Header("Initial Draw FX")]
    [SerializeField] private Transform _drawSpawnPoint;      // 초기 드로우 시 스폰 위치(없으면 핸드 위치)
    [SerializeField] private float _initialDrawStagger = 0.15f; // 초기 드로우 시 장당 지연(sec)

    private IDeckService _deckService;
    private ICardCatalog _cardCatalog;

    private readonly Dictionary<string, Card> _viewsById = new Dictionary<string, Card>();
    private readonly Stack<Card> _cardPool = new Stack<Card>();
    private bool _subscribed;
    private bool _initialized;

    // Bootstrap에서 호출하는 초기화: 의존성 주입 + 즉시 구독(레이스 컨디션 방지)
    public void Initialize(HandController hand, IDeckService deckService, ICardCatalog cardCatalog)
    {
        if (_initialized) return;
        _initialized = true;

        _hand = hand != null ? hand : FindObjectOfType<HandController>();
        _deckService = deckService != null ? deckService : ServiceRegistry.Get<IDeckService>();
        _cardCatalog = cardCatalog != null ? cardCatalog : ServiceRegistry.Get<ICardCatalog>();

        if (_deckService != null && !_subscribed)
        {
            _deckService.OnCardsDrawn += HandleCardsDrawn;
            _deckService.OnCardPlayed += HandleCardPlayed;
            _subscribed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HandServiceBinder] Initialize: hand={_hand!=null}, deckService={_deckService!=null}, cardCatalog={_cardCatalog!=null}, cardPrefab={_cardPrefab!=null}, subscribed={_subscribed}");
#endif
        }
    }

    void OnDisable()
    {
        if (_deckService != null && _subscribed)
        {
            _deckService.OnCardsDrawn -= HandleCardsDrawn;
            _deckService.OnCardPlayed -= HandleCardPlayed;
            _subscribed = false;
        }
    }

    private void HandleCardsDrawn(DrawResult result)
    {
        if (result == null || result.DrawnCards == null || _hand == null || _cardPrefab == null || _cardCatalog == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandServiceBinder] Draw event: count={result.DrawnCards.Count}, reason={result.Reason}, reshuffle={result.DidReshuffle}");
#endif

        if (result.DidReshuffle)
        {
            Debug.Log("[HandServiceBinder] 리셔플 발생! 셔플 효과를 재생합니다.");
            // TODO: 시각/청각 효과 트리거 (셔플 애니메이션, 사운드 등)
        }

        // 초기 드로우는 장당 지연/스폰포인트를 사용해 연출
        if (result.Reason == DrawReason.TurnStart && (_initialDrawStagger > 0f || _drawSpawnPoint != null))
        {
            StartCoroutine(SpawnDrawnCardsStaggered(result));
            return;
        }

        // 일반 드로우(즉시 스폰)
        foreach (var state in result.DrawnCards)
        {
            SpawnAndRegister(state, immediateSpawnAt: _hand.transform.position);
        }
        _hand.SetCardPositionsInHand();
    }

    private IEnumerator SpawnDrawnCardsStaggered(DrawResult result)
    {
        Vector3 spawnPos = (_drawSpawnPoint != null ? _drawSpawnPoint.position : _hand.transform.position);
        foreach (var state in result.DrawnCards)
        {
            SpawnAndRegister(state, immediateSpawnAt: spawnPos);
            if (_initialDrawStagger > 0f)
                yield return new WaitForSeconds(_initialDrawStagger);
        }
        _hand.SetCardPositionsInHand();
    }

    private void SpawnAndRegister(CardRuntimeState state, Vector3 immediateSpawnAt)
    {
        var so = _cardCatalog.GetCardData(state.CardId);
        if (so == null)
        {
            Debug.LogError($"[HandServiceBinder] CardId({state.CardId})에 대한 CardScriptableObject를 찾을 수 없습니다!");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandServiceBinder] Spawn view: instance={state.InstanceId}, cardId={state.CardId}");
#endif

        // 풀에서 가져오거나 새로 생성
        Card newCard = _cardPool.Count > 0 ? _cardPool.Pop() : Instantiate(_cardPrefab);
        newCard.gameObject.SetActive(true);
        newCard.transform.SetParent(_hand.transform, false);
        newCard.transform.position = immediateSpawnAt;
        newCard.Initialize(state.InstanceId, so, _deckService);
        _hand.AddCardToHand(newCard);
        if (_viewsById.ContainsKey(state.InstanceId))
        {
            Debug.LogWarning($"[HandServiceBinder] Duplicate view mapping for instance={state.InstanceId}. Overwriting.");
            _viewsById[state.InstanceId] = newCard;
        }
        else
        {
            _viewsById.Add(state.InstanceId, newCard);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandServiceBinder] View registered: go={newCard.name}, parent={(newCard.transform.parent!=null?newCard.transform.parent.name:"<none>")}, active={newCard.gameObject.activeSelf}, layer={newCard.gameObject.layer}, handCount={_hand.heldCards?.Count}");
#endif
    }

    private void HandleCardPlayed(PlayResult result)
    {
        if (result == null || result.Code != PlayResult.ResultCode.Success) return;
        if (_hand == null) return;

        // 1) 우선 매핑 테이블에서 뷰를 찾는다.
        Card view = null;
        if (!_viewsById.TryGetValue(result.PlayedInstanceId, out view))
        {
            // 2) 폴백: 핸드 목록에서 InstanceId로 직접 검색
            if (_hand != null && _hand.heldCards != null)
            {
                foreach (var c in _hand.heldCards)
                {
                    if (c != null && c.InstanceId == result.PlayedInstanceId)
                    {
                        view = c; break;
                    }
                }
            }
            if (view == null)
            {
                Debug.LogWarning($"[HandServiceBinder] OnCardPlayed: view 매핑을 찾지 못했습니다. InstanceId={result.PlayedInstanceId}");
                return;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool wasAssigned = view != null && view.assignedPlace != null;
        Debug.Log($"[HandServiceBinder] OnCardPlayed: id={result.PlayedInstanceId}, viewFound={view!=null}, assignedPlace={wasAssigned}");
#endif

        // 3) 뷰 제거 처리
        _viewsById.Remove(result.PlayedInstanceId);
        if (_hand != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int before = _hand.heldCards != null ? _hand.heldCards.Count : -1;
            Debug.Log($"[HandServiceBinder] Removing from hand: instance={result.PlayedInstanceId}, beforeCount={before}, viewGo={view.name}");
#endif
            _hand.RemoveCardFromHand(view);
            _hand.SetCardPositionsInHand();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int after = _hand.heldCards != null ? _hand.heldCards.Count : -1;
            Debug.Log($"[HandServiceBinder] Removed from hand: instance={result.PlayedInstanceId}, afterCount={after}");
#endif
        }

        // 4) 보드 배치 여부에 따라 풀 반환 여부 결정
        if (view != null && view.assignedPlace != null)
        {
            // 보드에 남겨둔다(카드 뷰는 배치 로직에서 부모/위치가 설정됨)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HandServiceBinder] Action: keep-on-board (no pooling) for {result.PlayedInstanceId}");
#endif
        }
        else
        {
            ReleaseToPool(view);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HandServiceBinder] Action: release-to-pool for {result.PlayedInstanceId}");
#endif
        }
    }

    private void ReleaseToPool(Card card)
    {
        if (card == null) return;
        card.gameObject.SetActive(false);
        _cardPool.Push(card);
    }
}

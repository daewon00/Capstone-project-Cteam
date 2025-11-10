using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

/// <summary>
/// IDeckService 이벤트를 구독해 핸드 컨트롤러에 카드를 생성·제거하는 어댑터입니다.
/// </summary>
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
    [SerializeField] private EffectIconDatabase _iconDatabase;

    private readonly Dictionary<string, Card> _viewsById = new Dictionary<string, Card>();
    private readonly Stack<Card> _cardPool = new Stack<Card>();
    private bool _subscribed;
    private bool _initialized;
    private bool _tutorialInitialDrawSignaled;

    public static Card SharedCardPrefab { get; private set; }

    /// <summary>
    /// 부트스트랩 단계에서 호출되어 의존성을 주입하고 덱 서비스 이벤트에 즉시 구독합니다.
    /// </summary>
    public void Initialize(HandController hand, IDeckService deckService, ICardCatalog cardCatalog)
    {
        if (_initialized) return;
        _initialized = true;

        _hand = hand != null ? hand : FindObjectOfType<HandController>();
        _deckService = deckService != null ? deckService : ServiceRegistry.Get<IDeckService>();
        _cardCatalog = cardCatalog != null ? cardCatalog : ServiceRegistry.Get<ICardCatalog>();
        if (_cardPrefab != null)
            SharedCardPrefab = _cardPrefab;

        if (_deckService != null && !_subscribed)
        {
            _deckService.OnCardsDrawn += HandleCardsDrawn;
            _deckService.OnCardPlayed += HandleCardPlayed;
            _subscribed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string spName = _drawSpawnPoint != null ? _drawSpawnPoint.name : "<null>";
            GameLog.Info($"[HandServiceBinder] Initialize: hand={_hand!=null}, deckService={_deckService!=null}, catalog={_cardCatalog!=null}, cardPrefab={_cardPrefab!=null}, spawnPoint={spName}, stagger={_initialDrawStagger:F2}, subscribed={_subscribed}");
#endif
        }
    }

    /// <summary>
    /// 바인더가 비활성화될 때 덱 서비스 이벤트 구독을 해제합니다.
    /// </summary>
    void OnDisable()
    {
        if (_deckService != null && _subscribed)
        {
            _deckService.OnCardsDrawn -= HandleCardsDrawn;
            _deckService.OnCardPlayed -= HandleCardPlayed;
            _subscribed = false;
        }
    }

    /// <summary>
    /// 덱 서비스에서 카드 드로우 이벤트를 받으면 핸드에 뷰를 생성합니다.
    /// </summary>
    private void HandleCardsDrawn(DrawResult result)
    {
        if (result == null || result.DrawnCards == null || _hand == null || _cardPrefab == null || _cardCatalog == null)
        {
            if (result != null && result.Reason == DrawReason.TurnStart)
            {
                BattleController.instance?.NotifyPlayerTurnStartReady();
                SignalTutorialTurnStart();
            }
            return;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[HandServiceBinder] Draw event: count={result.DrawnCards.Count}, reason={result.Reason}, reshuffle={result.DidReshuffle}");
#endif

        if (result.DidReshuffle)
        {
            GameLog.Info("[HandServiceBinder] 리셔플 발생! 셔플 효과를 재생합니다.");
            // TODO: 시각/청각 효과 트리거 (셔플 애니메이션, 사운드 등)
        }

        // 초기 드로우는 장당 지연과 스폰 지점을 활용해 연출합니다.
        if (result.Reason == DrawReason.TurnStart || result.Reason == DrawReason.ManualButton || result.Reason == DrawReason.Relic && (_initialDrawStagger > 0f || _drawSpawnPoint != null))
        {
            // 디버그: 어떤 스폰 위치를 사용하는지 기록
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Vector3 sp = (_drawSpawnPoint != null ? _drawSpawnPoint.position : _hand.transform.position);
            string spName = _drawSpawnPoint != null ? _drawSpawnPoint.name : "<hand.transform>";
            GameLog.Info($"[HandServiceBinder] Initial draw path: spawnPoint={spName} pos={sp}");
#endif
            StartCoroutine(SpawnDrawnCardsStaggered(result));
            return;
        }

        // 일반 드로우는 즉시 스폰합니다.
        foreach (var state in result.DrawnCards)
        {
            SpawnAndRegister(state, immediateSpawnAt: _hand.transform.position);
        }
        _hand.SetCardPositionsInHand();
        BattleSnapshotScheduler.Instance?.RequestSnapshot("AfterInitialDraw");

        if (result.Reason == DrawReason.TurnStart)
        {
            BattleController.instance?.NotifyPlayerTurnStartReady();
            SignalTutorialTurnStart();
        }
    }

    /// <summary>
    /// 초기 드로우 시 카드 뷰를 순차적으로 생성해 연출을 제공합니다.
    /// </summary>
    private IEnumerator SpawnDrawnCardsStaggered(DrawResult result)
    {
        yield return null;
        Vector3 spawnPos = (_drawSpawnPoint != null ? _drawSpawnPoint.position : _hand.transform.position);
        foreach (var state in result.DrawnCards)
        {
            SpawnAndRegister(state, immediateSpawnAt: spawnPos);
            if (_initialDrawStagger > 0f)
                yield return new WaitForSeconds(_initialDrawStagger);
        }
        _hand.SetCardPositionsInHand();
        BattleSnapshotScheduler.Instance?.RequestSnapshot("AfterInitialDraw");
        if (result.Reason == DrawReason.TurnStart)
        {
            BattleController.instance?.NotifyPlayerTurnStartReady();
            SignalTutorialTurnStart();
        }
    }

    private void SignalTutorialTurnStart()
    {
        var tutorial = ServiceRegistry.Get<ITutorialService>();
        if (tutorial == null) return;

        if (!_tutorialInitialDrawSignaled)
        {
            tutorial.ReportAction(TutorialRequiredActionType.ButtonClick, "initial-draw-complete");
            _tutorialInitialDrawSignaled = true;
        }
        else
        {
            tutorial.ReportAction(TutorialRequiredActionType.ButtonClick, "turn-start-ready");
        }
    }

    /// <summary>
    /// 카드 뷰를 풀에서 가져오거나 생성하고 핸드에 등록합니다.
    /// </summary>
    private void SpawnAndRegister(CardRuntimeState state, Vector3 immediateSpawnAt)
    {
        var so = _cardCatalog.GetCardData(state.CardId);
        if (so == null)
        {
            GameLog.Error($"[HandServiceBinder] CardId({state.CardId})에 대한 CardScriptableObject를 찾을 수 없습니다!");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[HandServiceBinder] Spawn view: instance={state.InstanceId}, cardId={state.CardId}");
#endif

        // 풀에서 가져오거나 새로 생성합니다.
        Card newCard = _cardPool.Count > 0 ? _cardPool.Pop() : Instantiate(_cardPrefab);
        newCard.gameObject.SetActive(true);
        newCard.transform.SetParent(_hand.transform, false);
        newCard.transform.position = immediateSpawnAt;
        bool isUpgraded = state.IsUpgraded();
        newCard.Initialize(state.InstanceId, so, _deckService, ResolveIconDatabase(), isUpgraded);
        _hand.AddCardToHand(newCard);
        _hand.ResumeLayoutFor(newCard);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 스폰 위치와 목표(핸드 인덱스) 위치 비교 로그
        int idx = newCard.handPosition;
        Vector3 target = (idx >= 0 && idx < _hand.cardPositions.Count) ? _hand.cardPositions[idx] : new Vector3(float.NaN, float.NaN, float.NaN);
        GameLog.Info($"[HandServiceBinder] SpawnAndRegister: instance={state.InstanceId}, spawnPos={immediateSpawnAt}, targetPos={target}, handIndex={idx}");
#endif
        if (_viewsById.ContainsKey(state.InstanceId))
        {
            GameLog.Warn($"[HandServiceBinder] Duplicate view mapping for instance={state.InstanceId}. Overwriting.");
            _viewsById[state.InstanceId] = newCard;
        }
        else
        {
            _viewsById.Add(state.InstanceId, newCard);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[HandServiceBinder] View registered: go={newCard.name}, parent={(newCard.transform.parent!=null?newCard.transform.parent.name:"<none>")}, active={newCard.gameObject.activeSelf}, layer={newCard.gameObject.layer}, handCount={_hand.heldCards?.Count}");
#endif
        GameEvents.RaiseCardDrawn(newCard);
        BattleDeckRuntimeSync.UpdateCardState(newCard);
    }

    /// <summary>
    /// 플레이된 카드 정보를 받아 핸드 뷰에서 제거하고 풀에 반환합니다.
    /// </summary>
    private void HandleCardPlayed(PlayResult result)
    {
        if (result == null || result.Code != PlayResult.ResultCode.Success) return;
        if (_hand == null) return;

        // 1) 먼저 매핑 테이블에서 뷰를 찾습니다.
        Card view = null;
        if (!_viewsById.TryGetValue(result.PlayedInstanceId, out view))
        {
            // 2) 대안: 핸드 목록에서 InstanceId로 직접 검색합니다.
            if (_hand != null && _hand.heldCards != null)
            {
                foreach (var c in _hand.heldCards)
                {
                    if (c != null && c.InstanceId == result.PlayedInstanceId)
                    {
                        view = c;
                        break;
                    }
                }
            }
            if (view == null)
            {
                GameLog.Warn($"[HandServiceBinder] OnCardPlayed: view 매핑을 찾지 못했습니다. InstanceId={result.PlayedInstanceId}");
                return;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool wasAssigned = view != null && view.assignedPlace != null;
        GameLog.Info($"[HandServiceBinder] OnCardPlayed: id={result.PlayedInstanceId}, viewFound={view!=null}, assignedPlace={wasAssigned}");
#endif

        // 3) 뷰 제거를 수행합니다.
        _viewsById.Remove(result.PlayedInstanceId);
        if (_hand != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int before = _hand.heldCards != null ? _hand.heldCards.Count : -1;
            GameLog.Info($"[HandServiceBinder] Removing from hand: instance={result.PlayedInstanceId}, beforeCount={before}, viewGo={view.name}");
#endif
            _hand.RemoveCardFromHand(view);
            _hand.SetCardPositionsInHand();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int after = _hand.heldCards != null ? _hand.heldCards.Count : -1;
            GameLog.Info($"[HandServiceBinder] Removed from hand: instance={result.PlayedInstanceId}, afterCount={after}");
#endif
        }

        // 4) 보드 배치 여부에 따라 풀 반환 여부를 결정합니다.
        if (view != null && view.assignedPlace != null)
        {
            // 보드에 남겨둔다(카드 뷰는 배치 로직에서 부모/위치가 설정됨)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[HandServiceBinder] Action: keep-on-board (no pooling) for {result.PlayedInstanceId}");
#endif
        }
        else
        {
            ReleaseToPool(view);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[HandServiceBinder] Action: release-to-pool for {result.PlayedInstanceId}");
#endif
        }
    }

    /// <summary>
    /// 카드 뷰를 비활성화하고 풀에 반환합니다.
    /// </summary>
    private void ReleaseToPool(Card card)
    {
        if (card == null) return;
        card.gameObject.SetActive(false);
        _cardPool.Push(card);
    }

    /// <summary>
    /// 이미 존재하는 카드 뷰를 캐시에 등록합니다.
    /// </summary>
    public void RegisterExistingCard(Card card)
    {
        if (card == null) return;
        var id = card.GetBattleInstanceId();
        if (string.IsNullOrEmpty(id)) return;
        _viewsById[id] = card;
    }

    /// <summary>
    /// 캐시와 풀을 초기화합니다.
    /// </summary>
    public void ResetViewCache()
    {
        _viewsById.Clear();
        _cardPool.Clear();
    }

    private EffectIconDatabase ResolveIconDatabase()
    {
        if (_iconDatabase != null)
            return _iconDatabase;

        _iconDatabase = ServiceRegistry.Get<EffectIconDatabase>();
        if (_iconDatabase != null)
            return _iconDatabase;

        _iconDatabase = Resources.Load<EffectIconDatabase>("Cards/EffectIconDatabase");
        if (_iconDatabase == null)
        {
            GameLog.Warn("[HandServiceBinder] EffectIconDatabase를 찾을 수 없습니다. 아이콘이 표시되지 않습니다.");
        }
        else
        {
            ServiceRegistry.Register<EffectIconDatabase>(_iconDatabase);
        }

        return _iconDatabase;
    }
}

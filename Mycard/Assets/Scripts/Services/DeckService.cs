using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Save;
using Game.Utils; // Shuffle extension
using BattleSnapshot;

public class DeckService : IDeckService
{
    private readonly IDatabase _db;
    private readonly IRngService _rng;
    private string _currentRunId;
    private List<CardRuntimeState> _runtimeDeck = new List<CardRuntimeState>();

    // 내부 캐시(고성능 조회/이동용)
    private readonly Dictionary<string, CardRuntimeState> _cardsById = new Dictionary<string, CardRuntimeState>();
    private readonly List<string> _drawPileIds = new List<string>();
    private readonly List<string> _handIds = new List<string>();
    private readonly List<string> _discardPileIds = new List<string>();
    private readonly List<string> _exhaustPileIds = new List<string>();
    private readonly List<string> _playerFieldIds = new List<string>();
    private readonly List<string> _enemyFieldIds = new List<string>();
    private readonly Dictionary<CardLocation, int> _nextOrderInPile = new Dictionary<CardLocation, int>();
    private int _handLimit = 10; // 내부 관리 핸드 한도

    public event System.Action<PlayResult> OnCardPlayed;
    public event System.Action<DrawResult> OnCardsDrawn;
    public event System.Action<PileCounts> OnPileCountsChanged;

    public DeckService(IDatabase db, IRngService rng)
    {
        _db = db;
        _rng = rng;
    }

    public void LoadAndPrepareDeck(string runId)
    {
        _currentRunId = runId;
        if (string.IsNullOrEmpty(_currentRunId)) return;

        // 1) 최신 포맷 시도
        var existingCards = _db.LoadCardRuntimeStates(_currentRunId);
        if (existingCards != null && existingCards.Count > 0)
        {
            _runtimeDeck = existingCards;
            BuildInternalCache(_runtimeDeck);
            Debug.Log($"[DeckService] 런({_currentRunId}) 덱 런타임 상태 로드 완료: {_runtimeDeck.Count}장");
            // 초기 카운트 방송으로 UI가 정확히 시작하도록 보장
            OnPileCountsChanged?.Invoke(GetPileCounts());
            return;
        }

        // 2) 백필: 구버전 CardInDeck → CardRuntimeState
        Debug.LogWarning("[DeckService] 런타임 상태가 없어 구버전 덱(CardInDeck)에서 백필을 진행합니다.");

        var runLoad = _db.LoadCurrentRun(_currentRunId);
        var legacy = runLoad?.Cards;
        if (legacy == null || legacy.Count == 0)
        {
            Debug.Log("[DeckService] 백필할 구버전 덱 데이터가 없습니다. 빈 덱으로 시작합니다.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var dupCheck = new HashSet<string>();
        foreach (var row in legacy)
        {
            if (!dupCheck.Add(row.InstanceId))
            {
                Debug.LogError($"[DeckService] 백필 입력에 중복 InstanceId: {row.InstanceId}");
            }
        }
#endif

        var newDeck = new List<CardRuntimeState>(legacy.Count);
        foreach (var row in legacy)
        {
            newDeck.Add(new CardRuntimeState
            {
                InstanceId = row.InstanceId,
                RunId = _currentRunId,
                CardId = row.CardId,
                Location = CardLocation.DrawPile,
                OrderInPile = 0,
                ModifiersJson = string.Empty
            });
        }

        // 3) 초기 셔플은 deck-init 도메인을 사용
        TryEnsureSeeded("deck-init");
        _rng.Shuffle("deck-init", newDeck);

        // OrderInPile: 값이 클수록 Top
        for (int i = 0; i < newDeck.Count; i++)
            newDeck[i].OrderInPile = i;

        _db.UpsertCardRuntimeStates(_currentRunId, newDeck);
        _runtimeDeck = newDeck;
        BuildInternalCache(_runtimeDeck);
        Debug.Log($"[DeckService] 백필 완료: {_runtimeDeck.Count}장");
        // 초기 카운트 방송
        OnPileCountsChanged?.Invoke(GetPileCounts());
    }

    public void SetHandLimit(int limit)
    {
        _handLimit = limit > 0 ? limit : 10;
    }

    public DrawResult DrawCards(int amount, DrawReason reason = DrawReason.Unknown)
    {
        EnsureInitialized();
        if (amount <= 0) amount = 0;

        var drawn = new List<CardRuntimeState>();
        var result = new DrawResult
        {
            DrawnCountRequested = amount,
            DrawnCountActual = 0,
            DidReshuffle = false,
            DrawnCards = drawn,
            Reason = reason
        };

        for (int i = 0; i < amount; i++)
        {
            if (_handIds.Count >= _handLimit) break;
            if (_drawPileIds.Count == 0)
            {
                result.DidReshuffle |= ReshuffleDiscardIntoDraw();
            }
            if (_drawPileIds.Count == 0) break;

            string topId = _drawPileIds[_drawPileIds.Count - 1]; // Top은 리스트 끝
            MoveCard(topId, CardLocation.Hand);
            drawn.Add(_cardsById[topId]);
        }

        result.DrawnCountActual = drawn.Count;
        PersistAndBroadcast(drawnResult: result);
        return result;
    }

    public void PrepareNewCombat()
    {
        EnsureInitialized();

        // 모든 카드를 DrawPile로 모은 후 셔플하여 전투 시작 상태를 준비합니다.
        // 1) 내부 리스트 재구성: 전 카드 ID 모으기
        var allIds = _cardsById.Keys.ToList();

        _drawPileIds.Clear();
        _handIds.Clear();
        _discardPileIds.Clear();
        _exhaustPileIds.Clear();
        _playerFieldIds.Clear();
        _enemyFieldIds.Clear();

        _drawPileIds.AddRange(allIds);
        foreach (var id in allIds)
        {
            var c = _cardsById[id];
            c.Location = CardLocation.DrawPile;
        }

        // 2) 셔플 + 순서 재부여(값이 클수록 Top)
        TryEnsureSeeded("deck-shuffle");
        _rng.Shuffle("deck-shuffle", _drawPileIds);
        for (int i = 0; i < _drawPileIds.Count; i++)
        {
            var id = _drawPileIds[i];
            _cardsById[id].OrderInPile = i;
        }

        // 3) 보조 인덱스 갱신 + 저장/브로드캐스트(카운터 업데이트)
        RecomputeNextOrderInPiles();
        PersistAndBroadcast();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var counts = GetCurrentPileCounts();
        Debug.Log($"[DeckService] PrepareNewCombat: draw={counts.Draw}, discard={counts.Discard}, hand={counts.Hand}, exhaust={counts.Exhaust}");
#endif
    }

    public void CleanupAfterCombat()
    {
        EnsureInitialized();

        // 남아있는 핸드 카드를 모두 Discard로 이동(전투 종료 시 핸드 비우기 정합성 보장)
        if (_handIds.Count > 0)
        {
            var handCopy = _handIds.ToList();
            foreach (var id in handCopy)
            {
                MoveCard(id, CardLocation.DiscardPile);
            }
        }

        PersistAndBroadcast();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var counts = GetCurrentPileCounts();
        Debug.Log($"[DeckService] CleanupAfterCombat: draw={counts.Draw}, discard={counts.Discard}, hand={counts.Hand}, exhaust={counts.Exhaust}");
#endif
    }

    public PlayResult PlayCard(string instanceId)
    {
        EnsureInitialized();
        var result = new PlayResult { PlayedInstanceId = instanceId };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DeckService] PlayCard request: id={instanceId}");
#endif

        if (string.IsNullOrEmpty(instanceId) || !_handIds.Contains(instanceId))
        {
            result.Code = PlayResult.ResultCode.CardNotInHand;
            return result;
        }

        // 기본 규칙: 사용하면 버림 더미로 이동(향후 카드 효과에 따라 Exhaust 등 변경 가능)
        MoveCard(instanceId, CardLocation.DiscardPile);
        result.TargetPile = CardLocation.DiscardPile;
        result.Code = PlayResult.ResultCode.Success;

        PersistAndBroadcast(playedResult: result);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var counts = GetCurrentPileCounts();
        Debug.Log($"[DeckService] PlayCard done: id={instanceId}, counts: draw={counts.Draw}, hand={counts.Hand}, discard={counts.Discard}, exhaust={counts.Exhaust}");
#endif
        return result;
    }

    public int GetPileCount(CardLocation location) => GetPileList(location).Count;

    public IReadOnlyList<CardRuntimeState> GetHandSnapshot()
    {
        if (_handIds.Count == 0) return Array.Empty<CardRuntimeState>();
        // 현재 핸드 목록에서 OrderInPile DESC로 반환(Top 우선)
        return _handIds
            .Select(id => _cardsById[id])
            .OrderByDescending(c => c.OrderInPile)
            .ToList();
    }

    public PileCounts GetPileCounts() => GetCurrentPileCounts();

    public void AddCardToDeckById(string cardId, bool isUpgraded = false)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(cardId))
            throw new System.ArgumentException("cardId must be non-empty", nameof(cardId));

        // 신규 카드 인스턴스 생성: DiscardPile 상단으로 push
        var instanceId = System.Guid.NewGuid().ToString("N");
        var newCard = new CardRuntimeState
        {
            InstanceId = instanceId,
            RunId = _currentRunId,
            CardId = cardId,
            Location = CardLocation.DiscardPile,
            OrderInPile = 0,
            ModifiersJson = string.Empty
        };

        // 내부 캐시에 반영
        _cardsById[instanceId] = newCard;
        if (!_nextOrderInPile.TryGetValue(CardLocation.DiscardPile, out var next)) next = 0;
        newCard.OrderInPile = next;
        _nextOrderInPile[CardLocation.DiscardPile] = next + 1;
        _discardPileIds.Add(instanceId);

        // 스냅샷 저장 + 방송
        PersistAndBroadcast();
        Debug.Log($"[DeckService] Card '{cardId}' added to deck (DiscardPile). counts={GetPileCounts().Discard}");
    }

    public IReadOnlyList<CardRuntimeState> GetCardsInLocation(CardLocation location)
    {
        EnsureInitialized();
        return _cardsById.Values.Where(c => c.Location == location).OrderByDescending(c => c.OrderInPile).ToList();
    }

    public CardRuntimeState GetCardByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        return _cardsById.TryGetValue(instanceId, out var state) ? state : null;
    }

    public void UpdateBattleCardState(BattleCardState state, CardLocation location)
    {
        EnsureInitialized();
        if (state == null || string.IsNullOrEmpty(state.instanceId)) return;
        if (!_cardsById.TryGetValue(state.instanceId, out var cardState))
            return;

        var fromList = GetPileList(cardState.Location);
        fromList.Remove(state.instanceId);

        cardState.Location = location;
        cardState.OrderInPile = state.slotIndex >= 0 ? state.slotIndex : cardState.OrderInPile;
        cardState.ModifiersJson = JsonUtility.ToJson(state);

        var toList = GetPileList(location);
        if (!toList.Contains(state.instanceId))
        {
            toList.Add(state.instanceId);
        }

        RecomputeNextOrderInPiles();
        PersistAndBroadcast();
    }

    private void TryEnsureSeeded(string domain)
    {
        try { _rng.NextUInt(domain); }
        catch (System.InvalidOperationException)
        {
            _rng.Seed(domain, HashRunIdToSeed(_currentRunId, domain));
        }
    }

    private static uint HashRunIdToSeed(string runId, string domain)
    {
        unchecked
        {
            uint h = 2166136261u; // FNV-1a
            if (!string.IsNullOrEmpty(runId)) foreach (char c in runId) { h ^= c; h *= 16777619u; }
            if (!string.IsNullOrEmpty(domain)) foreach (char c in domain) { h ^= c; h *= 16777619u; }
            return h == 0u ? 1u : h;
        }
    }

    // =============================
    // 내부 헬퍼
    // =============================

    private void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(_currentRunId))
            throw new InvalidOperationException("[DeckService] 아직 런이 로드되지 않았습니다. LoadAndPrepareDeck을 먼저 호출해야 합니다.");
    }

    private void BuildInternalCache(List<CardRuntimeState> allCards)
    {
        _cardsById.Clear();
        _drawPileIds.Clear();
        _handIds.Clear();
        _discardPileIds.Clear();
        _exhaustPileIds.Clear();
        _playerFieldIds.Clear();
        _enemyFieldIds.Clear();

        if (allCards == null) return;

        foreach (var c in allCards)
        {
            if (c == null || string.IsNullOrEmpty(c.InstanceId)) continue;
            _cardsById[c.InstanceId] = c;
            GetPileList(c.Location).Add(c.InstanceId);
        }

        SortAllPiles();
        RecomputeNextOrderInPiles();
    }

    private bool ReshuffleDiscardIntoDraw()
    {
        if (_discardPileIds.Count == 0) return false;

        // discard → draw로 이동 (리스트 병합)
        _drawPileIds.AddRange(_discardPileIds);
        _discardPileIds.Clear();

        // 실제 셔플 수행
        TryEnsureSeeded("deck-shuffle");
        _rng.Shuffle("deck-shuffle", _drawPileIds);

        // 새 순서 부여(작을수록 bottom, 클수록 top)
        for (int i = 0; i < _drawPileIds.Count; i++)
        {
            var id = _drawPileIds[i];
            _cardsById[id].Location = CardLocation.DrawPile;
            _cardsById[id].OrderInPile = i;
        }

        RecomputeNextOrderInPiles();
        return true;
    }

    private void MoveCard(string instanceId, CardLocation to)
    {
        if (!_cardsById.TryGetValue(instanceId, out var card)) return;
        var fromList = GetPileList(card.Location);
        fromList.Remove(instanceId);

        card.Location = to;
        // Top으로 push: 다음 순번 할당 후 리스트 끝에 추가
        if (!_nextOrderInPile.TryGetValue(to, out var next)) next = 0;
        card.OrderInPile = next;
        _nextOrderInPile[to] = next + 1;

        GetPileList(to).Add(instanceId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DeckService] MoveCard instance={instanceId} -> {to}");
#endif
    }

    private void PersistAndBroadcast(DrawResult drawnResult = null, PlayResult playedResult = null)
    {
        var counts = GetCurrentPileCounts();
        try
        {
            _db.UpsertCardRuntimeStates(_currentRunId, _cardsById.Values.ToList());
            _db.UpsertRngStates(_currentRunId, _rng.GetStatesForSave());
        }
        catch (Exception e)
        {
            Debug.LogError($"[DeckService] DB 상태 저장 실패: {e.Message}");
            throw;
        }

#if UNITY_EDITOR
        // 데이터 무결성 검사(에디터 전용)
        int total = _drawPileIds.Count + _discardPileIds.Count + _handIds.Count + _exhaustPileIds.Count + _playerFieldIds.Count + _enemyFieldIds.Count;
        UnityEngine.Debug.Assert(total == _cardsById.Count, $"[DeckService] 카드 총량 불일치! 캐시 합계: {total}, 전체: {_cardsById.Count}");
#endif

        if (drawnResult != null)
        {
            drawnResult.FinalCounts = counts;
            OnCardsDrawn?.Invoke(drawnResult);
        }
        if (playedResult != null)
        {
            playedResult.FinalCounts = counts;
            OnCardPlayed?.Invoke(playedResult);
        }
        OnPileCountsChanged?.Invoke(counts);
    }

    private List<string> GetPileList(CardLocation loc)
    {
        switch (loc)
        {
            case CardLocation.DrawPile: return _drawPileIds;
            case CardLocation.Hand: return _handIds;
            case CardLocation.DiscardPile: return _discardPileIds;
            case CardLocation.ExhaustPile: return _exhaustPileIds;
            case CardLocation.PlayerField: return _playerFieldIds;
            case CardLocation.EnemyField: return _enemyFieldIds;
            default:
                Debug.LogWarning($"[DeckService] 알 수 없는 CardLocation: {loc}");
                return _drawPileIds;
        }
    }

    private void SortAllPiles()
    {
        Comparison<string> comp = (a, b) => _cardsById[a].OrderInPile.CompareTo(_cardsById[b].OrderInPile);
        _drawPileIds.Sort(comp);
        _handIds.Sort(comp);
        _discardPileIds.Sort(comp);
        _exhaustPileIds.Sort(comp);
        _playerFieldIds.Sort(comp);
        _enemyFieldIds.Sort(comp);
    }

    private void RecomputeNextOrderInPiles()
    {
        // 대상 더미만 초기화
        _nextOrderInPile[CardLocation.DrawPile] = _drawPileIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
        _nextOrderInPile[CardLocation.Hand] = _handIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
        _nextOrderInPile[CardLocation.DiscardPile] = _discardPileIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
        _nextOrderInPile[CardLocation.ExhaustPile] = _exhaustPileIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
        _nextOrderInPile[CardLocation.PlayerField] = _playerFieldIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
        _nextOrderInPile[CardLocation.EnemyField] = _enemyFieldIds.Select(id => _cardsById[id].OrderInPile).DefaultIfEmpty(-1).Max() + 1;
    }

    private PileCounts GetCurrentPileCounts() => new PileCounts
    {
        Draw = _drawPileIds.Count,
        Discard = _discardPileIds.Count,
        Hand = _handIds.Count,
        Exhaust = _exhaustPileIds.Count
    };
}

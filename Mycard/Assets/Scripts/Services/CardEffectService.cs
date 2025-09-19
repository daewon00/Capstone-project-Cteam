using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 카드 효과를 총괄하는 서비스. 카드별 효과 정의를 해석하고
/// 전투 이벤트에 맞춰 실행한다.
/// </summary>
public sealed class CardEffectService : ICardEffectService
{
    private readonly Dictionary<string, CardRuntimeEffectState> _cardStates = new();
    private readonly LeaderRuntimeState _playerLeader = new(true);
    private readonly LeaderRuntimeState _enemyLeader = new(false);
    private readonly List<CardRuntimeEffectState> _scratchStates = new();
    private readonly EffectIconDatabase _iconDatabase;

    private int _playerRallyBonus;
    private int _enemyRallyBonus;

    public CardEffectService()
    {
        GameEvents.OnBattleStart += ResetAll;
        GameEvents.OnBattleEnd += ResetAll;
        GameEvents.OnTurnEnd += HandleTurnEnded;
        GameEvents.ModifyPlayerAttack += ApplyPlayerAttackBonus;
        GameEvents.ModifyEnemyAttack += ApplyEnemyAttackBonus;

        _iconDatabase = ServiceRegistry.Get<EffectIconDatabase>();
        if (_iconDatabase == null)
        {
            _iconDatabase = Resources.Load<EffectIconDatabase>("Cards/EffectIconDatabase");
            if (_iconDatabase != null)
            {
                ServiceRegistry.Register<EffectIconDatabase>(_iconDatabase);
            }
        }
    }

    public void RegisterBoardCard(Card card, bool isPlayerOwner, CardEffectRuntimeSnapshot snapshot = null)
    {
        if (card == null || card.cardSO == null)
            return;

        var key = GetCardKey(card);
        // 동일 카드가 이미 등록되어 있으면 먼저 정리한다.
        if (_cardStates.TryGetValue(key, out var existing))
        {
            DeactivateAuras(existing);
            _cardStates.Remove(key);
            existing.Dispose();
        }

        var state = new CardRuntimeEffectState(card, isPlayerOwner);
        CategorizeEffects(card.cardSO.Effects, state);
        _cardStates[key] = state;

        if (snapshot != null)
        {
            state.ShieldValue = Mathf.Max(0, snapshot.shield);
            state.ActiveAuraBonuses = Mathf.Max(0, snapshot.auraBonus);
            if (state.ActiveAuraBonuses != 0)
            {
                if (state.IsPlayerOwner)
                    _playerRallyBonus += state.ActiveAuraBonuses;
                else
                    _enemyRallyBonus += state.ActiveAuraBonuses;
            }
        }
        else
        {
            ExecuteOnPlay(state);
            ActivateAuras(state);
        }

        state.View.UpdateCardDisplay();
    }

    public void UnregisterBoardCard(Card card)
    {
        if (card == null)
            return;

        var key = GetCardKey(card);
        if (!_cardStates.TryGetValue(key, out var state))
            return;

        DeactivateAuras(state);
        _cardStates.Remove(key);
        state.Dispose();
    }

    public DamageMitigationResult ProcessCardDamage(Card card, Card attacker, int incomingDamage, DamageSourceKind sourceKind)
    {
        if (card == null)
            return new DamageMitigationResult(incomingDamage, 0);

        if (!_cardStates.TryGetValue(GetCardKey(card), out var state))
            return new DamageMitigationResult(incomingDamage, 0);

        int remaining = incomingDamage;
        int blocked = 0;

        if (state.ShieldValue > 0 && remaining > 0)
        {
            blocked = remaining;
            remaining = 0;
            state.ShieldValue = Mathf.Max(0, state.ShieldValue - 1);
        }

        return new DamageMitigationResult(remaining, blocked);
    }

    public void HandleCardDamaged(Card card, Card attacker, int appliedDamage, DamageSourceKind sourceKind)
    {
        if (card == null || appliedDamage < 0)
            return;

        if (!_cardStates.TryGetValue(GetCardKey(card), out var state))
            return;

        if (state.OnDamaged.Count == 0)
            return;

        foreach (var effect in state.OnDamaged)
        {
            switch (effect.Type)
            {
                case CardEffectType.CounterDamage:
                    if (sourceKind == DamageSourceKind.Retaliation)
                        continue; // 무한 루프 방지
                    if (attacker == null)
                        continue;
                    int counterDamage = Mathf.Max(1, effect.Value);
                    attacker?.DamageCard(counterDamage, card, DamageSourceKind.Retaliation);
                    break;
            }
        }
    }

    public DamageMitigationResult ProcessLeaderDamage(bool isPlayerLeader, int incomingDamage)
    {
        var leaderState = isPlayerLeader ? _playerLeader : _enemyLeader;

        int remaining = incomingDamage;
        int blocked = 0;
        if (leaderState.Shield > 0 && remaining > 0)
        {
            blocked = remaining;
            remaining = 0;
            leaderState.Shield = Mathf.Max(0, leaderState.Shield - 1);
        }

        return new DamageMitigationResult(remaining, blocked);
    }

    public void HandleAttackResolved(CardAttackContext context)
    {
        if (context.Attacker == null)
            return;

        if (!_cardStates.TryGetValue(GetCardKey(context.Attacker), out var state))
            return;

        if (state.OnAttackSuccess.Count == 0)
            return;

        foreach (var effect in state.OnAttackSuccess)
        {
            switch (effect.Type)
            {
                case CardEffectType.DestroyTarget:
                    if (!context.HitCard || context.PrimaryTarget == null)
                        continue;
                    if (context.DamageToPrimary <= 0)
                        continue; // 피해가 없으면 트리거하지 않음
                    ForceDestroyCard(context.PrimaryTarget, context.Attacker);
                    break;

                case CardEffectType.DamageAdjacent:
                    DealAdjacentDamage(state, context, effect);
                    break;

                case CardEffectType.HealLeader:
                    int heal = context.DamageToPrimary + context.DamageToLeader;
                    if (effect.Value > 0)
                        heal = effect.Value;
                    HealLeader(state.IsPlayerOwner, heal);
                    break;
            }
        }
    }

    public void HandleTurnEnded(bool isPlayerTurn)
    {
        _scratchStates.Clear();
        foreach (var kvp in _cardStates)
        {
            if (kvp.Value.IsPlayerOwner == isPlayerTurn && kvp.Value.OnOwnerTurnEnd.Count > 0)
            {
                _scratchStates.Add(kvp.Value);
            }
        }

        foreach (var state in _scratchStates)
        {
            foreach (var effect in state.OnOwnerTurnEnd)
            {
                switch (effect.Type)
                {
                    case CardEffectType.Move:
                        MoveCard(state, effect);
                        break;
                }
            }
        }
    }

    public bool HasEffect(Card card, CardEffectType effectType)
    {
        if (card == null)
            return false;

        if (!_cardStates.TryGetValue(GetCardKey(card), out var state))
            return false;

        return state.PassiveFlags.Contains(effectType);
    }

    public void ForceDestroyCard(Card target, Card killer = null)
    {
        if (target == null)
            return;

        UnregisterBoardCard(target);
        target.ForceKill(killer);
    }

    public CardEffectRuntimeSnapshot CaptureCardState(Card card)
    {
        if (card == null)
            return null;

        if (!_cardStates.TryGetValue(GetCardKey(card), out var state))
            return null;

        return new CardEffectRuntimeSnapshot
        {
            shield = Mathf.Max(0, state.ShieldValue),
            auraBonus = Mathf.Max(0, state.ActiveAuraBonuses)
        };
    }

    public int GetLeaderShield(bool isPlayerLeader)
    {
        var leader = isPlayerLeader ? _playerLeader : _enemyLeader;
        return Mathf.Max(0, leader.Shield);
    }

    public void RestoreLeaderShield(bool isPlayerLeader, int shieldValue)
    {
        var leader = isPlayerLeader ? _playerLeader : _enemyLeader;
        leader.Shield = Mathf.Max(0, shieldValue);
    }

    public void ResetAll()
    {
        foreach (var state in _cardStates.Values)
        {
            DeactivateAuras(state);
            state.Dispose();
        }
        _cardStates.Clear();
        _playerLeader.Reset();
        _enemyLeader.Reset();
        _playerRallyBonus = 0;
        _enemyRallyBonus = 0;
    }

    private void ExecuteOnPlay(CardRuntimeEffectState state)
    {
        if (state.OnPlay.Count == 0)
            return;

        foreach (var effect in state.OnPlay)
        {
            switch (effect.Type)
            {
                case CardEffectType.AddShield:
                    ApplyShield(effect, state);
                    break;
                case CardEffectType.DrawCard:
                    if (effect.Value > 0)
                    {
                        var deck = GameServices.Deck;
                        deck?.DrawCards(effect.Value, DrawReason.CardEffect);
                    }
                    break;
                case CardEffectType.SummonToken:
                    SummonAdjacentTokens(state, effect);
                    break;
            }
        }
    }

    private void ApplyShield(CardEffectDefinition effect, CardRuntimeEffectState state)
    {
        int amount = Mathf.Max(1, effect.Value > 0 ? effect.Value : effect.Potency);
        if (effect.Target == CardEffectTarget.OwnerLeader)
        {
            var leader = state.IsPlayerOwner ? _playerLeader : _enemyLeader;
            leader.Shield += amount;
            return;
        }

        if (effect.Target == CardEffectTarget.Self)
        {
            state.ShieldValue += amount;
        }
    }

    private void SummonAdjacentTokens(CardRuntimeEffectState ownerState, CardEffectDefinition effect)
    {
        if (string.IsNullOrEmpty(effect.PayloadId))
        {
            Debug.LogWarning("[CardEffectService] Summon 효과에 PayloadId가 설정되지 않았습니다.");
            return;
        }

        if (!TryFindLane(ownerState.View, ownerState.IsPlayerOwner, out var lane, out var index))
            return;

        var catalog = ServiceRegistry.Get<ICardCatalog>();
        var tokenData = catalog?.GetCardData(effect.PayloadId);
        if (tokenData == null)
        {
            Debug.LogWarning($"[CardEffectService] Summon 대상 카드 데이터를 찾을 수 없습니다: {effect.PayloadId}");
            return;
        }

        TrySummonAtOffset(ownerState, tokenData, lane, index - 1);
        TrySummonAtOffset(ownerState, tokenData, lane, index + 1);
    }

    private void TrySummonAtOffset(CardRuntimeEffectState ownerState, CardScriptableObject tokenData, CardPlacePoint[] lane, int targetIndex)
    {
        if (lane == null || targetIndex < 0 || targetIndex >= lane.Length)
            return;

        var targetPoint = lane[targetIndex];
        if (targetPoint == null || targetPoint.activeCard != null)
            return;

        var deck = GameServices.Deck;
        var prefab = HandServiceBinder.SharedCardPrefab;
        if (deck == null || prefab == null)
        {
            Debug.LogWarning("[CardEffectService] 토큰 소환에 필요한 Deck 또는 Card Prefab이 없습니다.");
            return;
        }

        Quaternion boardRotation;

        if (ownerState != null && ownerState.View != null)
        {
            boardRotation = ownerState.View.transform.rotation;
        }
        else if (HandController.instance != null && HandController.instance.minpos != null)
        {
            boardRotation = HandController.instance.minpos.rotation;
        }
        else
        {
            boardRotation = targetPoint.transform.rotation;
        }

        Card token = UnityEngine.Object.Instantiate(prefab, targetPoint.transform.position, boardRotation);
        string instanceId = Guid.NewGuid().ToString("N");
        token.Initialize(instanceId, tokenData, deck, _iconDatabase);
        token.inHand = false;
        token.handPosition = -1;
        token.isPlayer = ownerState.IsPlayerOwner;
        token.SetInteractable(false);
        token.transform.SetParent(targetPoint.transform, true);
        token.MoveToPoint(targetPoint.transform.position, boardRotation);
        if (HandController.instance != null)
            token.SetCardScale(HandController.instance.GetBoardScale());

        token.assignedPlace = targetPoint;
        targetPoint.activeCard = token;

        RegisterBoardCard(token, ownerState.IsPlayerOwner);
        BattleDeckRuntimeSync.UpdateCardState(token);
    }

    private void DealAdjacentDamage(CardRuntimeEffectState attackerState, CardAttackContext context, CardEffectDefinition effect)
    {
        var board = CardPointsController.instance;
        if (board == null)
            return;

        var opponentLane = context.AttackerIsPlayer ? board.enemyCardPoints : board.playerCardPoints;
        if (opponentLane == null || opponentLane.Length == 0)
            return;

        int damage = effect.Value > 0 ? effect.Value : context.BaseAttack;
        if (damage <= 0)
            return;

        ApplyDamageToLane(opponentLane, context.LaneIndex - 1, context.Attacker, damage);
        ApplyDamageToLane(opponentLane, context.LaneIndex + 1, context.Attacker, damage);
    }

    private void ApplyDamageToLane(CardPlacePoint[] lane, int index, Card attacker, int damage)
    {
        if (index < 0 || index >= lane.Length)
            return;

        var point = lane[index];
        if (point == null || point.activeCard == null)
            return;

        point.activeCard.DamageCard(damage, attacker, DamageSourceKind.Attack);
    }

    private void MoveCard(CardRuntimeEffectState state, CardEffectDefinition effect)
    {
        if (!TryFindLane(state.View, state.IsPlayerOwner, out var lane, out var currentIndex))
            return;

        int direction = effect.Value;
        if (direction == 0)
            direction = effect.Potency;
        direction = Math.Sign(direction);
        if (direction == 0)
            return;

        int targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= lane.Length)
            return;

        var originPoint = lane[currentIndex];
        var targetPoint = lane[targetIndex];
        if (targetPoint == null || targetPoint.activeCard != null)
            return;

        if (originPoint != null)
            originPoint.activeCard = null;

        targetPoint.activeCard = state.View;
        state.View.assignedPlace = targetPoint;
        // 재배치 시에도 카드가 슬롯의 자식으로 유지되어 핸드 배치와 동일한 구조를 갖도록 보장한다.
        state.View.transform.SetParent(targetPoint.transform, true);
        // 필드 이동은 기존 월드 회전을 유지하여 카드가 비정상적으로 세워지지 않도록 한다.
        state.View.MoveToPoint(targetPoint.transform.position, state.View.transform.rotation);
        if (HandController.instance != null)
        {
            state.View.SetCardScale(HandController.instance.GetBoardScale());
        }
        BattleDeckRuntimeSync.UpdateCardState(state.View);
    }

    private void HealLeader(bool isPlayer, int amount)
    {
        if (amount <= 0)
            return;

        BattleController.instance?.HealLeader(isPlayer, amount);
    }

    private int ApplyPlayerAttackBonus(int baseValue) => baseValue + _playerRallyBonus;

    private int ApplyEnemyAttackBonus(int baseValue) => baseValue + _enemyRallyBonus;

    private void ActivateAuras(CardRuntimeEffectState state)
    {
        if (state.AuraEffects.Count == 0)
            return;

        foreach (var effect in state.AuraEffects)
        {
            if (effect.Type == CardEffectType.RallyAttackBonus)
            {
                int bonus = effect.Value != 0 ? effect.Value : Mathf.Max(1, effect.Potency);
                if (state.IsPlayerOwner)
                    _playerRallyBonus += bonus;
                else
                    _enemyRallyBonus += bonus;

                state.ActiveAuraBonuses += bonus;
            }
        }
    }

    private void DeactivateAuras(CardRuntimeEffectState state)
    {
        if (state.ActiveAuraBonuses == 0)
            return;

        if (state.IsPlayerOwner)
            _playerRallyBonus -= state.ActiveAuraBonuses;
        else
            _enemyRallyBonus -= state.ActiveAuraBonuses;

        state.ActiveAuraBonuses = 0;
    }

    private static string GetCardKey(Card card)
    {
        if (card == null)
            return string.Empty;
        if (!string.IsNullOrEmpty(card.InstanceId))
            return card.InstanceId;
        return card.GetInstanceID().ToString();
    }

    private void CategorizeEffects(IReadOnlyList<CardEffectDefinition> definitions, CardRuntimeEffectState state)
    {
        if (definitions == null)
            return;

        foreach (var effect in definitions)
        {
            if (effect == null || effect.Type == CardEffectType.None)
                continue;

            switch (effect.Timing)
            {
                case CardEffectTiming.OnPlay:
                    state.OnPlay.Add(effect);
                    break;
                case CardEffectTiming.OnAttackSuccess:
                    state.OnAttackSuccess.Add(effect);
                    break;
                case CardEffectTiming.OnDamaged:
                    state.OnDamaged.Add(effect);
                    break;
                case CardEffectTiming.OnOwnerTurnEnd:
                    state.OnOwnerTurnEnd.Add(effect);
                    break;
                case CardEffectTiming.AuraWhileAlive:
                    state.AuraEffects.Add(effect);
                    break;
                case CardEffectTiming.Passive:
                    state.PassiveFlags.Add(effect.Type);
                    break;
            }
        }
    }

    private static bool TryFindLane(Card card, bool isPlayerOwner, out CardPlacePoint[] lane, out int index)
    {
        lane = null;
        index = -1;

        var controller = CardPointsController.instance;
        if (controller == null)
            return false;

        lane = isPlayerOwner ? controller.playerCardPoints : controller.enemyCardPoints;
        if (lane == null)
            return false;

        for (int i = 0; i < lane.Length; i++)
        {
            if (lane[i] != null && lane[i].activeCard == card)
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    private sealed class CardRuntimeEffectState
    {
        public CardRuntimeEffectState(Card card, bool isPlayerOwner)
        {
            View = card;
            IsPlayerOwner = isPlayerOwner;
        }

        public Card View { get; private set; }
        public bool IsPlayerOwner { get; }
        public int ShieldValue { get; set; }
        public int ActiveAuraBonuses { get; set; }
        public List<CardEffectDefinition> OnPlay { get; } = new();
        public List<CardEffectDefinition> OnAttackSuccess { get; } = new();
        public List<CardEffectDefinition> OnDamaged { get; } = new();
        public List<CardEffectDefinition> OnOwnerTurnEnd { get; } = new();
        public List<CardEffectDefinition> AuraEffects { get; } = new();
        public HashSet<CardEffectType> PassiveFlags { get; } = new();

        public void Dispose()
        {
            View = null;
            ShieldValue = 0;
            ActiveAuraBonuses = 0;
            OnPlay.Clear();
            OnAttackSuccess.Clear();
            OnDamaged.Clear();
            OnOwnerTurnEnd.Clear();
            AuraEffects.Clear();
            PassiveFlags.Clear();
        }
    }

    private sealed class LeaderRuntimeState
    {
        public LeaderRuntimeState(bool isPlayer)
        {
            IsPlayer = isPlayer;
        }

        public bool IsPlayer { get; }
        public int Shield { get; set; }

        public void Reset()
        {
            Shield = 0;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime wrapper that interprets <see cref="RelicEffectDefinition"/> entries
/// on <see cref="RelicData"/> so designers can author relic behaviour without
/// needing bespoke subclasses.
/// </summary>
public sealed class EffectDrivenRelic : Relic
{
    // ScriptableObject에 설정된 유물 효과 정의를 런타임에서 해석하는 구현입니다.
    private readonly List<RelicEffectDefinition> _persistentEffects = new(); // 상시 유지되는 능력치 조정 효과 목록
    private readonly List<RelicEffectDefinition> _triggeredEffects = new(); // 이벤트에 맞춰 즉시 실행되는 효과 목록
    private readonly Dictionary<RelicEffectDefinition, int> _appliedTotals = new(); // 지속 효과로 적용된 누적 값을 추적
    private readonly Dictionary<RelicEffectDefinition, EffectRuntimeState> _runtimeStates = new(); // 조건/쿨다운/지속시간 추적용 상태
    private static readonly List<Card> _targetBuffer = new(); // 타겟 카드 임시 저장소(할당 최소화)
    private static readonly List<Card> _candidateBuffer = new(); // 타겟 후보 캐시


    public EffectDrivenRelic(RelicData data) : base(data)
    {
        // 유물 데이터에 효과가 없으면 분류 단계 없이 종료합니다.
        if (data?.Effects == null)
            return;

        foreach (var effect in data.Effects)
        {
            // null 항목은 안전하게 무시합니다.
            if (effect == null)
                continue;

            if (IsPersistent(effect.Type)) // 스탯을 유지해야 하는 효과는 별도로 보관
                _persistentEffects.Add(effect);
            else // 나머지는 트리거 시점에만 실행
                _triggeredEffects.Add(effect);

            GetOrCreateState(effect); // 조건/쿨다운/타겟 추적 상태 초기화
        }
    }

    public override void OnAdd()
    {
        ResetAllRuntimeStates(revertAdjustments: false); // 신규 장착 시 상태 초기화
        // 스택 변화에 맞춰 지속 능력치를 다시 계산합니다.
        RefreshPersistentEffects();
        // 즉시 발동해야 하는 효과 OnAdd 트리거 처리
        ExecuteTrigger(RelicEffectTrigger.OnAdd);
    }

    protected override void OnStacksChanged()
    {
        RefreshPersistentEffects();
        ExecuteTrigger(RelicEffectTrigger.OnStacksChanged);
    }

    public override void OnRemove()
    {
        ResetAllRuntimeStates(revertAdjustments: true); // 남은 지속 효과 정리
        // 적용했던 지속 능력치를 정리합니다.
        RevertPersistentEffects();
        ExecuteTrigger(RelicEffectTrigger.OnRemove);
    }

    public override void OnBattleStart()
    {
        ResetAllRuntimeStates(revertAdjustments: true); // 전투 시작 전 상태 리셋
        RefreshPersistentEffects();
        ExecuteTrigger(RelicEffectTrigger.OnBattleStart);
    }

    public override void OnBattleEnd()
    {
        ExecuteTrigger(RelicEffectTrigger.OnBattleEnd);
        ResetAllRuntimeStates(revertAdjustments: true);
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        ExecuteTrigger(RelicEffectTrigger.OnTurnStart, isPlayerTurn);
    }

    public override void OnTurnEnd(bool isPlayerTurn)
    {
        ExecuteTrigger(RelicEffectTrigger.OnTurnEnd, isPlayerTurn);
        AdvanceTurnCounters(isPlayerTurn); // 턴 종료 시 지속시간/쿨다운 감소
    }

    public override void OnCardDrawn(Card card)
    {
        ExecuteTrigger(RelicEffectTrigger.OnCardDrawn, card != null && card.isPlayer, 0, card);
    }

    public override void OnCardPlayed(Card card)
    {
        ExecuteTrigger(RelicEffectTrigger.OnCardPlayed, card != null && card.isPlayer, 0, card);
    }

    public override void OnDamageDealt(int damage, bool isFromPlayer)
    {
        ExecuteTrigger(RelicEffectTrigger.OnDamageDealt, isFromPlayer, damage);
    }

    public override int ModifyPlayerAttack(int baseAttack)
    {
        return ExecuteModifier(baseAttack, RelicEffectTrigger.ModifyPlayerAttack);
    }

    public override int ModifyPlayerMana(int currentMana)
    {
        return ExecuteModifier(currentMana, RelicEffectTrigger.ModifyPlayerMana);
    }
    public override int ModifyCardManaCost(Card card, int currentCost)
    {
        return ExecuteCardModifier(card, currentCost, RelicEffectTrigger.ModifyCardManaCost);
    }
    public override int ModifyCardHealth(Card card, int currentHealth)
    {
        return ExecuteCardModifier(card, currentHealth, RelicEffectTrigger.ModifyCardHealth);
    }
    private static bool IsPersistent(RelicEffectType type)
    {
        return type == RelicEffectType.AdjustPlayerManaCapacity || type == RelicEffectType.AdjustPlayerHealth;
    }

    private void RefreshPersistentEffects()
    {
        // 지속 효과 목록을 돌며 목표치를 산출합니다.
        foreach (var effect in _persistentEffects)
        {
            ApplyPersistentEffect(effect, effect.ResolveValue(Stacks)); // 현재 스택에 맞춰 목표 값을 갱신
        }
    }

    private void RevertPersistentEffects()
    {
        if (_persistentEffects.Count == 0)
            return;

        // 반복 중 수정되는 문제를 피하려고 스냅샷 사용.
        // 반복 중 수정이 일어나지 않도록 사본을 만든 뒤 초기화합니다.
        var snapshot = new List<RelicEffectDefinition>(_persistentEffects.Count);
        foreach (var effect in _persistentEffects)
        {
            if (effect != null && _appliedTotals.ContainsKey(effect))
                snapshot.Add(effect);
        }

        foreach (var effect in snapshot)
            ApplyPersistentEffect(effect, 0);

        _appliedTotals.Clear();
    }

    private void ApplyPersistentEffect(RelicEffectDefinition effect, int desiredTotal)
    {
        if (effect == null)
            return;

        bool isHydrating = RelicSystem.Instance != null && RelicSystem.Instance.IsHydrating;

        _appliedTotals.TryGetValue(effect, out var applied);

        if (isHydrating)
        {
            if (desiredTotal == 0)
                _appliedTotals.Remove(effect);
            else
                _appliedTotals[effect] = desiredTotal;
            return;
        }

        int delta = desiredTotal - applied;
        if (delta == 0)
        {
            if (desiredTotal == 0)
                _appliedTotals.Remove(effect);
            return;
        }

        bool success = false;

        switch (effect.Type)
        {
            case RelicEffectType.AdjustPlayerManaCapacity:
                if (TryGetBattleController(out var manaController))
                {
                    manaController.ApplyPersistentPlayerManaCapacityDelta(delta);
                    success = true;
                }
                else
                {
                    success = ApplyPersistentPlayerManaCapacityToRun(delta);
                }
                break;
            case RelicEffectType.AdjustPlayerHealth:
                if (TryGetBattleController(out var healthController))
                {
                    healthController.ApplyPersistentPlayerHealthDelta(delta);
                    success = true;
                }
                else
                {
                    success = ApplyPersistentPlayerHealthToRun(delta);
                }
                break;
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Persistent effect type {effect.Type} is not supported.");
                break;
        }

        if (!success)
            return;

        if (desiredTotal == 0)
            _appliedTotals.Remove(effect);
        else
            _appliedTotals[effect] = desiredTotal;
    }

    private void ExecuteTrigger(RelicEffectTrigger trigger)
    {
        ExecuteTrigger(trigger, true, 0, null);
    }

    private void ExecuteTrigger(RelicEffectTrigger trigger, bool contextIsPlayer, int damage = 0, Card contextCard = null)
    {
        if (_triggeredEffects.Count == 0)
            return;

        var context = CreateContext(trigger, contextIsPlayer, damage, contextCard);

        // 트리거에 맞는 즉발 효과를 순회하며 적용합니다.
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            if (!MatchesOwnershipFilter(effect, contextIsPlayer))
                continue;

            var state = GetOrCreateState(effect);
            if (state == null)
                continue;

            if (!CanExecuteTriggeredEffect(effect, state, context, out var activatedNow))
                continue;

            var targets = ResolveTargets(effect, context, state, activatedNow);
            bool executed = ApplyTriggeredEffect(effect, context, state, targets, activatedNow);

            if (executed)
            {
                if (activatedNow && effect.Duration.UseDuration)
                    state.BeginDuration(effect.Duration);

                state.MarkTriggered(effect, activatedNow);
            }
        }
    }

    private int ExecuteModifier(int seed, RelicEffectTrigger trigger)
    {
        int value = seed;
        var context = CreateContext(trigger, true, 0, null);
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            var state = GetOrCreateState(effect);
            if (state == null)
                continue;

            if (!CanExecuteTriggeredEffect(effect, state, context, out var activatedNow))
                continue;

            value = ApplyModifierEffect(effect, value); // 누적 수정값을 계산
            if (activatedNow && effect.Duration.UseDuration)
                state.BeginDuration(effect.Duration);
            state.MarkTriggered(effect, activatedNow);
        }
        return value;
    }

    private int ExecuteCardModifier(Card card, int seed, RelicEffectTrigger trigger)
    {
        if (card == null)
            return seed;

        int value = seed;
        bool isPlayerCard = card.isPlayer;
        var context = CreateContext(trigger, isPlayerCard, 0, card);
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            if (!MatchesOwnershipFilter(effect, isPlayerCard))
                continue;

            var state = GetOrCreateState(effect);
            if (state == null)
                continue;

            if (!CanExecuteTriggeredEffect(effect, state, context, out var activatedNow))
                continue;

            value = ApplyCardModifierEffect(effect, card, value);
            if (activatedNow && effect.Duration.UseDuration)
                state.BeginDuration(effect.Duration);
            state.MarkTriggered(effect, activatedNow);
        }
        return Mathf.Max(0, value);
    }
    private int ApplyModifierEffect(RelicEffectDefinition effect, int current)
    {
        int amount = effect.ResolveValue(Stacks);
        switch (effect.Type)
        {
            case RelicEffectType.ModifyPlayerAttackFlat:
                return current + amount;
            case RelicEffectType.ModifyPlayerManaFlat:
                return current + amount;
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Unsupported modifier type {effect.Type} on trigger {effect.Trigger}.");
                return current;
        }
    }

    private int ApplyCardModifierEffect(RelicEffectDefinition effect, Card card, int current)
    {
        int amount = effect.ResolveValue(Stacks);
        switch (effect.Type)
        {
            case RelicEffectType.ModifyCardManaCostFlat:
                return Mathf.Max(0, current + amount);
            case RelicEffectType.ModifyCardHealthFlat:
                return Mathf.Max(0, current + amount);
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Unsupported card modifier type {effect.Type} on trigger {effect.Trigger}.");
                return current;
        }
    }

    private bool ApplyTriggeredEffect(
       RelicEffectDefinition effect,
       RelicTriggerContext context,
       EffectRuntimeState state,
       List<Card> targets,
       bool activatedNow)
    {
        _ = context; // 현재 분기에서는 사용하지 않지만 향후 카드/피해 정보를 활용할 수 있도록 보존
        switch (effect.Type)
        {
            case RelicEffectType.GainPlayerMana:
                if (TryGetBattleController(out var controller))
                {
                    int gain = effect.ResolveValue(Stacks);
                    if (gain != 0)
                    {
                        controller.playerMana = Mathf.Clamp(controller.playerMana + gain, 0, controller.playermaxMana);
                        UIController.instance?.SetPlayerManaText(controller.playerMana);
                        return true;
                    }
                }
                return false;
            case RelicEffectType.DrawCards:
                var deck = GameServices.Deck;
                if (deck == null)
                {
                    Debug.LogWarning("[EffectDrivenRelic] DrawCards effect requires a registered deck service.");
                    return false;
                }
                int count = Mathf.Max(0, effect.ResolveValue(Stacks)); // 스택 기반으로 도출된 카드 수
                if (count > 0)
                {
                    deck.DrawCards(count, DrawReason.Relic);
                    return true;
                }
                return false;
            case RelicEffectType.GainGold:
                var wallet = ServiceRegistry.Get<IWalletService>();
                if (wallet == null)
                {
                    Debug.LogWarning("[EffectDrivenRelic] GainGold effect requires IWalletService.");
                    return false;
                }
                int gold = effect.ResolveValue(Stacks); // 지급할 골드 양
                if (gold != 0)
                {
                    wallet.Add(gold);
                    return true;
                }
                return false;
            case RelicEffectType.AdjustTargetCardManaCostFlat:
                if (effect.Duration.UseDuration && !activatedNow)
                    return false; // 이미 적용 중이면 재적용하지 않음

                if (targets == null || targets.Count == 0)
                    return false;

                if (activatedNow && effect.Duration.UseDuration)
                {
                    RevertCardAdjustments(state);
                    state.ClearAdjustments();
                }

                int manaDelta = effect.ResolveValue(Stacks);
                if (manaDelta == 0)
                    return false;

                bool manaApplied = false;
                foreach (var card in targets)
                {
                    manaApplied |= ApplyCardManaDelta(card, manaDelta, effect, state);
                }
                return manaApplied;
            case RelicEffectType.AdjustTargetCardHealthFlat:
                if (effect.Duration.UseDuration && !activatedNow)
                    return false;

                if (targets == null || targets.Count == 0)
                    return false;

                if (activatedNow && effect.Duration.UseDuration)
                {
                    RevertCardAdjustments(state);
                    state.ClearAdjustments();
                }

                int healthDelta = effect.ResolveValue(Stacks);
                if (healthDelta == 0)
                    return false;

                bool healthApplied = false;
                foreach (var card in targets)
                {
                    healthApplied |= ApplyCardHealthDelta(card, healthDelta, effect, state);
                }
                return healthApplied;
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Unsupported triggered effect type {effect.Type} on trigger {effect.Trigger}.");
                return false;
        }
    }

    // 현재 전투의 HP/턴 정보를 수집해 트리거 조건 검사에 전달합니다.
    private RelicTriggerContext CreateContext(RelicEffectTrigger trigger, bool contextIsPlayer, int damage, Card contextCard)
    {
        int playerHp = 0;
        int playerMaxHp = 0;
        int turnNumber = 0;

        if (TryGetBattleController(out var controller))
        {
            playerHp = controller.playerHealth;
            playerMaxHp = controller.playerMaxHealth;
            turnNumber = controller.CurrentTurnNumber;
        }

        return new RelicTriggerContext(trigger, contextIsPlayer, damage, contextCard, playerHp, playerMaxHp, turnNumber);
    }

    private bool CanExecuteTriggeredEffect(RelicEffectDefinition effect, EffectRuntimeState state, RelicTriggerContext context, out bool activatedNow)
    {
        activatedNow = false;
        if (effect == null || state == null)
            return false;

        bool hasDuration = effect.Duration != null && effect.Duration.UseDuration;
        bool hasCooldown = effect.Cooldown != null && effect.Cooldown.UseCooldown;
        bool durationActive = hasDuration && state.HasActiveDuration;

        if (hasCooldown && state.IsOnCooldown && !durationActive)
            return false;

        bool conditionMet = EvaluateCondition(effect, state, context);

        if (hasDuration)
        {
            if (!durationActive)
            {
                if (!conditionMet)
                    return false;

                activatedNow = true;
            }
        }
        else
        {
            if (!conditionMet)
                return false;
            activatedNow = true;
        }

        if (!hasDuration && hasCooldown && state.IsOnCooldown && !activatedNow)
            return false;

        return true;
    }

    private bool EvaluateCondition(RelicEffectDefinition effect, EffectRuntimeState state, RelicTriggerContext context)
    {
        if (effect == null)
            return false;

        var condition = effect.TriggerCondition;
        if (condition == null || condition.ConditionType == RelicTriggerConditionType.Always)
            return true;

        switch (condition.ConditionType)
        {
            case RelicTriggerConditionType.PlayerTurnOnly:
                return context.IsPlayerContext;
            case RelicTriggerConditionType.EnemyTurnOnly:
                return !context.IsPlayerContext;
            case RelicTriggerConditionType.PlayerHpBelowOrEqual:
                if (condition.HpThreshold <= 0)
                    return true;
                if (context.PlayerHp <= 0)
                    return true;
                return context.PlayerHp <= condition.HpThreshold;
            case RelicTriggerConditionType.EveryNthTurn:
                int interval = Mathf.Max(1, condition.TurnInterval);
                int offset = Mathf.Max(0, condition.StartTurnOffset);
                int basis = condition.CountEnemyTurns
                    ? state.GetUpcomingTotalTurnCount()
                    : state.GetUpcomingPlayerTurnCount(context.IsPlayerContext);
                int relative = basis - offset;
                if (relative < 0)
                    return false;
                return relative % interval == 0;
            default:
                return true;
        }
    }

    private List<Card> ResolveTargets(RelicEffectDefinition effect, RelicTriggerContext context, EffectRuntimeState state, bool activatedNow)
    {
        _ = context; // 타겟 필터 조건 확장 대비해 인자 유지
        _targetBuffer.Clear();

        var targeting = effect.Targeting;
        if (targeting == null || targeting.Mode == RelicTargetingMode.None)
            return _targetBuffer;

        if (!activatedNow && effect.Duration.UseDuration)
        {
            CollectCardsFromAdjustments(state, _targetBuffer);
            if (_targetBuffer.Count > 0)
                return _targetBuffer;
        }

        var hand = HandController.instance;
        if (hand == null || hand.heldCards == null || hand.heldCards.Count == 0)
            return _targetBuffer;

        _candidateBuffer.Clear();
        foreach (var card in hand.heldCards)
        {
            if (card == null)
                continue;
            if (!IsCardOwnerMatch(card, targeting.OwnerFilter))
                continue;
            _candidateBuffer.Add(card);
        }

        if (_candidateBuffer.Count == 0)
            return _targetBuffer;

        switch (targeting.Mode)
        {
            case RelicTargetingMode.AllHandCards:
                _targetBuffer.AddRange(_candidateBuffer);
                break;
            case RelicTargetingMode.RandomHandCard:
                int required = Mathf.Clamp(targeting.RandomCount, 1, _candidateBuffer.Count);
                if (targeting.AllowDuplicates)
                {
                    for (int i = 0; i < required; i++)
                    {
                        int idx = UnityEngine.Random.Range(0, _candidateBuffer.Count);
                        _targetBuffer.Add(_candidateBuffer[idx]);
                    }
                }
                else
                {
                    int remaining = _candidateBuffer.Count;
                    for (int i = 0; i < required && remaining > 0; i++)
                    {
                        int idx = UnityEngine.Random.Range(0, remaining);
                        var selected = _candidateBuffer[idx];
                        _targetBuffer.Add(selected);
                        _candidateBuffer.RemoveAt(idx);
                        remaining--;
                    }
                }
                break;
        }

        _candidateBuffer.Clear();
        return _targetBuffer;
    }

    private bool ApplyCardManaDelta(Card card, int delta, RelicEffectDefinition effect, EffectRuntimeState state)
    {
        if (card == null || delta == 0)
            return false;

        int original = card.manaCost;
        int updated = Mathf.Max(0, original + delta);
        if (updated == original)
            return false;

        card.manaCost = updated;
        card.UpdateCardDisplay();

        if (effect.Duration.UseDuration)
        {
            int applied = updated - original;
            state.RecordManaAdjustment(card.InstanceId, applied);
        }

        return true;
    }

    private bool ApplyCardHealthDelta(Card card, int delta, RelicEffectDefinition effect, EffectRuntimeState state)
    {
        if (card == null || delta == 0)
            return false;

        int original = card.currentHealth;
        int updated = Mathf.Max(0, original + delta);
        if (updated == original)
            return false;

        card.currentHealth = updated;
        card.UpdateCardDisplay();

        if (effect.Duration.UseDuration)
        {
            int applied = updated - original;
            state.RecordHealthAdjustment(card.InstanceId, applied);
        }

        return true;
    }

    // 턴 종료 시 지속시간과 쿨다운을 감소시키고 만료된 카드 버프를 되돌립니다.
    private void AdvanceTurnCounters(bool isPlayerTurn)
    {
        foreach (var kvp in _runtimeStates)
        {
            var state = kvp.Value;
            if (state == null)
                continue;

            bool expired = state.TickTurn(isPlayerTurn);
            if (expired)
            {
                RevertCardAdjustments(state);
                state.ClearAdjustments();
            }
        }
    }

    // 유물 효과 상태를 전부 초기화합니다. revertAdjustments=true이면 카드에 적용된 임시 수치를 되돌립니다.
    private void ResetAllRuntimeStates(bool revertAdjustments)
    {
        foreach (var kvp in _runtimeStates)
        {
            var state = kvp.Value;
            if (state == null)
                continue;

            if (revertAdjustments)
                RevertCardAdjustments(state);

            state.ResetAll();
        }
    }

    private void RevertCardAdjustments(EffectRuntimeState state)
    {
        if (state == null)
            return;

        if (state.ManaAdjustments.Count > 0)
        {
            foreach (var kv in state.ManaAdjustments)
            {
                if (!TryFindCardInHand(kv.Key, out var card) || card == null)
                    continue;

                int reverted = Mathf.Max(0, card.manaCost - kv.Value);
                if (reverted != card.manaCost)
                {
                    card.manaCost = reverted;
                    card.UpdateCardDisplay();
                }
            }
        }

        if (state.HealthAdjustments.Count > 0)
        {
            foreach (var kv in state.HealthAdjustments)
            {
                if (!TryFindCardInHand(kv.Key, out var card) || card == null)
                    continue;

                int reverted = Mathf.Max(0, card.currentHealth - kv.Value);
                if (reverted != card.currentHealth)
                {
                    card.currentHealth = reverted;
                    card.UpdateCardDisplay();
                }
            }
        }
    }

    private void CollectCardsFromAdjustments(EffectRuntimeState state, List<Card> buffer)
    {
        if (state == null || buffer == null)
            return;

        if (state.ManaAdjustments.Count > 0)
        {
            foreach (var kv in state.ManaAdjustments)
            {
                if (!TryFindCardInHand(kv.Key, out var card) || card == null)
                    continue;
                if (!buffer.Contains(card))
                    buffer.Add(card);
            }
        }

        if (state.HealthAdjustments.Count > 0)
        {
            foreach (var kv in state.HealthAdjustments)
            {
                if (!TryFindCardInHand(kv.Key, out var card) || card == null)
                    continue;
                if (!buffer.Contains(card))
                    buffer.Add(card);
            }
        }
    }

    private EffectRuntimeState GetOrCreateState(RelicEffectDefinition effect)
    {
        if (effect == null)
            return null;

        if (!_runtimeStates.TryGetValue(effect, out var state) || state == null)
        {
            state = new EffectRuntimeState(effect);
            _runtimeStates[effect] = state;
        }

        return state;
    }

    private static bool MatchesOwnershipFilter(RelicEffectDefinition effect, bool contextIsPlayer)
    {
        if (effect == null)
            return true;

        string payload = effect.PayloadId;
        if (string.IsNullOrWhiteSpace(payload))
            return contextIsPlayer; // 기본: 플레이어 컨텍스트에만 적용.

        if (string.Equals(payload, "Any", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(payload, "Player", StringComparison.OrdinalIgnoreCase))
            return contextIsPlayer;
        if (string.Equals(payload, "Enemy", StringComparison.OrdinalIgnoreCase))
            return !contextIsPlayer;

        return contextIsPlayer;
    }
    private static bool IsCardOwnerMatch(Card card, RelicTargetingOwner owner)
    {
        if (card == null)
            return false;

        switch (owner)
        {
            case RelicTargetingOwner.PlayerHand:
                return card.isPlayer;
            case RelicTargetingOwner.EnemyHand:
                return !card.isPlayer;
            case RelicTargetingOwner.AnyHand:
            default:
                return true;
        }
    }

    private static bool TryFindCardInHand(string instanceId, out Card card)
    {
        card = null;
        if (string.IsNullOrEmpty(instanceId))
            return false;

        var hand = HandController.instance;
        if (hand == null || hand.heldCards == null)
            return false;

        foreach (var held in hand.heldCards)
        {
            if (held == null)
                continue;
            if (string.Equals(held.InstanceId, instanceId, StringComparison.Ordinal))
            {
                card = held;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBattleController(out BattleController controller)
    {
        controller = BattleController.instance;
        return controller != null;
    }
    private bool ApplyPersistentPlayerManaCapacityToRun(int delta)
    {
        if (delta == 0)
            return true;

        var db = ServiceRegistry.Get<IDatabase>();
        if (db == null)
            return false;

        string runId = ResolveActiveRunId();
        if (string.IsNullOrEmpty(runId))
            return false;

        try
        {
            db.ApplyRunRelicEnergyDelta(runId, delta);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EffectDrivenRelic] ApplyRunRelicEnergyDelta failed: {e.Message}");
            return false;
        }

        RunCacheSynchronizer.Sync();
        return true;
    }

    private bool ApplyPersistentPlayerHealthToRun(int delta)
    {
        if (delta == 0)
            return true;

        var db = ServiceRegistry.Get<IDatabase>();
        if (db == null)
            return false;

        string runId = ResolveActiveRunId();
        if (string.IsNullOrEmpty(runId))
            return false;

        try
        {
            db.ApplyRunRelicHpDelta(runId, delta, adjustCurrentHp: true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EffectDrivenRelic] ApplyRunRelicHpDelta failed: {e.Message}");
            return false;
        }

        RunCacheSynchronizer.Sync();
        return true;
    }

    private static string ResolveActiveRunId()
    {
        if (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
            return GameContext.I.RunId;
        return PlayerPrefs.GetString("lastRunId", string.Empty);
    }
    /// <summary>
    /// 조건 계산에 필요한 턴/HP/카드 정보를 담는 컨텍스트입니다.
    /// 트리거별로 필요한 데이터를 간단히 확장할 수 있도록 구조체로 제공합니다.
    /// </summary>
    private readonly struct RelicTriggerContext
    {
        public RelicTriggerContext(
            RelicEffectTrigger trigger,
            bool isPlayerContext,
            int damage,
            Card card,
            int playerHp,
            int playerMaxHp,
            int battleTurnNumber)
        {
            Trigger = trigger;
            IsPlayerContext = isPlayerContext;
            Damage = damage;
            Card = card;
            PlayerHp = playerHp;
            PlayerMaxHp = playerMaxHp;
            BattleTurnNumber = battleTurnNumber;
        }

        public RelicEffectTrigger Trigger { get; }
        public bool IsPlayerContext { get; }
        public int Damage { get; }
        public Card Card { get; }
        public int PlayerHp { get; }
        public int PlayerMaxHp { get; }
        public int BattleTurnNumber { get; }
    }

    /// <summary>
    /// 각 유물 효과별로 지속시간/쿨다운/적용된 카드 변화를 추적하는 런타임 상태입니다.
    /// 데이터 기반으로 설정한 Duration, Cooldown, Targeting 값을 해석할 때 사용됩니다.
    /// </summary>
    private sealed class EffectRuntimeState
    {
        private readonly RelicEffectDefinition _definition;
        private readonly Dictionary<string, int> _manaAdjustments = new();
        private readonly Dictionary<string, int> _healthAdjustments = new();

        public EffectRuntimeState(RelicEffectDefinition definition)
        {
            _definition = definition;
        }

        public int CooldownRemaining { get; private set; }
        public int DurationRemaining { get; private set; }
        public int ObservedPlayerTurns { get; private set; }
        public int ObservedTotalTurns { get; private set; }

        public bool HasActiveDuration => _definition.Duration != null && _definition.Duration.UseDuration && DurationRemaining > 0;
        public bool IsOnCooldown => _definition.Cooldown != null && _definition.Cooldown.UseCooldown && CooldownRemaining > 0;

        public IReadOnlyDictionary<string, int> ManaAdjustments => _manaAdjustments;
        public IReadOnlyDictionary<string, int> HealthAdjustments => _healthAdjustments;

        public void ResetAll()
        {
            CooldownRemaining = 0;
            DurationRemaining = 0;
            ObservedPlayerTurns = 0;
            ObservedTotalTurns = 0;
            _manaAdjustments.Clear();
            _healthAdjustments.Clear();
        }

        public void ClearAdjustments()
        {
            _manaAdjustments.Clear();
            _healthAdjustments.Clear();
        }

        public void BeginDuration(RelicDurationSettings duration)
        {
            if (duration == null || !duration.UseDuration)
            {
                DurationRemaining = 0;
                return;
            }

            DurationRemaining = Mathf.Max(1, duration.TurnCount);
        }

        public void MarkTriggered(RelicEffectDefinition definition, bool activated)
        {
            if (definition?.Cooldown == null || !definition.Cooldown.UseCooldown)
                return;

            if (activated || !definition.Duration.UseDuration)
                CooldownRemaining = Mathf.Max(0, definition.Cooldown.TurnCount);
        }

        public bool TickTurn(bool isPlayerTurn)
        {
            if (isPlayerTurn)
                ObservedPlayerTurns++;
            ObservedTotalTurns++;

            if (_definition.Cooldown != null && _definition.Cooldown.UseCooldown && CooldownRemaining > 0)
            {
                if (isPlayerTurn || _definition.Cooldown.CountEnemyTurns)
                    CooldownRemaining = Mathf.Max(0, CooldownRemaining - 1);
            }

            if (_definition.Duration != null && _definition.Duration.UseDuration && DurationRemaining > 0)
            {
                bool shouldTick = isPlayerTurn || _definition.Duration.CountEnemyTurns;
                if (shouldTick)
                {
                    DurationRemaining = Mathf.Max(0, DurationRemaining - 1);
                    return DurationRemaining == 0;
                }
            }

            return false;
        }

        public void RecordManaAdjustment(string cardId, int delta)
        {
            if (string.IsNullOrEmpty(cardId) || delta == 0)
                return;

            if (_manaAdjustments.TryGetValue(cardId, out var current))
                _manaAdjustments[cardId] = current + delta;
            else
                _manaAdjustments.Add(cardId, delta);
        }

        public void RecordHealthAdjustment(string cardId, int delta)
        {
            if (string.IsNullOrEmpty(cardId) || delta == 0)
                return;

            if (_healthAdjustments.TryGetValue(cardId, out var current))
                _healthAdjustments[cardId] = current + delta;
            else
                _healthAdjustments.Add(cardId, delta);
        }

        public int GetUpcomingPlayerTurnCount(bool isPlayerTurn)
        {
            return ObservedPlayerTurns + (isPlayerTurn ? 1 : 0);
        }

        public int GetUpcomingTotalTurnCount()
        {
            return ObservedTotalTurns + 1;
        }
    }
}





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
        }
    }

    public override void OnAdd()
    {
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
        // 적용했던 지속 능력치를 정리합니다.
        RevertPersistentEffects();
        ExecuteTrigger(RelicEffectTrigger.OnRemove);
    }

    public override void OnBattleStart()
    {
        RefreshPersistentEffects();
        ExecuteTrigger(RelicEffectTrigger.OnBattleStart);
    }

    public override void OnBattleEnd()
    {
        ExecuteTrigger(RelicEffectTrigger.OnBattleEnd);
    }

    public override void OnTurnStart(bool isPlayerTurn)
    {
        ExecuteTrigger(RelicEffectTrigger.OnTurnStart, isPlayerTurn);
    }

    public override void OnTurnEnd(bool isPlayerTurn)
    {
        ExecuteTrigger(RelicEffectTrigger.OnTurnEnd, isPlayerTurn);
    }

    public override void OnCardDrawn(Card card)
    {
        ExecuteTrigger(RelicEffectTrigger.OnCardDrawn, card != null && card.isPlayer);
    }

    public override void OnCardPlayed(Card card)
    {
        ExecuteTrigger(RelicEffectTrigger.OnCardPlayed, card != null && card.isPlayer);
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
        ExecuteTrigger(trigger, true);
    }

    private void ExecuteTrigger(RelicEffectTrigger trigger, bool contextIsPlayer, int damage = 0)
    {
        _ = damage; // 현재 구현에서는 타격량을 사용하지 않지만 시그니처 유지

        // 트리거에 맞는 즉발 효과를 순회하며 적용합니다.
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            if (!MatchesOwnershipFilter(effect, contextIsPlayer))
                continue;

            ApplyTriggeredEffect(effect, damage);
        }
    }

    private int ExecuteModifier(int seed, RelicEffectTrigger trigger)
    {
        int value = seed;
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            value = ApplyModifierEffect(effect, value); // 누적 수정값을 계산
        }
        return value;
    }

    private int ExecuteCardModifier(Card card, int seed, RelicEffectTrigger trigger)
    {
        if (card == null)
            return seed;

        int value = seed;
        bool isPlayerCard = card.isPlayer;
        foreach (var effect in _triggeredEffects)
        {
            if (effect == null || effect.Trigger != trigger)
                continue;

            if (!MatchesOwnershipFilter(effect, isPlayerCard))
                continue;

            value = ApplyCardModifierEffect(effect, card, value);
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
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Unsupported card modifier type {effect.Type} on trigger {effect.Trigger}.");
                return current;
        }
    }

    private void ApplyTriggeredEffect(RelicEffectDefinition effect, int damage)
    {
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
                    }
                }
                break;
            case RelicEffectType.DrawCards:
                var deck = GameServices.Deck;
                if (deck == null)
                {
                    Debug.LogWarning("[EffectDrivenRelic] DrawCards effect requires a registered deck service.");
                    break;
                }
                int count = Mathf.Max(0, effect.ResolveValue(Stacks)); // 스택 기반으로 도출된 카드 수
                if (count > 0)
                    deck.DrawCards(count, DrawReason.Relic);
                break;
            case RelicEffectType.GainGold:
                var wallet = ServiceRegistry.Get<IWalletService>();
                if (wallet == null)
                {
                    Debug.LogWarning("[EffectDrivenRelic] GainGold effect requires IWalletService.");
                    break;
                }
                int gold = effect.ResolveValue(Stacks); // 지급할 골드 양
                if (gold != 0)
                    wallet.Add(gold);
                break;
            default:
                Debug.LogWarning($"[EffectDrivenRelic] Unsupported triggered effect type {effect.Type} on trigger {effect.Trigger}.");
                break;
        }
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

    private static bool TryGetBattleController(out BattleController controller)
    {
        controller = BattleController.instance;
        return controller != null;
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
}





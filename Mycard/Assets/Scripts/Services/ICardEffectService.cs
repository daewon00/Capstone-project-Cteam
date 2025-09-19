public interface ICardEffectService
{
    void RegisterBoardCard(Card card, bool isPlayerOwner, CardEffectRuntimeSnapshot snapshot = null);
    void UnregisterBoardCard(Card card);
    DamageMitigationResult ProcessCardDamage(Card card, Card attacker, int incomingDamage, DamageSourceKind sourceKind);
    void HandleCardDamaged(Card card, Card attacker, int appliedDamage, DamageSourceKind sourceKind);
    DamageMitigationResult ProcessLeaderDamage(bool isPlayerLeader, int incomingDamage);
    void HandleAttackResolved(CardAttackContext context);
    void HandleTurnEnded(bool isPlayerTurn);
    bool HasEffect(Card card, CardEffectType effectType);
    void ForceDestroyCard(Card target, Card killer = null);
    void ResetAll();
    CardEffectRuntimeSnapshot CaptureCardState(Card card);
    int GetLeaderShield(bool isPlayerLeader);
    void RestoreLeaderShield(bool isPlayerLeader, int shieldValue);
}

/// <summary>
/// 피해 계산 결과를 나타냅니다.
/// </summary>
public readonly struct DamageMitigationResult
{
    public int RemainingDamage { get; }
    public int BlockedDamage { get; }

    public DamageMitigationResult(int remainingDamage, int blockedDamage)
    {
        RemainingDamage = remainingDamage;
        BlockedDamage = blockedDamage;
    }
}

public enum DamageSourceKind
{
    Attack,
    Retaliation,
    Effect,
    Leader
}

public readonly struct CardDamageResult
{
    public CardDamageResult(int appliedDamage, bool targetDestroyed)
    {
        AppliedDamage = appliedDamage;
        TargetDestroyed = targetDestroyed;
    }

    public int AppliedDamage { get; }
    public bool TargetDestroyed { get; }
}

public readonly struct CardAttackContext
{
    public CardAttackContext(Card attacker, bool attackerIsPlayer, int laneIndex, int baseAttack,
        int damageToPrimary, int damageToLeader, Card primaryTarget, bool hitCard)
    {
        Attacker = attacker;
        AttackerIsPlayer = attackerIsPlayer;
        LaneIndex = laneIndex;
        BaseAttack = baseAttack;
        DamageToPrimary = damageToPrimary;
        DamageToLeader = damageToLeader;
        PrimaryTarget = primaryTarget;
        HitCard = hitCard;
    }

    public Card Attacker { get; }
    public bool AttackerIsPlayer { get; }
    public int LaneIndex { get; }
    public int BaseAttack { get; }
    public int DamageToPrimary { get; }
    public int DamageToLeader { get; }
    public Card PrimaryTarget { get; }
    public bool HitCard { get; }
}

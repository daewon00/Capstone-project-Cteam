using System;

/// <summary>
/// Serializable container for card-specific effect runtime data that must persist across saves.
/// </summary>
[Serializable]
public sealed class CardEffectRuntimeSnapshot
{
    /// <summary>Remaining shield on the card (self target).</summary>
    public int shield;

    /// <summary>Total aura bonus currently contributed while the card is alive.</summary>
    public int auraBonus;
}

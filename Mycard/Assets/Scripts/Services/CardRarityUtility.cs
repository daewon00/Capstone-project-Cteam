using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CardAcquisitionContext
{
    Reward = 0,
    Shop = 1
}

/// <summary>
/// Provides base rarity weights and pricing multipliers for different acquisition contexts.
/// </summary>
public static class CardRarityConfig
{
    private static readonly Dictionary<CardRarity, float> DefaultWeights = new()
    {
        { CardRarity.Common, 70f },
        { CardRarity.Rare, 20f },
        { CardRarity.Heroic, 8f },
        { CardRarity.Legendary, 2f }
    };

    private static readonly Dictionary<CardAcquisitionContext, Dictionary<CardRarity, float>> ContextWeights = new()
    {
        { CardAcquisitionContext.Reward, new Dictionary<CardRarity, float>(DefaultWeights) },
        { CardAcquisitionContext.Shop, new Dictionary<CardRarity, float>(DefaultWeights) }
    };

    private static readonly Dictionary<CardRarity, float> PriceMultipliers = new()
    {
        { CardRarity.Common, 1f },
        { CardRarity.Rare, 1.4f },
        { CardRarity.Heroic, 1.8f },
        { CardRarity.Legendary, 2.3f }
    };

    private static readonly CardRarity[] Ordered =
    {
        CardRarity.Common,
        CardRarity.Rare,
        CardRarity.Heroic,
        CardRarity.Legendary
    };

    public static IReadOnlyList<CardRarity> OrderedRarities => Ordered;

    public static Dictionary<CardRarity, float> GetBaseWeights(CardAcquisitionContext context)
    {
        if (!ContextWeights.TryGetValue(context, out var weights))
        {
            weights = DefaultWeights;
        }

        return new Dictionary<CardRarity, float>(weights);
    }

    public static float GetPriceMultiplier(CardRarity rarity)
    {
        return PriceMultipliers.TryGetValue(rarity, out var multiplier) ? multiplier : 1f;
    }
}

/// <summary>
/// Mutable rarity weight collection that downstream systems can modify.
/// </summary>
public sealed class RarityWeightBuilder
{
    private readonly Dictionary<CardRarity, float> _weights;

    public RarityWeightBuilder(Dictionary<CardRarity, float> source)
    {
        _weights = new Dictionary<CardRarity, float>();
        foreach (CardRarity rarity in Enum.GetValues(typeof(CardRarity)))
        {
            float value = 0f;
            if (source != null && source.TryGetValue(rarity, out var existing))
                value = Mathf.Max(0f, existing);
            _weights[rarity] = value;
        }
    }

    public void Add(CardRarity rarity, float delta)
    {
        _weights[rarity] = Mathf.Max(0f, _weights[rarity] + delta);
    }

    public void Multiply(CardRarity rarity, float factor)
    {
        _weights[rarity] = Mathf.Max(0f, _weights[rarity] * factor);
    }

    public void Set(CardRarity rarity, float value)
    {
        _weights[rarity] = Mathf.Max(0f, value);
    }

    public IReadOnlyDictionary<CardRarity, float> Weights => _weights;

    internal Dictionary<CardRarity, float> Build()
    {
        return new Dictionary<CardRarity, float>(_weights);
    }
}

/// <summary>
/// Utility that selects cards based on rarity weights and optional exclusions.
/// </summary>
public sealed class WeightedCardPicker
{
    private readonly Dictionary<CardRarity, List<CardScriptableObject>> _cardsByRarity = new();
    private readonly Func<CardScriptableObject, bool> _filter;

    public WeightedCardPicker(IEnumerable<CardScriptableObject> cards, Func<CardScriptableObject, bool> filter = null)
    {
        _filter = filter;
        foreach (var rarity in CardRarityConfig.OrderedRarities)
        {
            _cardsByRarity[rarity] = new List<CardScriptableObject>();
        }

        if (cards == null)
            return;

        foreach (var card in cards)
        {
            if (card == null)
                continue;
            if (!string.IsNullOrEmpty(card.CardId) && (filter == null || filter(card)))
            {
                if (!_cardsByRarity.TryGetValue(card.Rarity, out var list))
                {
                    list = new List<CardScriptableObject>();
                    _cardsByRarity[card.Rarity] = list;
                }
                list.Add(card);
            }
        }
    }

    public List<CardScriptableObject> PickMany(CardAcquisitionContext context, int count, Func<float> nextFloat, Func<int, int> nextInt, HashSet<string> excludeIds = null)
    {
        if (count <= 0)
            return new List<CardScriptableObject>();

        var results = new List<CardScriptableObject>(count);
        var exclusions = excludeIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < count; i++)
        {
            var card = PickOne(context, nextFloat, nextInt, exclusions);
            if (card == null)
                break;

            results.Add(card);
            if (!string.IsNullOrEmpty(card.CardId))
                exclusions.Add(card.CardId);
        }

        return results;
    }

    public CardScriptableObject PickOne(CardAcquisitionContext context, Func<float> nextFloat, Func<int, int> nextInt, HashSet<string> excludeIds = null)
    {
        var candidates = BuildCandidateMap(excludeIds);
        if (candidates.Count == 0)
            return null;

        var weights = CardRarityConfig.GetBaseWeights(context);
        weights = GameEvents.ApplyRarityWeightModifiers(context, weights) ?? new Dictionary<CardRarity, float>();

        var filteredWeights = new Dictionary<CardRarity, float>();
        foreach (var rarity in CardRarityConfig.OrderedRarities)
        {
            if (!candidates.TryGetValue(rarity, out var list) || list.Count == 0)
                continue;

            if (!weights.TryGetValue(rarity, out var weight) || weight <= 0f)
                continue;

            filteredWeights[rarity] = weight;
        }

        if (filteredWeights.Count == 0)
        {
            foreach (var rarity in candidates.Keys)
            {
                filteredWeights[rarity] = 1f;
            }
        }

        float total = filteredWeights.Values.Sum();
        if (total <= 0f)
            return null;

        float roll = Mathf.Clamp01(nextFloat != null ? nextFloat() : UnityEngine.Random.value);
        float cumulative = 0f;
        CardRarity selectedRarity = CardRarity.Common;
        bool found = false;

        foreach (var rarity in CardRarityConfig.OrderedRarities)
        {
            if (!filteredWeights.TryGetValue(rarity, out var weight))
                continue;

            cumulative += weight / total;
            if (roll <= cumulative || Mathf.Approximately(cumulative, 1f))
            {
                selectedRarity = rarity;
                found = true;
                break;
            }
        }

        if (!found)
        {
            selectedRarity = filteredWeights.Keys.Last();
        }

        if (!candidates.TryGetValue(selectedRarity, out var pool) || pool.Count == 0)
            return null;

        int index = pool.Count == 1
            ? 0
            : Mathf.Clamp(nextInt != null ? nextInt(pool.Count) : UnityEngine.Random.Range(0, pool.Count), 0, pool.Count - 1);

        return pool[index];
    }

    private Dictionary<CardRarity, List<CardScriptableObject>> BuildCandidateMap(HashSet<string> excludeIds)
    {
        var map = new Dictionary<CardRarity, List<CardScriptableObject>>();

        foreach (var kvp in _cardsByRarity)
        {
            if (kvp.Value == null || kvp.Value.Count == 0)
                continue;

            List<CardScriptableObject> filtered;
            if (excludeIds == null || excludeIds.Count == 0)
            {
                filtered = kvp.Value.Where(card => card != null && !string.IsNullOrEmpty(card.CardId)).ToList();
            }
            else
            {
                filtered = kvp.Value.Where(card => card != null && !string.IsNullOrEmpty(card.CardId) && !excludeIds.Contains(card.CardId)).ToList();
            }

            if (filtered.Count > 0)
            {
                map[kvp.Key] = filtered;
            }
        }

        return map;
    }
}

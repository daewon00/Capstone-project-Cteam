using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 희귀도와 강화 여부에 따라 카드 배경(Front)과 희귀도 엠블렘을 매핑합니다.
/// </summary>
[CreateAssetMenu(fileName = "CardVisualProfile", menuName = "Card Visuals/Profile", order = 0)]
public class CardVisualProfile : ScriptableObject
{
    [Serializable]
    private struct RarityVisual
    {
        public CardRarity rarity;
        [Tooltip("기본 상태에서 사용할 전면 배경(Sprite)")] public Sprite front;
        [Tooltip("강화 상태에서 사용할 전면 배경(Sprite)")] public Sprite frontUpgraded;
        [Tooltip("카드 정면 중앙에 배치할 희귀도 엠블렘(Sprite)")] public Sprite emblem;
    }

    [Header("기본(미등록 등급 공통) 스프라이트")]
    [SerializeField] private Sprite defaultFront;
    [SerializeField] private Sprite defaultFrontUpgraded;
    [SerializeField] private Sprite defaultEmblem;

    [Header("등급별 개별 설정")]
    [SerializeField] private List<RarityVisual> overrides = new List<RarityVisual>();

    private readonly Dictionary<CardRarity, RarityVisual> _cache = new();

    private void OnEnable()
    {
        RebuildCache();
    }

    private void RebuildCache()
    {
        _cache.Clear();
        if (overrides == null) return;
        foreach (var item in overrides)
        {
            if (_cache.ContainsKey(item.rarity))
                _cache[item.rarity] = item;
            else
                _cache.Add(item.rarity, item);
        }
    }

    public Sprite GetFront(CardRarity rarity, bool upgraded)
    {
        if (_cache.Count == 0) RebuildCache();
        if (_cache.TryGetValue(rarity, out var visual))
        {
            var sprite = upgraded ? visual.frontUpgraded : visual.front;
            if (sprite != null) return sprite;
        }
        return upgraded ? defaultFrontUpgraded : defaultFront;
    }

    public Sprite GetEmblem(CardRarity rarity)
    {
        if (_cache.Count == 0) RebuildCache();
        if (_cache.TryGetValue(rarity, out var visual))
        {
            if (visual.emblem != null) return visual.emblem;
        }
        return defaultEmblem;
    }
}

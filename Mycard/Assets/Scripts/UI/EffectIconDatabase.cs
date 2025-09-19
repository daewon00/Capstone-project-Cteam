using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectIconDatabase", menuName = "Game Data/Effect Icon Database")]
public class EffectIconDatabase : ScriptableObject
{
    [SerializeField]
    private List<EffectIconData> effectIcons = new();

    private Dictionary<CardEffectType, EffectIconData> _iconMap;

    private void OnEnable()
    {
        if (_iconMap == null)
            _iconMap = new Dictionary<CardEffectType, EffectIconData>();
        else
            _iconMap.Clear();

        foreach (var entry in effectIcons)
        {
            if (entry == null)
                continue;
            if (_iconMap.ContainsKey(entry.effectType))
                continue;
            _iconMap.Add(entry.effectType, entry);
        }
    }

    public Sprite GetIcon(CardEffectType effectType)
    {
        var data = GetData(effectType);
        return data != null ? data.effectIcon : null;
    }

    public EffectIconData GetData(CardEffectType effectType)
    {
        if (_iconMap == null || _iconMap.Count == 0)
            OnEnable();

        return _iconMap != null && _iconMap.TryGetValue(effectType, out var data)
            ? data
            : null;
    }
}

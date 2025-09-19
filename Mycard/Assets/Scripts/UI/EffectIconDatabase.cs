using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectIconDatabase", menuName = "Game Data/Effect Icon Database")]
public class EffectIconDatabase : ScriptableObject
{
    [SerializeField]
    private List<EffectIconData> effectIcons = new();

    private Dictionary<CardEffectType, Sprite> _iconMap;

    private void OnEnable()
    {
        if (_iconMap == null)
            _iconMap = new Dictionary<CardEffectType, Sprite>();
        else
            _iconMap.Clear();

        foreach (var entry in effectIcons)
        {
            if (entry == null || entry.effectIcon == null)
                continue;
            if (_iconMap.ContainsKey(entry.effectType))
                continue;
            _iconMap.Add(entry.effectType, entry.effectIcon);
        }
    }

    public Sprite GetIcon(CardEffectType effectType)
    {
        if (_iconMap == null || _iconMap.Count == 0)
            OnEnable();

        return _iconMap != null && _iconMap.TryGetValue(effectType, out var icon)
            ? icon
            : null;
    }
}

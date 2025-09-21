using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RelicData", menuName = "Data/Relic", order = 1)]
public class RelicData : ScriptableObject
{
    [Header("Display")]
    public string relicId;
    public string displayName;

    [TextArea] public string description;
    public Sprite icon;

    [Header("Rule")]
    public bool stackable = false;
    [Min(1)] public int maxStacks = 1;

    [Header("Effects")]
    // 에디터에서 구성한 유물 효과 정의 목록입니다.
    [SerializeField] private List<RelicEffectDefinition> effects = new();

    public IReadOnlyList<RelicEffectDefinition> Effects => effects;
    public bool HasEffectDefinitions => effects != null && effects.Count > 0;
}

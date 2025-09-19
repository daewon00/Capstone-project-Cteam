using UnityEngine;

[System.Serializable]
public class EffectIconData
{
    [Tooltip("어떤 카드 효과에 대응하는 아이콘인지 지정합니다.")]
    public CardEffectType effectType;
    [Tooltip("이 효과를 나타낼 아이콘 스프라이트")]
    public Sprite effectIcon;
    [Tooltip("아이콘 옆에 표시할 숫자(예: +1)")]
    public int effectValue = 0;
}

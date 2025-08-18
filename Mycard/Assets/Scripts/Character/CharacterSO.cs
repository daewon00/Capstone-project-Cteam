using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Character", order = 2)]
public class CharacterSO : ScriptableObject
{
    public string characterName;
    public Sprite portraitSprite;  // 초상화 이미지
    public string description;     // 인물 설명
}

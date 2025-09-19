using UnityEngine;

/// <summary>
/// 캐릭터의 표시 정보와 설명을 보관하는 스크립터블 오브젝트입니다.
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Character", order = 2)]
public class CharacterSO : ScriptableObject
{
    /// <summary>
    /// 캐릭터의 고유 이름입니다.
    /// </summary>
    public string characterName;
    /// <summary>
    /// UI에 표시할 초상화 이미지입니다.
    /// </summary>
    public Sprite portraitSprite;  // 초상화 이미지
    /// <summary>
    /// 캐릭터 설명 문구입니다.
    /// </summary>
    public string description;     // 인물 설명
}

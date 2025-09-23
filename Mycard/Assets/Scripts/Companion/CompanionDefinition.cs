using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료의 시작 카드, 유물, 능력치를 정의하는 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "Companion_", menuName = "Game/Companion Definition")]
public class CompanionDefinition : ScriptableObject
{
    [Header("Identity")]
    /// <summary>
    /// 동료를 구분하기 위한 고유 ID입니다.
    /// </summary>
    public string CompanionId;           // 예: "COMP_WARRIOR"
    /// <summary>
    /// UI에 노출될 동료 이름입니다.
    /// </summary>
    public string DisplayName;           // 예: "전사"
    /// <summary>
    /// 동료 설명과 플레이 스타일을 안내하는 문구입니다.
    /// </summary>
    [TextArea] public string Description;
    /// <summary>
    /// 동료 카드를 표시할 초상화 이미지입니다.
    /// </summary>
    public Sprite Portrait;

    [Header("Start Loadout")]
    /// <summary>
    /// 런 시작 시 덱에 포함될 카드 ID 목록입니다.
    /// </summary>
    public List<string> StartingCardIds = new();     // 예: "CARD_STRIKE", "CARD_DEFEND"
    /// <summary>
    /// 런 시작 시 부여할 유물 ID 목록입니다.
    /// </summary>
    public List<string> StartingRelicIds = new();    // 일반 유물 (필요 시 수동 추가)
    /// <summary>
    /// 런 시작 시 지급할 포션 ID 목록입니다.
    /// </summary>
    public List<string> StartingPotionIds = new();   // 선택 (없어도 됨)

    [Header("Base Stats Mods (optional)")]
    /// <summary>
    /// 기본 최대 체력에 더해질 보너스 수치입니다.
    /// </summary>
    public int MaxHpBonus;
    /// <summary>
    /// 시작 골드에 더해질 추가 금액입니다.
    /// </summary>
    public int GoldBonus;
    /// <summary>
    /// 시작 에너지 최대치에 더해질 보너스입니다.
    /// </summary>
    public int EnergyMaxBonus;
}

// CardInstance.cs
// 이 파일은 ScriptableObject(원본 데이터)와는 별개로,
// 게임 플레이 중에 사용되는 카드 한 장 한 장의 '실물' 상태를 저장하는 클래스입니다.

using System;

[Serializable] // 이 클래스의 데이터를 저장하거나 에디터에 표시할 수 있도록 합니다.
public class CardInstance
{
    /// <summary>
    /// 이번 판(Run)에서만 사용되는 이 카드만의 고유 시리얼 번호입니다.
    /// 예: "RUN-ABC-0001", "RUN-ABC-0002"
    /// </summary>
    public string InstanceId;

    /// <summary>
    /// 이 카드의 원본이 무엇인지 알려주는 '제품 모델명'입니다.
    /// 예: "CARD_STRIKE", "CARD_DEFEND"
    /// </summary>
    public string BaseCardId;

    /// <summary>
    /// 이 카드가 강화되었는지 여부입니다.
    /// </summary>
    public bool IsUpgraded;

    // --- 전투 중에만 일시적으로 변하는 값들 ---
    // 이 값들은 전투가 끝나면 초기화됩니다.

    /// <summary>
    /// 이번 턴 또는 이번 전투에만 적용되는 코스트 변화량입니다. (예: -1)
    /// </summary>
    public int TempCostDelta;

    /// <summary>
    /// "이번 턴에만 소멸" 과 같이, 전투 중에만 적용되는 특수 효과를 JSON 텍스트로 저장할 수 있습니다. (확장용)
    /// </summary>
    public string CombatModsJson;

    /// <summary>
    /// '갓 구운 빵'을 만드는 생성자입니다.
    /// </summary>
    /// <param name="instanceId">총괄 셰프가 발급한 고유 시리얼 번호</param>
    /// <param name="baseId">원본 카드의 제품 모델명</param>
    /// <param name="upgraded">강화 여부</param>
    public CardInstance(string instanceId, string baseId, bool upgraded)
    {
        InstanceId = instanceId;
        BaseCardId = baseId;
        IsUpgraded = upgraded;
    }
}
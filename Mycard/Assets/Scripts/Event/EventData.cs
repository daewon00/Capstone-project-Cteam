using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save; // NodeType을 사용하기 위함

/// <summary>
/// 이벤트의 원본 데이터를 정의하는 설계도로, 선택지와 설명을 포함합니다.
/// </summary>
[CreateAssetMenu(fileName = "New Event", menuName = "Events/New Event")]
public class EventScriptableObject : ScriptableObject
{
    public string eventId;
    public string description;
    public List<EventChoice> choices;
}

/// <summary>
/// 이벤트 화면에 노출될 선택지 한 항목과 그 효과 목록을 나타냅니다.
/// </summary>
[System.Serializable]
public class EventChoice
{
    public string id;
    public string label; // 버튼에 표시될 텍스트
    public List<EventEffect> effects;
}

/// <summary>
/// 선택지를 통해 적용될 개별 효과 데이터를 표현합니다.
/// </summary>
[System.Serializable]
public class EventEffect
{
    public string type; // 예: "HpDelta", "GoldDelta", "AddCard"
    public int amount;
    public string refId; // 카드를 추가할 경우 CardId 등
}

/// <summary>
/// 이어하기 기능을 위해 현재 이벤트 진행 상황을 직렬화한 DTO입니다.
/// </summary>
[System.Serializable]
public class EventSessionDTO
{
    public string eventId;
    public bool resolved; // 이미 해결된 이벤트인지 여부
    public string pickedChoiceId;
    public string description;  // UI가 SO 없이도 복원 가능하도록 텍스트 포함
    public EventChoiceDTO[] choices;
}

/// <summary>
/// 직렬화된 선택지 항목을 나타냅니다.
/// </summary>
[System.Serializable]
public class EventChoiceDTO
{
    public string id;
    public string label;
    public EventEffectDTO[] effects;
}

/// <summary>
/// 직렬화된 선택지 효과 항목을 나타냅니다.
/// </summary>
[System.Serializable]
public class EventEffectDTO
{
    public string type;
    public int amount;
    public string refId;
}

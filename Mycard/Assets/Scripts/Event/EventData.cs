using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Save; // NodeType을 사용하기 위함
using UnityEngine.Serialization;

/// <summary>
/// 이벤트의 원본 데이터를 정의하는 설계도로, 선택지와 설명을 포함합니다.
/// </summary>
[CreateAssetMenu(fileName = "New Event", menuName = "Events/New Event")]
public class EventScriptableObject : ScriptableObject
{
    private const string DefaultStageId = "stage_1";

    public string eventId;

    [FormerlySerializedAs("description")]
    [SerializeField, HideInInspector] private string legacyDescription;

    [FormerlySerializedAs("choices")]
    [SerializeField, HideInInspector] private List<EventChoice> legacyChoices = new();

    public List<EventStage> stages = new();

    public void EnsureStageData()
    {
        if (stages == null)
        {
            stages = new List<EventStage>();
        }

        if (stages.Count == 0)
        {
            stages.Add(new EventStage
            {
                stageId = DefaultStageId,
                description = legacyDescription,
                choices = legacyChoices != null ? CloneChoices(legacyChoices) : new List<EventChoice>()
            });
        }
    }

    public EventStage GetStageOrFirst(string stageId)
    {
        EnsureStageData();
        if (!string.IsNullOrEmpty(stageId))
        {
            var match = stages.FirstOrDefault(s => string.Equals(s.stageId, stageId, StringComparison.Ordinal));
            if (match != null) return match;
            Debug.LogWarning($"[EventSO] Stage '{stageId}' not found in event '{eventId}'. Falling back to first stage.");
        }
        return stages.FirstOrDefault();
    }

    public EventStage GetFirstStage()
    {
        EnsureStageData();
        return stages.FirstOrDefault();
    }

    private static List<EventChoice> CloneChoices(List<EventChoice> source)
    {
        return source?.Where(choice => choice != null).Select(CloneChoice).ToList() ?? new List<EventChoice>();
    }

    private static EventChoice CloneChoice(EventChoice original)
    {
        if (original == null) return null;

        return new EventChoice
        {
            id = original.id,
            label = original.label,
            nextStageId = original.nextStageId,
            effects = original.effects != null
                ? original.effects.Select(CloneEffect).ToList()
                : new List<EventEffect>()
        };
    }

    private static EventEffect CloneEffect(EventEffect original)
    {
        if (original == null) return null;

        return new EventEffect
        {
            type = original.type,
            amount = original.amount,
            refId = original.refId,
            quantity = original.quantity,
            upgrade = original.upgrade
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureStageData();
    }
#endif
}

/// <summary>
/// 이벤트를 여러 단계로 표현하기 위한 스테이지 데이터입니다.
/// </summary>
[Serializable]
public class EventStage
{
    public string stageId = "stage_1";
    [TextArea]
    public string description;
    public List<EventChoice> choices = new();
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
    public string nextStageId;
}

/// <summary>
/// 선택지를 통해 적용될 개별 효과 데이터를 표현합니다.
/// </summary>
[System.Serializable]
public class EventEffect
{
    [FormerlySerializedAs("type")]
    public EventEffectType type; // 예: HpDelta, GoldDelta, AddCard
    public int amount;
    public string refId; // 카드를 추가할 경우 CardId 등
    public int quantity = 1; // AddCard 타입에서 사용할 카드 장수
    public bool upgrade;     // AddCard 타입에서 사용할 업그레이드 여부
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
    public string stageId;
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
    public string nextStageId;
}

/// <summary>
/// 직렬화된 선택지 효과 항목을 나타냅니다.
/// </summary>
[System.Serializable]
public class EventEffectDTO
{
    [FormerlySerializedAs("type")]
    public EventEffectType type;
    public int amount;
    public string refId;
    public int quantity;
    public bool upgrade;
}

/// <summary>
/// 이벤트에서 지원하는 효과 타입입니다.
/// </summary>
public enum EventEffectType
{
    [InspectorName("체력 변경 (HpDelta)")]
    HpDelta,
    [InspectorName("골드 변경 (GoldDelta)")]
    GoldDelta,
    [InspectorName("카드 추가 (AddCard)")]
    AddCard,
    [InspectorName("최대 체력 변경 (MaxHpDelta)")]
    MaxHpDelta,
    [InspectorName("맵 복귀 (ReturnToMap)")]
    ReturnToMap,
    [InspectorName("유물 획득 (AddRelic)")]
    AddRelic,
    [InspectorName("체력 % 회복 (HealPercent)")]
    HealPercent,
    [InspectorName("카드 변환 (TransformCard)")]
    TransformCard,
    [InspectorName("저주 추가 (AddCurse)")]
    AddCurse,
    [InspectorName("카드 강화 (UpgradeRandomCard)")]
    UpgradeRandomCard,
    [InspectorName("카드 제거 (RemoveCard)")]
    RemoveCard
}

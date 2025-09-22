using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이벤트 노드에 배정할 이벤트 목록과 정책을 정의하는 풀입니다.
/// </summary>
[CreateAssetMenu(fileName = "New Event Pool", menuName = "Events/Event Pool")]
public sealed class EventPoolDefinition : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("배정할 이벤트 ID (Resources/Events/<ID>.asset)")]
        public string eventId;

        [Min(0), Tooltip("선택 가중치 (0이면 제외)")]
        public int weight = 1;

        [Tooltip("이벤트가 배정될 수 있는 최소 층 인덱스 (0 기반)")]
        public int minLayer = 0;

        [Tooltip("이벤트가 배정될 수 있는 최대 층 인덱스 (포함)")]
        public int maxLayer = 99;
    }

    [SerializeField] private List<Entry> entries = new();

    [SerializeField, Tooltip("풀의 이벤트가 소진되었을 때 사용할 기본 이벤트 ID")]
    private string fallbackEventId = "GoldenIdolEvent";

    public IReadOnlyList<Entry> Entries => entries;

    public string FallbackEventId => fallbackEventId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (entry.minLayer < 0) entry.minLayer = 0;
            if (entry.maxLayer < entry.minLayer) entry.maxLayer = entry.minLayer;
        }
    }
#endif
}

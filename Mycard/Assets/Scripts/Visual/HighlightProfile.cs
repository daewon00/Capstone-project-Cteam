using System;
using UnityEngine;

// 설계도(데이터) 역할: 상태별 하이라이트 표현 규칙을 정의
[CreateAssetMenu(menuName = "Highlight/New Highlight Profile", fileName = "HighlightProfile")]
public class HighlightProfile : ScriptableObject
{
    public enum ApplyMode
    {
        ColorOnly,      // 머티리얼 색/틴트만 변경
        MaterialSwap,   // 지정 머티리얼로 교체(상태별)
        ObjectToggle    // 상태별 오브젝트 on/off
    }

    public enum ColorMatchMode
    {
        FirstMatch,     // 우선순위 상 가장 먼저 매칭된 1개 속성만 적용
        AllMatches      // 우선순위 목록 중 매칭되는 모든 속성에 적용
    }

    public enum HighlightStateType
    {
        Off,
        Allowed,
        Blocked
    }

    [Serializable]
    public class StateVisualSettings
    {
        [Header("Color Settings")]
        public bool applyColor = true;
        [ColorUsage(true, true)] public Color color = Color.white;

        [Header("Material Settings")]
        public bool applyMaterial = false;
        public Material material; // 전체 렌더러에 교체 적용(선택)

        [Header("Object Toggle Settings")]
        public bool toggleObjects = false;
        public GameObject[] objectsToEnable; // 상태 진입 시 활성화할 오브젝트들
        public GameObject[] objectsToDisable; // 상태 진입 시 비활성화할 오브젝트들
    }

    [Header("Apply Behavior")]
    public ApplyMode applyMode = ApplyMode.ColorOnly;

    [Tooltip("컬러를 적용할 셰이더 속성 이름 우선순위. 첫 매치에 적용.")]
    public string[] colorPropertyNames = new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor" };

    [Tooltip("MaterialPropertyBlock을 사용하여 색 변경(머티리얼 인스턴싱 방지)")]
    public bool useMaterialPropertyBlock = true;

    [Tooltip("색 적용 대상 속성 선택 방식(첫 매치 1개 vs 전체 매치)")]
    public ColorMatchMode colorMatchMode = ColorMatchMode.FirstMatch;

    [Header("State Visuals")] 
    public StateVisualSettings off = new StateVisualSettings
    {
        applyColor = true,
        color = new Color(1f, 1f, 1f, 0f), // 투명(기본)
        applyMaterial = false,
        toggleObjects = false
    };

    public StateVisualSettings allowed = new StateVisualSettings
    {
        applyColor = true,
        color = new Color(0.35f, 1f, 0.35f, 1f), // 연한 초록
        applyMaterial = false,
        toggleObjects = false
    };

    public StateVisualSettings blocked = new StateVisualSettings
    {
        applyColor = true,
        color = new Color(1f, 0.35f, 0.35f, 1f), // 연한 빨강
        applyMaterial = false,
        toggleObjects = false
    };

    public StateVisualSettings GetSettings(HighlightStateType state)
    {
        switch (state)
        {
            case HighlightStateType.Allowed: return allowed;
            case HighlightStateType.Blocked: return blocked;
            default: return off;
        }
    }

    private void OnValidate()
    {
        if (colorPropertyNames == null || colorPropertyNames.Length == 0)
        {
            colorPropertyNames = new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor" };
        }
    }
}

using UnityEngine;
using System;
using Game.Save;

/// <summary>
/// 하나의 튜토리얼 흐름(예: 코어 온보딩)에 대한 단계 정의를 담습니다.
/// </summary>
[CreateAssetMenu(menuName = "Tutorial/Sequence", fileName = "TutorialSequence")]
public class TutorialSequenceDefinition : ScriptableObject
{
    [SerializeField] private string tutorialId = TutorialIds.CoreOnboarding;
    [SerializeField] private TutorialStepConfig[] steps = Array.Empty<TutorialStepConfig>();

    public string TutorialId => string.IsNullOrEmpty(tutorialId) ? TutorialIds.CoreOnboarding : tutorialId;
    public TutorialStepConfig[] Steps => steps ?? Array.Empty<TutorialStepConfig>();

    public TutorialStepConfig GetStep(TutorialStep step)
    {
        if (steps == null) return null;
        foreach (var cfg in steps)
        {
            if (cfg != null && cfg.Step == step)
            {
                return cfg;
            }
        }
        return null;
    }

    public TutorialStep GetNextStep(TutorialStep current)
    {
        if (steps == null || steps.Length == 0) return TutorialStep.Completed;
        Array.Sort(steps, (a, b) => a?.Step.CompareTo(b?.Step ?? TutorialStep.None) ?? -1);
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null) continue;
            if (steps[i].Step == current)
            {
                for (int j = i + 1; j < steps.Length; j++)
                {
                    if (steps[j] != null)
                    {
                        return steps[j].Step;
                    }
                }
                break;
            }
        }
        return TutorialStep.Completed;
    }
}

[Serializable]
public class TutorialStepConfig
{
    [Header("Identification")]
    [Tooltip("이 단계가 속한 튜토리얼 단계 ID입니다.")]
    public TutorialStep Step = TutorialStep.None;
    [Tooltip("플레이어에게 보여줄 안내 문구입니다.")]
    [TextArea] public string Message;

    [Header("Placement")]
    [Tooltip("오버레이 패널이 처음 배치될 기준 위치입니다.")]
    public TutorialAnchor PrimaryAnchor = TutorialAnchor.Bottom;
    [Tooltip("하이라이트와 겹칠 때 대체로 사용할 위치입니다.")]
    public TutorialAnchor FallbackAnchor = TutorialAnchor.Top;
    [Tooltip("항상 보조 앵커 위치를 우선 적용할지 여부입니다.")]
    public bool PreferFallback;

    [Header("Highlight")]
    [Tooltip("강조(하이라이트)할 TutorialTarget의 ID입니다.")]
    public string HighlightTargetId;
    [Tooltip("해당 타깃이 없어도 단계를 진행할 수 있는지 여부입니다.")]
    public bool HighlightOptional;

    [Header("Interaction")]
    [Tooltip("해당 단계에서 다른 UI 입력을 얼마나 차단할지 설정합니다.")]
    public TutorialInteractionGate InteractionGate = TutorialInteractionGate.None;
    [Tooltip("다음 단계로 가기 위해 필요한 플레이어 행동 유형입니다.")]
    public TutorialRequiredActionType RequiredAction = TutorialRequiredActionType.None;
    [Tooltip("RequiredAction이 ButtonClick/카드 플레이 등일 때 매칭할 추가 식별자입니다.")]
    public string ActionId;
    [Tooltip("RequiredAction이 NodeTravel일 때 허용할 Act 번호입니다. -1은 전체 허용입니다.")]
    public int TargetAct = -1;
    [Tooltip("RequiredAction이 NodeTravel일 때 허용할 층 번호입니다. -1은 전체 허용입니다.")]
    public int TargetFloor = -1;
    [Tooltip("RequiredAction이 NodeTravel일 때 허용할 노드 인덱스입니다. -1은 전체 허용입니다.")]
    public int TargetNodeIndex = -1;
    [Tooltip("별도 행동 요구가 없을 때 오버레이 탭만으로도 진행할 수 있는지 여부입니다.")]
    public bool AllowTapToContinue = true;
    [Tooltip("문구를 보여주자마자 자동으로 다음 단계로 넘어갈지 여부입니다.")]
    public bool AutoAdvance;

    public bool MatchesAction(TutorialRequiredActionType actionType, string context)
    {
        if (RequiredAction != actionType) return false;

        switch (actionType)
        {
            case TutorialRequiredActionType.None:
            case TutorialRequiredActionType.ShowOverlayOnly:
            case TutorialRequiredActionType.BattleCompleted:
                return true;
            case TutorialRequiredActionType.ButtonClick:
            case TutorialRequiredActionType.CardPlay:
                if (string.IsNullOrEmpty(ActionId)) return true;
                return string.Equals(ActionId, context, StringComparison.OrdinalIgnoreCase);
            case TutorialRequiredActionType.NodeTravel:
                if (TryParseNodeContext(context, out var act, out var floor, out var node))
                {
                    bool actOk = TargetAct < 0 || TargetAct == act;
                    bool floorOk = TargetFloor < 0 || TargetFloor == floor;
                    bool nodeOk = TargetNodeIndex < 0 || TargetNodeIndex == node;
                    return actOk && floorOk && nodeOk;
                }
                return false;
            default:
                return false;
        }
    }

    public bool IsNodeAllowed(int act, int floor, int nodeIndex)
    {
        if (RequiredAction != TutorialRequiredActionType.NodeTravel)
        {
            return InteractionGate != TutorialInteractionGate.BlockOthers &&
                   InteractionGate != TutorialInteractionGate.Exclusive;
        }

        bool actOk = TargetAct < 0 || TargetAct == act;
        bool floorOk = TargetFloor < 0 || TargetFloor == floor;
        bool nodeOk = TargetNodeIndex < 0 || TargetNodeIndex == nodeIndex;
        return actOk && floorOk && nodeOk;
    }

    private static bool TryParseNodeContext(string context, out int act, out int floor, out int nodeIndex)
    {
        act = -1;
        floor = -1;
        nodeIndex = -1;
        if (string.IsNullOrEmpty(context)) return false;
        var parts = context.Split(':');
        if (parts.Length < 3) return false;
        return int.TryParse(parts[0], out act)
               && int.TryParse(parts[1], out floor)
               && int.TryParse(parts[2], out nodeIndex);
    }
}

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
    public TutorialStep Step = TutorialStep.None;
    [TextArea] public string Message;

    [Header("Placement")]
    public TutorialAnchor PrimaryAnchor = TutorialAnchor.Bottom;
    public TutorialAnchor FallbackAnchor = TutorialAnchor.Top;
    public bool PreferFallback;

    [Header("Highlight")]
    public string HighlightTargetId;
    public bool HighlightOptional;

    [Header("Interaction")]
    public TutorialInteractionGate InteractionGate = TutorialInteractionGate.None;
    public TutorialRequiredActionType RequiredAction = TutorialRequiredActionType.None;
    public string ActionId;
    public int TargetAct = -1;
    public int TargetFloor = -1;
    public int TargetNodeIndex = -1;
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

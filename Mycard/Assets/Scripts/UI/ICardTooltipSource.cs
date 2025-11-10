using UnityEngine;

public interface ICardTooltipSource
{
    CardTooltipData GetTooltipData();
    Vector3 GetTooltipAnchorWorldPos();
    bool ShouldUseHandOffset { get; }
    bool IsTooltipValid { get; }
}

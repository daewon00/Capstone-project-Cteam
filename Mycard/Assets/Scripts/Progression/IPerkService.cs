using System.Collections.Generic;
using Game.Save;

public interface IPerkService
{
    IReadOnlyList<PerkDefinition> GetAllDefinitions();
    IReadOnlyList<PerkAllocation> GetAllocations(string profileId);
    bool TryPurchase(string profileId, string perkId, int levels, out string error);

    // v3.0: compute and persist run snapshot for current run
    void ComputeRunSnapshotAndPersist(string profileId, string runId);

    // Debug/preview: compute aggregates without persisting
    Dictionary<string, (float flat, float percent)> ComputeAggregatesForProfile(string profileId);

    // v2.0 staging: apply a full set of target levels atomically
    bool ApplyAdjustments(string profileId, System.Collections.Generic.Dictionary<string, int> targetLevels, out string error);
}

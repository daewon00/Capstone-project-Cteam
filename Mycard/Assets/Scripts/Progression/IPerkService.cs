using System.Collections.Generic;
using Game.Save;

/// <summary>
/// 특전 정의 조회와 구매, 스냅샷 계산을 제공하는 서비스 계약입니다.
/// </summary>
public interface IPerkService
{
    IReadOnlyList<PerkDefinition> GetAllDefinitions();
    IReadOnlyList<PerkAllocation> GetAllocations(string profileId);
    bool TryPurchase(string profileId, string perkId, int levels, out string error);

    // v3.0: 현재 런을 위한 특전 스냅샷 계산 및 저장
    void ComputeRunSnapshotAndPersist(string profileId, string runId);

    // 디버그/프리뷰: 저장 없이 집계만 계산
    Dictionary<string, (float flat, float percent)> ComputeAggregatesForProfile(string profileId);

    // v2.0 준비: 목표 레벨 세트를 한 번에 적용
    bool ApplyAdjustments(string profileId, System.Collections.Generic.Dictionary<string, int> targetLevels, out string error);
}

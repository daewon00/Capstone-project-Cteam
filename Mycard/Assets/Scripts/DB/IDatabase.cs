using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

// '의존성 주입'을 위해, DB가 제공해야 할 기능의 목록을 정의하는 인터페이스입니다.
// 지금은 이벤트와 관련된 기능만 구현되어 있지만 확장 될 수 있습니다.
public interface IDatabase
{
    // --- 프로필 ---
    PlayerProfile LoadProfile(string profileId);

    // --- 런(Run) 기본 정보 ---
    RunLoadResult LoadCurrentRun(string runId);
    void UpdateRunGold(string runId, int newGold);
    void UpdateRunHp(string runId, int newHp);
    void UpsertCurrentRun(CurrentRun run);
    void UpdateRunPosition(string runId, int act, int floor, int nodeIndex);
    
    /// <summary>
    /// 현재 진행 중인 런을 요약 저장하고 이어하기 데이터를 정리(삭제)합니다.
    /// DatabaseManager.EndRunAndSummarize와 동일한 의미의 인터페이스 노출입니다.
    /// </summary>
    void EndRunAndSummarize(RunSummary summary);

    // --- 이벤트 세션 (단일 행) ---
    // '이어하기' 시 이벤트를 복원하기 위한 전용 저장/로드 기능입니다.
    void UpsertActiveEventSession(string runId, string json);
    string LoadActiveEventSessionJson(string runId);
    void DeleteActiveEventSession(string runId);

    // --- 노드 상태 (단일 행) ---
    // 이벤트 결과를 맵 노드에 기록하기 위한 기능입니다.
    void UpsertNodeState(MapNodeState node);
    void ReplaceCardsInDeck(string runId, IEnumerable<CardInDeck> cards);
    void ReplaceRelics(string runId, IEnumerable<RelicInPossession> relics);
    void ReplacePotions(string runId, IEnumerable<PotionInPossession> potions);

    // --- RNG 상태 ---
    // 결정론적 무작위성 유지를 위해 도메인별 RNG 상태를 저장/로드합니다.
    List<RngState> LoadRngStates(string runId);
    void UpsertRngStates(string runId, IEnumerable<RngState> states);

    // ==== 덱 상태(CardRuntimeState) 관리 API ====
    /// <summary>
    /// 특정 런의 모든 카드 상태를 DB에 덮어씁니다. (전체 스냅샷 저장)
    /// 내부적으로 트랜잭션을 사용하여 원자성을 보장합니다.
    /// </summary>
    void UpsertCardRuntimeStates(string runId, IEnumerable<CardRuntimeState> cards);

    /// <summary>
    /// 특정 런의 모든 카드 상태를 불러옵니다. OrderInPile 내림차순으로 정렬됩니다.
    /// </summary>
    List<CardRuntimeState> LoadCardRuntimeStates(string runId);

    /// <summary>
    /// 특정 런의 특정 위치에 있는 카드들만 효율적으로 불러옵니다. OrderInPile 내림차순으로 정렬됩니다.
    /// </summary>
    List<CardRuntimeState> LoadCardRuntimeStates(string runId, CardLocation location);

    // --- v3.0: Perk Snapshot & Achievements ---
    void ReplaceRunPerkSnapshot(string runId, IEnumerable<RunPerkSnapshot> rows);
    List<RunPerkSnapshot> LoadRunPerkSnapshot(string runId);
    AchievementProgress LoadAchievementProgress(string profileId, string achievementId);
    void UpsertAchievementProgress(AchievementProgress row);
    void AddPerkPoints(string profileId, int delta);
    List<PerkAllocation> LoadPerkAllocations(string profileId);
    void SavePerkAllocations(string profileId, IEnumerable<PerkAllocation> perks);
    void ApplyPerkAdjustments(string profileId, IEnumerable<PerkAllocation> perks, int pointsDelta);
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

// 기존의 DatabaseManager 싱글턴을 'IDatabase'라는 표준 규격에 맞게 감싸주는 어댑터 클래스입니다.
public sealed class DatabaseFacade : IDatabase
{
    public PlayerProfile LoadProfile(string profileId)
        => DatabaseManager.Instance.LoadProfile(profileId);

    public RunLoadResult LoadCurrentRun(string runId)
        => DatabaseManager.Instance.LoadCurrentRun(runId);

    public void UpdateRunGold(string runId, int newGold)
        => DatabaseManager.Instance.UpdateRunGold(runId, newGold);

    public void UpdateRunHp(string runId, int newHp)
        => DatabaseManager.Instance.UpdateRunHp(runId, newHp);

    public void UpsertCurrentRun(CurrentRun run)
        => DatabaseManager.Instance.UpsertCurrentRun(run);

    public void UpdateRunPosition(string runId, int act, int floor, int nodeIndex)
        => DatabaseManager.Instance.UpdateRunPosition(runId, act, floor, nodeIndex);

    public void UpsertActiveEventSession(string runId, string json)
        => DatabaseManager.Instance.UpsertActiveEventSession(runId, json);

    public string LoadActiveEventSessionJson(string runId)
        => DatabaseManager.Instance.LoadActiveEventSessionJson(runId);

    public void DeleteActiveEventSession(string runId)
        => DatabaseManager.Instance.DeleteActiveEventSession(runId);

    public void UpsertNodeState(MapNodeState node)
        => DatabaseManager.Instance.UpsertNodeState(node);

    public void ReplaceCardsInDeck(string runId, System.Collections.Generic.IEnumerable<CardInDeck> cards)
        => DatabaseManager.Instance.ReplaceCardsInDeck(runId, cards);

    public void ReplaceRelics(string runId, System.Collections.Generic.IEnumerable<RelicInPossession> relics)
        => DatabaseManager.Instance.ReplaceRelics(runId, relics);

    public void ReplacePotions(string runId, System.Collections.Generic.IEnumerable<PotionInPossession> potions)
        => DatabaseManager.Instance.ReplacePotions(runId, potions);

    // --- RNG 상태 ---
    public System.Collections.Generic.List<RngState> LoadRngStates(string runId)
        => DatabaseManager.Instance.LoadRngStates(runId);

    public void UpsertRngStates(string runId, System.Collections.Generic.IEnumerable<RngState> states)
        => DatabaseManager.Instance.UpsertRngStates(runId, states);

    // ==== 덱 상태(CardRuntimeState) 관리 API ====
    public void UpsertCardRuntimeStates(string runId, System.Collections.Generic.IEnumerable<CardRuntimeState> cards)
        => DatabaseManager.Instance.UpsertCardRuntimeStates(runId, cards);

    public System.Collections.Generic.List<CardRuntimeState> LoadCardRuntimeStates(string runId)
        => DatabaseManager.Instance.LoadCardRuntimeStates(runId);

    public System.Collections.Generic.List<CardRuntimeState> LoadCardRuntimeStates(string runId, CardLocation location)
        => DatabaseManager.Instance.LoadCardRuntimeStates(runId, location);

    public void EndRunAndSummarize(RunSummary summary)
        => DatabaseManager.Instance.EndRunAndSummarize(summary);

    // v3.0 helpers
    public void ReplaceRunPerkSnapshot(string runId, System.Collections.Generic.IEnumerable<RunPerkSnapshot> rows)
        => DatabaseManager.Instance.ReplaceRunPerkSnapshot(runId, rows);

    public System.Collections.Generic.List<RunPerkSnapshot> LoadRunPerkSnapshot(string runId)
        => DatabaseManager.Instance.LoadRunPerkSnapshot(runId);

    public AchievementProgress LoadAchievementProgress(string profileId, string achievementId)
        => DatabaseManager.Instance.LoadAchievementProgress(profileId, achievementId);

    public void UpsertAchievementProgress(AchievementProgress row)
        => DatabaseManager.Instance.UpsertAchievementProgress(row);

    public void AddPerkPoints(string profileId, int delta)
        => DatabaseManager.Instance.AddPerkPoints(profileId, delta);

    public System.Collections.Generic.List<PerkAllocation> LoadPerkAllocations(string profileId)
        => DatabaseManager.Instance.LoadPerkAllocations(profileId);

    public void SavePerkAllocations(string profileId, System.Collections.Generic.IEnumerable<PerkAllocation> perks)
        => DatabaseManager.Instance.SavePerkAllocations(profileId, perks);

    public void ApplyPerkAdjustments(string profileId, System.Collections.Generic.IEnumerable<PerkAllocation> perks, int pointsDelta)
        => DatabaseManager.Instance.ApplyPerkAdjustments(profileId, perks, pointsDelta);
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

/// <summary>
/// 업적 정의를 로드하고 진행도를 집계하며 특전 포인트 지급을 담당하는 서비스입니다.
/// </summary>
public sealed class AchievementService : IAchievementService
{
    private readonly IDatabase _db;
    private readonly Dictionary<string, AchievementDefinition> _defs;
    private readonly Dictionary<string, int> _pending = new();
    private readonly List<string> _newlyUnlocked = new();
    private string _profileId = "P1";

    // 런 단위 업적을 위해 사용되는 인메모리 카운터
    private readonly Dictionary<string, int> _singleRunFloorCount = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// DB 핸들을 받아 업적 서비스를 초기화하고 메타 이벤트를 구독합니다.
    /// </summary>
    public AchievementService(IDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _defs = LoadDefinitions();

        // 서비스가 직접 처리하는 메타 이벤트를 구독합니다.
        try
        {
            MetaEvents.OnFloorReached += HandleFloorReached;
            MetaEvents.OnRunEnded += HandleRunEnded;
        }
        catch { }
    }

    /// <summary>
    /// 업적 집계 대상 프로필을 변경합니다.
    /// </summary>
    public void RebindProfile(string profileId)
    {
        _profileId = string.IsNullOrEmpty(profileId) ? "P1" : profileId;
    }

    /// <summary>
    /// 지정한 업적에 진행도 증가치를 더해 둡니다.
    /// </summary>
    public void ReportProgress(string achievementId, int delta)
    {
        if (string.IsNullOrEmpty(achievementId) || delta == 0) return;
        _pending.TryGetValue(achievementId, out int cur);
        _pending[achievementId] = cur + delta;
    }

    /// <summary>
    /// 누적된 진행도가 목표를 달성했는지 확인하고 필요하면 해금합니다.
    /// </summary>
    public void UnlockIfEligible(string achievementId)
    {
        if (!_defs.TryGetValue(achievementId, out var def)) return;
        var row = _db.LoadAchievementProgress(_profileId, achievementId);
        if (row != null && row.IsUnlocked) return;

        int current = row?.Progress ?? 0;
        _pending.TryGetValue(achievementId, out int delta);
        int total = current + delta;
        if (total >= Mathf.Max(1, def.ProgressTarget))
        {
            UnlockInternal(achievementId, def.PointsReward);
            _pending[achievementId] = 0; // 소진된 진행도를 초기화합니다.
        }
    }

    /// <summary>
    /// 조건 검사 없이 바로 업적을 해금합니다.
    /// </summary>
    public void UnlockDirect(string achievementId, int pointsAward)
    {
        UnlockInternal(achievementId, pointsAward);
    }

    /// <summary>
    /// 업적을 해금하고 포인트를 지급한 뒤 브로드캐스트합니다.
    /// </summary>
    private void UnlockInternal(string achievementId, int points)
    {
        var row = _db.LoadAchievementProgress(_profileId, achievementId) ?? new AchievementProgress
        {
            ProfileId = _profileId,
            AchievementId = achievementId,
        };
        if (row.IsUnlocked) return; // 이미 해금되어 있으면 무시합니다.
        row.IsUnlocked = true;
        row.UnlockedAtUtc = DateTime.UtcNow.ToString("o");
        _db.UpsertAchievementProgress(row);
        _db.AddPerkPoints(_profileId, points);
        _newlyUnlocked.Add(achievementId);

        // 실시간 UI 알림(토스트)을 위해 해금 사실을 브로드캐스트합니다.
        try
        {
            _defs.TryGetValue(achievementId, out var def);
            MetaEvents.RaiseAchievementUnlocked(new MetaEvents.AchievementUnlockedPayload
            {
                ProfileId = _profileId,
                AchievementId = achievementId,
                DisplayName = def != null ? def.DisplayName : achievementId,
                Description = def != null ? def.Description : string.Empty,
                Points = (def != null && def.PointsReward > 0) ? def.PointsReward : points,
                UnlockedAtUtc = row.UnlockedAtUtc,
                RunId = GameContext.I != null ? GameContext.I.RunId : string.Empty
            });
        }
        catch { }
    }

    /// <summary>
    /// 누적된 진행도를 DB에 반영하고 아직 미해금 업적을 재검사합니다.
    /// </summary>
    public void Flush()
    {
        // 대기 중인 진행도를 일괄 반영합니다.
        foreach (var kv in _pending)
        {
            var id = kv.Key; var delta = kv.Value;
            if (!_defs.TryGetValue(id, out var def)) continue;
            var row = _db.LoadAchievementProgress(_profileId, id) ?? new AchievementProgress
            {
                ProfileId = _profileId,
                AchievementId = id,
                IsUnlocked = false,
                Progress = 0
            };
            row.Progress = Mathf.Max(0, row.Progress + delta);
            _db.UpsertAchievementProgress(row);
            if (row.Progress >= Mathf.Max(1, def.ProgressTarget) && !row.IsUnlocked)
            {
                UnlockInternal(id, def.PointsReward);
            }
        }
        _pending.Clear();
    }

    /// <summary>
    /// 마지막 Flush 이후 새로 해금된 업적 ID 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<string> GetNewlyUnlockedSinceLastFlush()
        => _newlyUnlocked.AsReadOnly();

    /// <summary>
    /// 로드된 모든 업적 정의를 반환합니다.
    /// </summary>
    public IReadOnlyList<AchievementDefinition> GetAllDefinitions()
        => new List<AchievementDefinition>(_defs.Values);

    /// <summary>
    /// 지정한 프로필의 업적 진행도를 즉시 조회합니다.
    /// </summary>
    public IReadOnlyDictionary<string, AchievementProgress> GetProgressSnapshot(string profileId)
    {
        var map = new Dictionary<string, AchievementProgress>(StringComparer.OrdinalIgnoreCase);
        var pid = string.IsNullOrEmpty(profileId) ? _profileId : profileId;
        foreach (var id in _defs.Keys)
        {
            var row = _db.LoadAchievementProgress(pid, id);
            if (row == null)
            {
                row = new AchievementProgress
                {
                    ProfileId = pid,
                    AchievementId = id,
                    IsUnlocked = false,
                    Progress = 0,
                    UnlockedAtUtc = null
                };
            }
            map[id] = row;
        }
        return map;
    }

    /// <summary>
    /// Resources에서 업적 정의를 로드하거나 기본 더미 데이터를 생성합니다.
    /// </summary>
    private static Dictionary<string, AchievementDefinition> LoadDefinitions()
    {
        var dict = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assets = Resources.LoadAll<AchievementDefinition>("Progression/Achievements");
            foreach (var a in assets)
            {
                if (a != null && !string.IsNullOrEmpty(a.Id)) dict[a.Id] = a;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AchievementService] SO load failed: {e.Message}");
        }

        if (dict.Count == 0)
        {
            var firstWin = ScriptableObject.CreateInstance<AchievementDefinition>();
            firstWin.Id = "ACH_FIRST_WIN";
            firstWin.DisplayName = "첫 승리!";
            firstWin.Description = "전투에서 한 번 승리";
            firstWin.PointsReward = 1;
            firstWin.ProgressTarget = 1;
            dict[firstWin.Id] = firstWin;
        }
        return dict;
    }

    // --- 서비스 내부에서 처리하는 메타 이벤트 핸들러 ---
    private void HandleFloorReached(MetaEvents.FloorReachedPayload payload)
    {
        // 런 ID가 비어 있으면 무시합니다.
        if (payload.RunId == null) return;

        // DB를 참고해 런 단위 카운터의 초기값을 보정합니다.
        if (!_singleRunFloorCount.ContainsKey(payload.RunId))
        {
            try
            {
                int seed = 0;
                var lr = _db.LoadCurrentRun(payload.RunId);
                if (lr != null && lr.Nodes != null)
                {
                    // 방문한 층 수의 고유 개수를 세어 횟수를 유추합니다.
                    var distinctFloors = new System.Collections.Generic.HashSet<int>();
                    foreach (var n in lr.Nodes)
                    {
                        if (n != null && n.Visited) distinctFloors.Add(n.Floor);
                    }
                    seed = Mathf.Max(0, distinctFloors.Count - 1);
                }
                _singleRunFloorCount[payload.RunId] = seed;
            }
            catch { _singleRunFloorCount[payload.RunId] = 0; }
        }

        // 1) 누적 이동 층수: FloorReached마다 +1을 합산하고 티어 업적을 검증합니다.
        try
        {
            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T1", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T1");

            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T2", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T2");

            ReportProgress("ACH_TRAVERSE_FLOORS_TOTAL_T3", 1);
            UnlockIfEligible("ACH_TRAVERSE_FLOORS_TOTAL_T3");
        }
        catch { }

        // 2) 런 단위 이동 층수: 런별 카운터로 임계치를 검사합니다.
        try
        {
            _singleRunFloorCount.TryGetValue(payload.RunId, out int cur);
            cur += 1; // 층을 이동할 때마다 +1
            _singleRunFloorCount[payload.RunId] = cur;

            // 정의된 목표치에 도달하면 직접 해금합니다.
            if (_defs.TryGetValue("ACH_TRAVERSE_FLOORS_SINGLE_RUN_T1", out var def1))
            {
                if (cur >= Math.Max(1, def1.ProgressTarget))
                {
                    UnlockDirect(def1.Id, def1.PointsReward);
                }
            }
            if (_defs.TryGetValue("ACH_TRAVERSE_FLOORS_SINGLE_RUN_T2", out var def2))
            {
                if (cur >= Math.Max(1, def2.ProgressTarget))
                {
                    UnlockDirect(def2.Id, def2.PointsReward);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 런 종료 시 런 단위 카운터를 초기화합니다.
    /// </summary>
    private void HandleRunEnded(MetaEvents.RunEndedPayload payload)
    {
        if (payload.RunId == null) return;
        _singleRunFloorCount.Remove(payload.RunId);
    }
}

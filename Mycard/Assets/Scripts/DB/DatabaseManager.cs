using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using UnityEngine; // Unity의 GameLog.Info, Application.persistentDataPath 등을 사용하기 위해 필요합니다.
using Game.Save;  // \SaveData.cs의 데이터 구조를 사용 선언

/// <summary>
/// 싱글턴, 데이터베이스와의 모든 통신을 책임지는 클래스입니다.
/// </summary>
public sealed class DatabaseManager
{
    // 게임 코드 어디서든 'DatabaseManager.Instance'로 이 관리자에게 쉽게 접근할 수 있습니다.
    public static DatabaseManager Instance { get; } = new DatabaseManager();
    // 생성자를 private으로 막아서, 다른 곳에서 실수로 또 만드는 것을 방지합니다.
    private DatabaseManager() { }

    private SQLiteConnection _conn; // 데이터베이스와의 연결 통로입니다.
    private string _dbPath;         // 세이브 파일(.db)의 전체 경로입니다.
    private string _bakPath;        // 백업 파일(.bak)의 전체 경로입니다.

    // ---------------------------
    // 1) 연결 및 스키마(테이블) 보장
    // ---------------------------

    /// <summary>
    /// 데이터베이스 파일에 연결하고, 모든 테이블이 존재하는지 확인 및 생성합니다.
    /// 게임 시작 시 단 한 번만 호출하면 됩니다.
    /// </summary>
    /// <param name="fileName">세이브 파일의 이름입니다.</param>
    public void Connect(string fileName = "game_save.db")
    {
        if (_conn != null) return; // ← 이미 연결되어 있으면 즉시 종료
        // Application.persistentDataPath는 PC, 모바일 등 어떤 환경에서도
        // 안전하게 파일을 저장할 수 있는 경로를 자동으로 찾아줍니다.
        var dir = Application.persistentDataPath;
        Directory.CreateDirectory(dir); // 폴더가 없으면 생성합니다.
        _dbPath = Path.Combine(dir, fileName);
        _bakPath = _dbPath + ".bak";

        // DB 파일에 연결을 시도하고, 파일이 없으면 새로 생성합니다.
        _conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

        // 데이터베이스의 안정성과 성능을 높여주는 전문적인 설정입니다.
        TryPragmaScalar("PRAGMA journal_mode=WAL;");      // 동시 읽기/쓰기 성능 향상
        TryPragmaScalar("PRAGMA synchronous=NORMAL;");    // 쓰기 속도 향상
        TryPragmaScalar("PRAGMA foreign_keys=ON;");       // 데이터 관계 무결성 보장

        // SaveData.cs에 정의된 모든 테이블이 DB에 존재하는지 확인하고, 없으면 생성합니다.
        EnsureSchema();
        GameLog.Info($"[DB] 데이터베이스 연결 성공: {_dbPath}");
    }

    private void TryPragmaScalar(string sql)
    {
        try
        {
            // PRAGMA는 보통 1행을 반환하므로 Scalar로 소모해 준다
            var _ = _conn.ExecuteScalar<string>(sql);
        }
        catch (SQLiteException e)
        {
            // 플랫폼/드라이버에 따라 미지원일 수 있으니 경고만 남기고 무시
            GameLog.Warn($"[DB] PRAGMA ignored ({sql}): {e.Message}");
        }
    }

    /// <summary>
    /// SaveData.cs에 정의된 모든 클래스를 기반으로 DB에 테이블을 생성합니다.
    /// 테이블이 이미 존재하면 자동으로 건너뜁니다.
    /// </summary>
    private void EnsureSchema()
    {
        // ==== 영구 저장용 테이블 생성 ====
        _conn.CreateTable<PlayerProfile>();
        _conn.CreateTable<PerkAllocation>();
        _conn.CreateTable<UnlockedCard>();
        _conn.CreateTable<UnlockedRelic>();
        _conn.CreateTable<UnlockedCompanion>();
        _conn.CreateTable<AchievementUnlocked>();
        _conn.CreateTable<AchievementProgress>();
        _conn.CreateTable<RunSummary>();

        // ==== '이어하기'용 테이블 생성 ====
        _conn.CreateTable<CurrentRun>();
        _conn.CreateTable<CardInDeck>();
        _conn.CreateTable<CardRuntimeState>();
        _conn.CreateTable<RelicInPossession>();
        _conn.CreateTable<PotionInPossession>();
        _conn.CreateTable<MapNodeState>();
        _conn.CreateTable<RngState>();
        _conn.CreateTable<ActiveShopSession>(); //db 상점
        _conn.CreateTable<ActiveEventSession>(); //db 이벤트
        _conn.CreateTable<RunPerkSnapshot>();
        _conn.CreateTable<RunStageState>();
        _conn.CreateTable<ActiveBattleState>();
        _conn.CreateTable<MapLayoutStorage>();
        _conn.CreateTable<TutorialProgress>();

        EnsureCurrentRunCompanionColumn();
        EnsureAchievementProgressTierColumn();
        EnsureCurrentRunTutorialColumn();

        // ==== CardRuntimeState 핵심 인덱스 생성 ====
        try
        {
            // 특정 런의 특정 더미를 Top 우선으로 빠르게 조회하기 위한 인덱스
            _conn.Execute(
                "CREATE INDEX IF NOT EXISTS IX_CardRuntimeState_Query " +
                "ON CardRuntimeState (RunId, Location, OrderInPile DESC)"
            );

            // 런 내 카드 타입 집계를 위한 보조 인덱스
            _conn.Execute(
                "CREATE INDEX IF NOT EXISTS IX_CardRuntimeState_Type_Count " +
                "ON CardRuntimeState (RunId, CardId)"
            );
        }
        catch (SQLiteException e)
        {
            GameLog.Warn($"[DB] CardRuntimeState 인덱스 생성 경고: {e.Message}");
        }

        // ==== 무결성/성능 인덱스 및 유니크 제약 ====
        try { _conn.Execute("CREATE UNIQUE INDEX IF NOT EXISTS UX_PerkAllocation ON PerkAllocation (ProfileId, PerkId)"); }
        catch (SQLiteException e) { GameLog.Warn($"[DB] UX_PerkAllocation 생성 경고: {e.Message}"); }

        try { _conn.Execute("CREATE UNIQUE INDEX IF NOT EXISTS UX_AchievementProgress ON AchievementProgress (ProfileId, AchievementId)"); }
        catch (SQLiteException e) { GameLog.Warn($"[DB] UX_AchievementProgress 생성 경고: {e.Message}"); }

        try { _conn.Execute("CREATE UNIQUE INDEX IF NOT EXISTS UX_RunPerkSnapshot ON RunPerkSnapshot (RunId, EffectKey)"); }
        catch (SQLiteException e) { GameLog.Warn($"[DB] UX_RunPerkSnapshot 생성 경고: {e.Message}"); }

        try { _conn.Execute("CREATE UNIQUE INDEX IF NOT EXISTS UX_TutorialProgress_ProfileTutorial ON TutorialProgress (ProfileId, TutorialId)"); }
        catch (SQLiteException e) { GameLog.Warn($"[DB] UX_TutorialProgress 생성 경고: {e.Message}"); }
    }

    private void EnsureCurrentRunCompanionColumn()
    {
        try
        {
            var columns = _conn.GetTableInfo("CurrentRun");
            bool hasColumn = columns.Any(c => string.Equals(c.Name, "CompanionId", StringComparison.OrdinalIgnoreCase));
            if (!hasColumn)
            {
                _conn.Execute("ALTER TABLE CurrentRun ADD COLUMN CompanionId TEXT NOT NULL DEFAULT '';");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info("[DB] Added CompanionId column to CurrentRun.");
#endif
            }

            _conn.Execute("CREATE INDEX IF NOT EXISTS IX_CurrentRun_Companion ON CurrentRun (CompanionId);");
        }
        catch (Exception e)
        {
            GameLog.Error($"[DB] EnsureCurrentRunCompanionColumn failed: {e.Message}");
        }
    }

    private void EnsureAchievementProgressTierColumn()
    {
        try
        {
            var columns = _conn.GetTableInfo("AchievementProgress");
            bool hasColumn = columns.Any(c => string.Equals(c.Name, "HighestTierUnlocked", StringComparison.OrdinalIgnoreCase));
            if (!hasColumn)
            {
                _conn.Execute("ALTER TABLE AchievementProgress ADD COLUMN HighestTierUnlocked INTEGER NOT NULL DEFAULT 0;");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info("[DB] Added HighestTierUnlocked column to AchievementProgress.");
#endif
            }
        }
        catch (Exception e)
        {
            GameLog.Error($"[DB] EnsureAchievementProgressTierColumn failed: {e.Message}");
        }
    }

    private void EnsureCurrentRunTutorialColumn()
    {
        try
        {
            var columns = _conn.GetTableInfo("CurrentRun");
            bool hasColumn = columns.Any(c => string.Equals(c.Name, "IsTutorialRun", StringComparison.OrdinalIgnoreCase));
            if (!hasColumn)
            {
                _conn.Execute("ALTER TABLE CurrentRun ADD COLUMN IsTutorialRun INTEGER NOT NULL DEFAULT 0;");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info("[DB] Added IsTutorialRun column to CurrentRun.");
#endif
            }
        }
        catch (Exception e)
        {
            GameLog.Error($"[DB] EnsureCurrentRunTutorialColumn failed: {e.Message}");
        }
    }

    /// <summary>
    /// 현재 DB 파일을 백업 파일(.bak)으로 복사하여 데이터 손상을 방지합니다.
    /// </summary>
    private void BackupDatabaseAtomic()
    {
        try
        {
            // 기존 .bak 있으면 지우고(중복 방지)
            if (File.Exists(_bakPath)) File.Delete(_bakPath);

            var quoted = _bakPath.Replace("'", "''");
            _conn.Execute($"VACUUM INTO '{quoted}';");   // 일관된 스냅샷 백업
            GameLog.Info($"[DB] 백업 완료(VACUUM INTO): {_bakPath}");
            return;
        }
        catch (Exception e)
        {
            GameLog.Warn($"[DB] VACUUM INTO 불가 → File.Copy 폴백: {e.Message}");
        }

        try
        {
            File.Copy(_dbPath, _bakPath, true);
            GameLog.Info($"[DB] 백업 완료(File.Copy): {_bakPath}");
        }
        catch (Exception e2)
        {
            GameLog.Warn($"[DB] 백업 실패(File.Copy): {e2.Message}");
        }
    }

    /// <summary>
    /// 여러 개의 DB 작업을 하나의 묶음(트랜잭션)으로 처리하여 안정성을 보장합니다.
    /// 작업 중간에 오류가 나면, 모든 작업이 없었던 일처럼 원상 복구됩니다.
    /// </summary>
    private void InTx(Action<SQLiteConnection> work)
    {
        _conn.BeginTransaction();
        try
        {
            work(_conn);
            _conn.Commit();
        }
        catch (Exception e)
        {
            _conn.Rollback();
            GameLog.Error($"[DB] 트랜잭션 실패: {e.Message}");
            throw; // 에러를 다시 던져서 호출한 쪽에서 알 수 있게 함
        }
    }

    // ==========================================================
    // 2) 프로필 (영구 저장) 관련 함수들
    // ==========================================================

    /// <summary>
    /// 플레이어 프로필을 저장합니다. 이미 존재하면 덮어쓰고, 없으면 새로 만듭니다. (Upsert)
    /// </summary>
    public void SaveProfile(PlayerProfile profile)
    {
        profile.UpdatedAtUtc = DateTime.UtcNow.ToString("o"); // 마지막 저장 시각 갱신
        InTx(conn => conn.InsertOrReplace(profile));
        BackupDatabaseAtomic();
    }

    /// <summary>
    /// 특정 ID의 플레이어 프로필을 불러옵니다.
    /// </summary>
    public PlayerProfile LoadProfile(string profileId)
    {
        return _conn.Find<PlayerProfile>(profileId);
    }

    /// <summary>
    /// 특정 프로필의 모든 특전 정보를 통째로 교체합니다. (기존 것 삭제 후 새로 삽입)
    /// </summary>
    public void SavePerkAllocations(string profileId, IEnumerable<PerkAllocation> perks)
    {
        InTx(conn =>
        {
            conn.Table<PerkAllocation>().Delete(p => p.ProfileId == profileId);
            conn.InsertAll(perks);
        });
        BackupDatabaseAtomic();
    }

    /// <summary>
    /// 특전 배분과 포인트 변화를 하나의 트랜잭션으로 원자적 적용합니다.
    /// pointsDelta는 음수면 차감, 양수면 환급입니다.
    /// </summary>
    public void ApplyPerkAdjustments(string profileId, System.Collections.Generic.IEnumerable<PerkAllocation> perks, int pointsDelta)
    {
        if (string.IsNullOrEmpty(profileId)) return;
        var list = perks?.ToList() ?? new System.Collections.Generic.List<PerkAllocation>();
        InTx(conn =>
        {
            var profile = conn.Find<PlayerProfile>(profileId);
            if (profile == null)
            {
                profile = new PlayerProfile
                {
                    ProfileId = profileId,
                    SchemaVersion = 1,
                    CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
                    AppVersion = Application.version,
                    UnspentPerkPoints = 0
                };
            }
            int newPoints = profile.UnspentPerkPoints + pointsDelta;
            if (newPoints < 0)
                throw new System.InvalidOperationException("ApplyPerkAdjustments would result in negative perk points.");

            profile.UnspentPerkPoints = newPoints;
            profile.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
            conn.InsertOrReplace(profile);

            conn.Table<PerkAllocation>().Delete(p => p.ProfileId == profileId);
            if (list.Count > 0)
            {
                foreach (var p in list)
                {
                    if (p == null) continue;
                    p.ProfileId = profileId;
                }
                conn.InsertAll(list);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var count = conn.Table<PerkAllocation>().Count(p => p.ProfileId == profileId);
            GameLog.Info($"[DB] ApplyPerkAdjustments committed: profile={profileId}, pointsDelta={pointsDelta}, allocCount={count}");
#endif
        });
        BackupDatabaseAtomic();
    }

    public System.Collections.Generic.List<PerkAllocation> LoadPerkAllocations(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return new System.Collections.Generic.List<PerkAllocation>();
        return _conn.Table<PerkAllocation>().Where(p => p.ProfileId == profileId).ToList();
    }

    // --- 런 메타(현재 위치/기본 정보) 업데이트 ---
    public void UpsertCurrentRun(CurrentRun run)
    {
        if (run == null || string.IsNullOrEmpty(run.RunId))
            throw new ArgumentException("UpsertCurrentRun: invalid run");
        run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        InTx(conn => conn.InsertOrReplace(run));
    }

    public void UpdateRunPosition(string runId, int act, int floor, int nodeIndex)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var run = _conn.Find<CurrentRun>(runId);
        if (run == null) return;
        run.Act = act;
        run.Floor = floor;
        run.NodeIndex = nodeIndex;
        run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        _conn.Update(run);
    }

    // ... (AddUnlockedCard, AddUnlockedRelic 등 다른 영구 데이터 저장 함수들) ...

    // ==========================================================
    // 3) 현재 런 (일시 저장) 관련 함수들
    // ==========================================================

    // SaveCurrentRun legacy API는 제거되었고, 세분화된 API로 대체되었습니다.

    /// <summary>
    /// 저장된 '이어하기' 데이터를 불러옵니다.
    /// </summary>
    public RunLoadResult LoadCurrentRun(string runId)
    {
        var run = _conn.Find<CurrentRun>(runId);
        if (run == null) return null; // 저장된 런이 없으면 null 반환

        // RunId에 해당하는 모든 관련 데이터를 각 테이블에서 불러옵니다.
        return new RunLoadResult
        {
            Run = run,
            Cards = _conn.Table<CardInDeck>().Where(x => x.RunId == runId).ToList(),
            Relics = _conn.Table<RelicInPossession>().Where(x => x.RunId == runId).ToList(),
            Potions = _conn.Table<PotionInPossession>().Where(x => x.RunId == runId).ToList(),
            Nodes = _conn.Table<MapNodeState>().Where(x => x.RunId == runId).ToList(),
            RngStates = _conn.Table<RngState>().Where(x => x.RunId == runId).ToList(),
            Stage = _conn.Find<RunStageState>(runId),
            BattleState = _conn.Find<ActiveBattleState>(runId)
        };
    }

    /// <summary>
    /// '이어하기' 데이터를 삭제합니다. (예: 런 포기, 런 완료)
    /// </summary>
    public void DeleteCurrentRun(string runId)
    {
        InTx(conn =>
        {
            DeleteCurrentRun_NoTx(conn, runId);
        });

        BackupDatabaseAtomic();
        GameLog.Info($"[DB] 현재 런 삭제 완료: {runId}");
    }

    // 같은 트랜잭션 내에서 합성 호출을 가능하게 하기 위한 내부 헬퍼(별도 Begin/Commit 없음)
    private static void DeleteCurrentRun_NoTx(SQLiteConnection conn, string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        conn.Table<CardInDeck>().Delete(x => x.RunId == runId);
        conn.Table<RelicInPossession>().Delete(x => x.RunId == runId);
        conn.Table<PotionInPossession>().Delete(x => x.RunId == runId);
        conn.Table<MapNodeState>().Delete(x => x.RunId == runId);
        conn.Table<RngState>().Delete(x => x.RunId == runId);
        conn.Table<RunPerkSnapshot>().Delete(x => x.RunId == runId);
        conn.Table<MapLayoutStorage>().Delete(x => x.RunId == runId);
        conn.Table<ActiveBattleState>().Delete(x => x.RunId == runId);
        conn.Table<RunStageState>().Delete(x => x.RunId == runId);
        conn.Delete<ActiveEventSession>(runId);
        conn.Delete<ActiveShopSession>(runId);
        conn.Table<CurrentRun>().Delete(x => x.RunId == runId);
    }

    private static void ReplaceCardsInDeck_NoTx(SQLiteConnection conn, string runId, System.Collections.Generic.List<CardInDeck> cards)
    {
        conn.Table<CardInDeck>().Delete(x => x.RunId == runId);
        if (cards.Count > 0) conn.InsertAll(cards);
    }

    private static void ReplaceRelics_NoTx(SQLiteConnection conn, string runId, System.Collections.Generic.List<RelicInPossession> relics)
    {
        conn.Table<RelicInPossession>().Delete(x => x.RunId == runId);
        if (relics.Count > 0) conn.InsertAll(relics);
    }

    private static void ReplacePotions_NoTx(SQLiteConnection conn, string runId, System.Collections.Generic.List<PotionInPossession> potions)
    {
        conn.Table<PotionInPossession>().Delete(x => x.RunId == runId);
        if (potions.Count > 0) conn.InsertAll(potions);
    }

    /// <summary>
    /// (편의 기능) 런 요약 정보를 저장하고, 동시에 '이어하기' 데이터를 삭제합니다.
    /// </summary>
    public void EndRunAndSummarize(RunSummary summary)
    {
        InTx(conn =>
        {
            conn.Insert(summary);
            // 요약에 사용된 RunId를 기준으로 '이어하기' 데이터를 삭제합니다. (동일 트랜잭션 내에서 처리)
            DeleteCurrentRun_NoTx(conn, summary.RunId);
        });
        GameLog.Info($"[DB] 런 종료 및 요약 저장 완료: {summary.RunId}");
    }

    

    // ==========================================================
    // 4) 부분 업데이트 (안전한 저장)
    // ==========================================================

    /// <summary>
    /// 특정 런(Run)의 골드만 안전하게 업데이트합니다.
    /// DB에서 최신 데이터를 읽어와 골드만 수정 후 저장하므로 다른 값을 덮어쓸 위험이 없습니다.
    /// </summary>
    public void UpdateRunGold(string runId, int newGold)
    {
        var run = _conn.Find<CurrentRun>(runId);
        if (run == null) return;

        run.Gold = Mathf.Max(0, newGold);
        run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");

        _conn.Update(run);
        GameLog.Info($"[DB] 골드 업데이트 완료: {run.Gold}");
    }

    /// <summary>
    /// 상점 세션 정보만 안전하게 추가하거나 갱신(Upsert)합니다.
    /// 기존 노드 정보를 보존하며 상점 데이터만 덮어씁니다.
    /// </summary>
    /*
    public void UpsertShopSession(string runId, int act, int floor, int index, string shopJson)
    {
        var existing = _conn.Table<MapNodeState>().FirstOrDefault(n =>
            n.RunId == runId && n.Act == act && n.Floor == floor && n.NodeIndex == index);

        if (existing == null)
        {
            _conn.Insert(new MapNodeState {
                RunId = runId,
                Act = act,
                Floor = floor,
                NodeIndex = index,
                Type = Game.Save.NodeType.Shop,
                Visited = true, // 최초 저장 시 방문 처리
                ShopInventoryJson = shopJson
            });
        }
        else
        {
            existing.ShopInventoryJson = shopJson;
            _conn.Update(existing);
        }
        GameLog.Info($"[DB] 상점 세션 저장 완료: ({floor}, {index})");
    }
    */

    // --- 활성 상점 세션: RunId 1-row 저장소 ---
    public void UpsertActiveShopSession(string runId, string json, int floor, int index)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var row = new ActiveShopSession {
            RunId = runId,
            Json = json ?? "",
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            Floor = floor,
            Index = index
        };
        _conn.InsertOrReplace(row);
    }

    public ActiveShopSession LoadActiveShopSession(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        return _conn.Find<ActiveShopSession>(runId);
    }

    public void DeleteActiveShopSession(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        _conn.Table<ActiveShopSession>().Delete(x => x.RunId == runId);
    }

    // 1. HP 업데이트 함수 (품질 보강)
    public void UpdateRunHp(string runId, int newHp)
    {
        var run = _conn.Find<CurrentRun>(runId);
        if (run == null) return;

        // 경계값 보정: 체력이 0 미만 또는 최대 체력을 초과하지 않도록 안전장치 추가
        int maxHp = run.MaxHpBase + run.MaxHpFromPerks + run.MaxHpFromRelics;
        run.CurrentHp = Mathf.Clamp(newHp, 0, Mathf.Max(1, maxHp));

        run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        _conn.Update(run);
    }

    // 1.5. 최대 체력/현재 체력 동시 업데이트 함수
    public void UpdateRunMaxHp(string runId, int newMaxHpBase, int newCurrentHp)
    {
        if (string.IsNullOrEmpty(runId)) return;

        var run = _conn.Find<CurrentRun>(runId);
        if (run == null) return;

        run.MaxHpBase = Mathf.Max(1, newMaxHpBase);
        int maxHp = run.MaxHpBase + run.MaxHpFromPerks + run.MaxHpFromRelics;

        if (newCurrentHp >= 0)
        {
            run.CurrentHp = Mathf.Clamp(newCurrentHp, 0, Mathf.Max(1, maxHp));
        }
        else
        {
            run.CurrentHp = Mathf.Clamp(run.CurrentHp, 0, Mathf.Max(1, maxHp));
        }

        run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        _conn.Update(run);
    }

    public void ApplyRunRelicHpDelta(string runId, int delta, bool adjustCurrentHp)
    {
        if (string.IsNullOrEmpty(runId) || delta == 0)
            return;

        var run = _conn.Find<CurrentRun>(runId);
        if (run == null)
            return;

        int newRelicMax = Mathf.Max(0, run.MaxHpFromRelics + delta);
        run.MaxHpFromRelics = newRelicMax;

        int maxHp = Mathf.Max(1, run.MaxHpBase + run.MaxHpFromPerks + newRelicMax);
        if (adjustCurrentHp)
        {
            run.CurrentHp = Mathf.Clamp(run.CurrentHp + delta, 0, maxHp);
        }
        else
        {
            run.CurrentHp = Mathf.Clamp(run.CurrentHp, 0, maxHp);
        }

        run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        _conn.Update(run);
    }
    public void ApplyRunRelicEnergyDelta(string runId, int delta)
    {
        if (string.IsNullOrEmpty(runId) || delta == 0)
            return;

        var run = _conn.Find<CurrentRun>(runId);
        if (run == null)
            return;

        int newEnergy = Mathf.Max(0, run.EnergyMax + delta);
        run.EnergyMax = newEnergy;
        run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        _conn.Update(run);
    }
    // 2. 이벤트 세션 JSON 로드 함수 (신규 추가)
    public string LoadActiveEventSessionJson(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        var row = _conn.Find<ActiveEventSession>(runId);
        return row?.Json;
    }

    // 3. 노드 상태 업데이트/삽입 함수 (신규 추가)
    public void UpsertNodeState(MapNodeState node)
    {
        if (node == null || string.IsNullOrEmpty(node.RunId)) return;

        // 복합키처럼 작동하도록, 기존 데이터가 있는지 먼저 확인
        var existing = _conn.Table<MapNodeState>().FirstOrDefault(n =>
            n.RunId == node.RunId &&
            n.Act == node.Act &&
            n.Floor == node.Floor &&
            n.NodeIndex == node.NodeIndex);

        if (existing != null)
            node.Id = existing.Id; // 기존 데이터가 있으면 PK를 유지하여 덮어쓰기(Update)가 되도록 함

        _conn.InsertOrReplace(node);
    }

    public MapLayoutStorage LoadMapLayout(string runId, int act)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        return _conn.Table<MapLayoutStorage>().FirstOrDefault(x => x.RunId == runId && x.Act == act);
    }

    public void UpsertMapLayout(string runId, int act, string json)
    {
        if (string.IsNullOrEmpty(runId)) return;

        var existing = _conn.Table<MapLayoutStorage>().FirstOrDefault(x => x.RunId == runId && x.Act == act);
        if (existing != null)
        {
            existing.Json = json ?? string.Empty;
            existing.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            _conn.Update(existing);
            return;
        }

        var row = new MapLayoutStorage
        {
            RunId = runId,
            Act = act,
            Json = json ?? string.Empty,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o")
        };
        _conn.Insert(row);
    }

    public void DeleteMapLayout(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        _conn.Table<MapLayoutStorage>().Delete(x => x.RunId == runId);
    }

    // --- 레거시 덱(CardInDeck)/유물/포션 교체 저장 (런 생성 시에만 사용) ---
    public void ReplaceCardsInDeck(string runId, System.Collections.Generic.IEnumerable<CardInDeck> cards)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var list = cards?.ToList() ?? new System.Collections.Generic.List<CardInDeck>();
        InTx(conn => ReplaceCardsInDeck_NoTx(conn, runId, list));
    }

    public void ReplaceRelics(string runId, System.Collections.Generic.IEnumerable<RelicInPossession> relics)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var list = relics?.ToList() ?? new System.Collections.Generic.List<RelicInPossession>();
        InTx(conn => ReplaceRelics_NoTx(conn, runId, list));
    }

    public void ReplacePotions(string runId, System.Collections.Generic.IEnumerable<PotionInPossession> potions)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var list = potions?.ToList() ?? new System.Collections.Generic.List<PotionInPossession>();
        InTx(conn => ReplacePotions_NoTx(conn, runId, list));
    }

    public void CreateNewRunSnapshot(CurrentRun run, System.Collections.Generic.IEnumerable<CardInDeck> cards, System.Collections.Generic.IEnumerable<RelicInPossession> relics, System.Collections.Generic.IEnumerable<PotionInPossession> potions)
    {
        if (run == null || string.IsNullOrEmpty(run.RunId))
            throw new ArgumentException("CreateNewRunSnapshot: invalid run");

        var cardList = cards?.ToList() ?? new System.Collections.Generic.List<CardInDeck>();
        var relicList = relics?.ToList() ?? new System.Collections.Generic.List<RelicInPossession>();
        var potionList = potions?.ToList() ?? new System.Collections.Generic.List<PotionInPossession>();

        InTx(conn =>
        {
            if (string.IsNullOrEmpty(run.CreatedAtUtc))
            {
                run.CreatedAtUtc = DateTime.UtcNow.ToString("o");
            }
            run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            conn.InsertOrReplace(run);

            ReplaceCardsInDeck_NoTx(conn, run.RunId, cardList);
            ReplaceRelics_NoTx(conn, run.RunId, relicList);
            ReplacePotions_NoTx(conn, run.RunId, potionList);
        });
    }

    // --- 활성 이벤트 세션: RunId 1-row 저장소 ---
    public void UpsertActiveEventSession(string runId, string json)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var row = new ActiveEventSession {
            RunId = runId,
            Json = json ?? "",
            UpdatedAtUtc = System.DateTime.UtcNow.ToString("o")
        };
        _conn.InsertOrReplace(row);
    }

    public ActiveEventSession LoadActiveEventSession(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        return _conn.Find<ActiveEventSession>(runId);
    }

    public void DeleteActiveEventSession(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        _conn.Table<ActiveEventSession>().Delete(x => x.RunId == runId);
    }

    public RunStageState LoadRunStageState(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        return _conn.Find<RunStageState>(runId);
    }

    public void UpsertRunStageState(RunStageState state)
    {
        if (state == null || string.IsNullOrEmpty(state.RunId)) return;
        state.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        _conn.InsertOrReplace(state);
    }

    public void DeleteRunStageState(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        _conn.Table<RunStageState>().Delete(x => x.RunId == runId);
    }

    public ActiveBattleState LoadActiveBattleState(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        return _conn.Find<ActiveBattleState>(runId);
    }

    public void UpsertActiveBattleState(string runId, string json)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var row = new ActiveBattleState
        {
            RunId = runId,
            Json = json ?? string.Empty,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o")
        };
        _conn.InsertOrReplace(row);
    }

    public void DeleteActiveBattleState(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return;
        _conn.Table<ActiveBattleState>().Delete(x => x.RunId == runId);
    }

    public TutorialProgress LoadTutorialProgress(string profileId, string tutorialId)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(tutorialId)) return null;
        return _conn.Table<TutorialProgress>()
            .FirstOrDefault(x => x.ProfileId == profileId && x.TutorialId == tutorialId);
    }

    public void UpsertTutorialProgress(TutorialProgress row)
    {
        if (row == null) return;
        row.ProfileId ??= "P1";
        row.TutorialId ??= TutorialIds.CoreOnboarding;
        row.UpdatedAtUtc = DateTime.UtcNow.ToString("o");

        var existing = _conn.Table<TutorialProgress>()
            .FirstOrDefault(x => x.ProfileId == row.ProfileId && x.TutorialId == row.TutorialId);

        if (existing != null)
        {
            row.Id = existing.Id;
            _conn.Update(row);
        }
        else
        {
            _conn.Insert(row);
        }
    }

    public void DeleteTutorialProgress(string profileId, string tutorialId)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(tutorialId)) return;
        _conn.Table<TutorialProgress>().Delete(x => x.ProfileId == profileId && x.TutorialId == tutorialId);
    }

    // --- RNG 상태 저장/로드 ---
    public System.Collections.Generic.List<RngState> LoadRngStates(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return new System.Collections.Generic.List<RngState>();
        return _conn.Table<RngState>().Where(r => r.RunId == runId).ToList();
    }

    public void UpsertRngStates(string runId, System.Collections.Generic.IEnumerable<RngState> states)
    {
        if (string.IsNullOrEmpty(runId)) return;

        InTx(conn =>
        {
            conn.Table<RngState>().Delete(r => r.RunId == runId);
            if (states != null)
            {
                foreach (var s in states)
                {
                    if (s == null) continue;
                    s.RunId = runId; // 상위에서 비워져 내려오는 경우 보정
                    conn.Insert(s);
                }
            }
        });
    }
    
    // ==== CardRuntimeState 관리 API ====
    public void UpsertCardRuntimeStates(string runId, System.Collections.Generic.IEnumerable<CardRuntimeState> cards)
    {
        if (string.IsNullOrEmpty(runId)) return;

        var list = cards as System.Collections.Generic.IList<CardRuntimeState> ?? cards?.ToList();
        if (list == null)
        {
            // null 입력은 실수로 간주하고 아무 작업도 하지 않음(보호)
            return;
        }

        InTx(conn =>
        {
            // 기존 상태 전부 삭제(스냅샷 교체)
            conn.Table<CardRuntimeState>().Delete(c => c.RunId == runId);

            if (list.Count == 0)
            {
                // 의도된 전체 삭제
                return;
            }

            foreach (var card in list)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (card == null)
                    throw new System.InvalidOperationException("UpsertCardRuntimeStates: null card detected in input list.");
                if (string.IsNullOrEmpty(card.InstanceId))
                    throw new System.InvalidOperationException("UpsertCardRuntimeStates: InstanceId is empty.");
                if (string.IsNullOrEmpty(card.CardId))
                    throw new System.InvalidOperationException($"UpsertCardRuntimeStates: CardId is empty for {card.InstanceId}.");
#endif
                card.RunId = runId;
                card.ModifiersJson = card.ModifiersJson ?? string.Empty;
            }

            conn.InsertAll(list);
        });
    }

    public System.Collections.Generic.List<CardRuntimeState> LoadCardRuntimeStates(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return new System.Collections.Generic.List<CardRuntimeState>();
        return _conn.Table<CardRuntimeState>()
            .Where(c => c.RunId == runId)
            .OrderByDescending(c => c.OrderInPile)
            .ToList();
    }

    public void DeleteCardRuntimeState(string runId, string instanceId)
    {
        if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(instanceId))
            return;

        InTx(conn =>
        {
            conn.Table<CardRuntimeState>().Delete(c => c.RunId == runId && c.InstanceId == instanceId);
            conn.Table<CardInDeck>().Delete(c => c.RunId == runId && c.InstanceId == instanceId);
        });
    }

    public System.Collections.Generic.List<CardRuntimeState> LoadCardRuntimeStates(string runId, CardLocation location)
    {
        if (string.IsNullOrEmpty(runId)) return new System.Collections.Generic.List<CardRuntimeState>();
        return _conn.Table<CardRuntimeState>()
            .Where(c => c.RunId == runId && c.Location == location)
            .OrderByDescending(c => c.OrderInPile)
            .ToList();
    }


    public void Close()
    {
        try { _conn?.Close(); } catch { }
        _conn = null;
    }  
    
    // ==========================================================
    // 5) v3.0: Perk Snapshot & Achievement Progress helpers
    // ==========================================================

    public void ReplaceRunPerkSnapshot(string runId, System.Collections.Generic.IEnumerable<RunPerkSnapshot> rows)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var list = rows?.ToList() ?? new System.Collections.Generic.List<RunPerkSnapshot>();
        InTx(conn =>
        {
            conn.Table<RunPerkSnapshot>().Delete(x => x.RunId == runId);
            if (list.Count > 0)
            {
                foreach (var r in list)
                {
                    if (r == null) continue;
                    r.RunId = runId;
                    r.EffectKey = r.EffectKey ?? string.Empty;
                    conn.Insert(r);
                }
            }
        });
    }

    public System.Collections.Generic.List<RunPerkSnapshot> LoadRunPerkSnapshot(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return new System.Collections.Generic.List<RunPerkSnapshot>();
        return _conn.Table<RunPerkSnapshot>().Where(x => x.RunId == runId).ToList();
    }

    public AchievementProgress LoadAchievementProgress(string profileId, string achievementId)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(achievementId)) return null;
        return _conn.Table<AchievementProgress>()
            .FirstOrDefault(x => x.ProfileId == profileId && x.AchievementId == achievementId);
    }

    public void UpsertAchievementProgress(AchievementProgress row)
    {
        if (row == null || string.IsNullOrEmpty(row.ProfileId) || string.IsNullOrEmpty(row.AchievementId)) return;
        InTx(conn =>
        {
            var existing = conn.Table<AchievementProgress>()
                .FirstOrDefault(x => x.ProfileId == row.ProfileId && x.AchievementId == row.AchievementId);
            if (existing == null)
            {
                conn.Insert(row);
            }
            else
            {
                existing.IsUnlocked = row.IsUnlocked;
                existing.Progress = row.Progress;
                existing.UnlockedAtUtc = row.UnlockedAtUtc;
                existing.HighestTierUnlocked = row.HighestTierUnlocked;
                conn.Update(existing);
            }
        });
    }

    public void AddPerkPoints(string profileId, int delta)
    {
        if (string.IsNullOrEmpty(profileId) || delta == 0) return;
        var profile = _conn.Find<PlayerProfile>(profileId);
        if (profile == null)
        {
            profile = new PlayerProfile
            {
                ProfileId = profileId,
                SchemaVersion = 1,
                CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
                AppVersion = Application.version,
                UnspentPerkPoints = 0
            };
        }
        profile.UnspentPerkPoints = Mathf.Max(0, profile.UnspentPerkPoints + delta);
        profile.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        _conn.InsertOrReplace(profile);
    }


}

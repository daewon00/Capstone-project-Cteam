using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using UnityEngine; // Unity의 Debug.Log, Application.persistentDataPath 등을 사용하기 위해 필요합니다.
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
        Debug.Log($"[DB] 데이터베이스 연결 성공: {_dbPath}");
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
            Debug.LogWarning($"[DB] PRAGMA ignored ({sql}): {e.Message}");
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
        _conn.CreateTable<RunSummary>();

        // ==== '이어하기'용 테이블 생성 ====
        _conn.CreateTable<CurrentRun>();
        _conn.CreateTable<CardInDeck>();
        _conn.CreateTable<RelicInPossession>();
        _conn.CreateTable<PotionInPossession>();
        _conn.CreateTable<MapNodeState>();
        _conn.CreateTable<RngState>();
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
            Debug.Log($"[DB] 백업 완료(VACUUM INTO): {_bakPath}");
            return;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DB] VACUUM INTO 불가 → File.Copy 폴백: {e.Message}");
        }

        try
        {
            File.Copy(_dbPath, _bakPath, true);
            Debug.Log($"[DB] 백업 완료(File.Copy): {_bakPath}");
        }
        catch (Exception e2)
        {
            Debug.LogWarning($"[DB] 백업 실패(File.Copy): {e2.Message}");
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
            Debug.LogError($"[DB] 트랜잭션 실패: {e.Message}");
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

    // ... (AddUnlockedCard, AddUnlockedRelic 등 다른 영구 데이터 저장 함수들) ...

    // ==========================================================
    // 3) 현재 런 (일시 저장) 관련 함수들
    // ==========================================================

    /// <summary>
    /// '이어하기'를 위해 현재 게임의 모든 상태를 저장합니다.
    /// </summary>
    public void SaveCurrentRun(
        CurrentRun run,
        IList<CardInDeck> cards,
        IList<RelicInPossession> relics,
        IList<PotionInPossession> potions,
        IList<MapNodeState> nodes,
        IList<RngState> rngStates)
    {
        run.UpdatedAtUtc = DateTime.UtcNow.ToString("o"); // 마지막 저장 시각 갱신

        InTx(conn =>
        {
            // 덮어쓰기를 위해, 이 RunId에 해당하는 기존 데이터를 모두 깨끗이 지웁니다.
            conn.Table<CardInDeck>().Delete(x => x.RunId == run.RunId);
            conn.Table<RelicInPossession>().Delete(x => x.RunId == run.RunId);
            conn.Table<PotionInPossession>().Delete(x => x.RunId == run.RunId);
            conn.Table<MapNodeState>().Delete(x => x.RunId == run.RunId);
            conn.Table<RngState>().Delete(x => x.RunId == run.RunId);

            // 새로운 데이터들을 삽입합니다.
            conn.InsertOrReplace(run);
            if (cards != null) conn.InsertAll(cards);
            if (relics != null) conn.InsertAll(relics);
            if (potions != null) conn.InsertAll(potions);
            if (nodes != null) conn.InsertAll(nodes);
            if (rngStates != null) conn.InsertAll(rngStates);
        });

        BackupDatabaseAtomic();
        Debug.Log($"[DB] 현재 런 저장 완료: {run.RunId}");
    }

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
            RngStates = _conn.Table<RngState>().Where(x => x.RunId == runId).ToList()
        };
    }

    /// <summary>
    /// '이어하기' 데이터를 삭제합니다. (예: 런 포기, 런 완료)
    /// </summary>
    public void DeleteCurrentRun(string runId)
    {
        InTx(conn =>
        {
            conn.Table<CardInDeck>().Delete(x => x.RunId == runId);
            conn.Table<RelicInPossession>().Delete(x => x.RunId == runId);
            conn.Table<PotionInPossession>().Delete(x => x.RunId == runId);
            conn.Table<MapNodeState>().Delete(x => x.RunId == runId);
            conn.Table<RngState>().Delete(x => x.RunId == runId);
            conn.Table<CurrentRun>().Delete(x => x.RunId == runId);
        });

        BackupDatabaseAtomic();
        Debug.Log($"[DB] 현재 런 삭제 완료: {runId}");
    }

    /// <summary>
    /// (편의 기능) 런 요약 정보를 저장하고, 동시에 '이어하기' 데이터를 삭제합니다.
    /// </summary>
    public void EndRunAndSummarize(RunSummary summary)
    {
        InTx(conn =>
        {
            conn.Insert(summary);
            // 요약에 사용된 RunId를 기준으로 '이어하기' 데이터를 삭제합니다.
            DeleteCurrentRun(summary.RunId);
        });
        Debug.Log($"[DB] 런 종료 및 요약 저장 완료: {summary.RunId}");
    }

    // ... (간단한 조회 헬퍼 함수들) ...

    /// <summary>
    /// LoadCurrentRun의 결과를 하나의 묶음(객체)으로 전달하기 위한 클래스입니다.
    /// </summary>
    public sealed class RunLoadResult
    {
        public CurrentRun Run;
        public List<CardInDeck> Cards;
        public List<RelicInPossession> Relics;
        public List<PotionInPossession> Potions;
        public List<MapNodeState> Nodes;
        public List<RngState> RngStates;
    }

    public void Close()
    {
        try { _conn?.Close(); } catch { }
        _conn = null;
    }  
    
}

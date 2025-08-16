using UnityEngine;
using SQLite; 
using System.IO;

/// <summary>
/// SQLite 데이터베이스와의 모든 통신을 책임지는 관리자 클래스입니다.
/// 싱글턴 패턴을 사용하여, 게임 내에서 단 하나만 존재하도록 보장합니다.
/// </summary>
public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager instance;

    private SQLiteConnection _connection;

    // 나중에 여기에 저장할 데이터의 '설계도' 클래스들을 추가하게 됩니다.
    // 예: public class PlayerProfile { ... }
    // 예: public class CurrentDeck { ... }


    private void Awake()
    {
        // 싱글턴 패턴 구현
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않도록 설정
            ConnectToDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 데이터베이스 파일에 연결하고, 필요한 테이블이 없으면 생성합니다.
    /// </summary>
    private void ConnectToDatabase()
    {
        // DB 파일 경로를 안전하게 설정합니다. (PC, 모바일 등 모든 플랫폼에서 작동)
        string path = Path.Combine(Application.persistentDataPath, "MyGameData.db");
        _connection = new SQLiteConnection(path);
        Debug.Log($"데이터베이스 경로: {path}");

        // 예시: 플레이어 프로필 테이블 생성 (테이블이 이미 존재하면 자동으로 건너뜀)
        // _connection.CreateTable<PlayerProfile>();
    }

    // 여기에 앞으로 Save, Load 함수들을 추가하게 됩니다.
    // public void SavePlayerProfile(PlayerProfile profile) { ... }
    // public PlayerProfile LoadPlayerProfile() { ... }


    // 게임이 완전히 종료될 때, DB 연결을 안전하게 닫아줍니다.
    private void OnApplicationQuit()
    {
        if (_connection != null)
        {
            _connection.Close();
        }
    }
}

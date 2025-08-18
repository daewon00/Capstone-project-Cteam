using UnityEngine;

/// <summary>
/// 게임이 시작될 때, DatabaseManager와 같은 핵심 시스템을 깨우고 초기화하는 역할을 합니다.
/// </summary>
public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        // 게임이 시작되면, DatabaseManager 프로그램을 깨워서
        // 데이터베이스에 연결하도록 시동을 겁니다.
        DatabaseManager.Instance.Connect();

        Debug.Log("GameInitializer: 모든 핵심 시스템 초기화 완료.");
    }
}

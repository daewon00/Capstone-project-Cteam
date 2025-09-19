using UnityEngine;
using System;
using UnityEngine.PlayerLoop;

/// <summary>
/// 현재 프로필과 런 ID, 전투 종류 등 세션 전역 상태를 보관하는 런타임 컨텍스트입니다.
/// </summary>
public class GameContext : MonoBehaviour
{
    public static GameContext I { get; private set; } //I는 Instance의 I?

    [Header("Session")]
    public string ProfileId = "P1"; // 임시 기본값
    public string RunId;
    public string SelectedCompanionId; // "WARRIOR" 처럼 저장

    // 전투 종류 태깅: 동일 전투 씬 재사용을 위한 꼬리표
    public enum BattleKind { Normal, Elite, Boss }
    public BattleKind CurrentBattleKind = BattleKind.Normal;

    //public DeckController DeckController { get; private set; }
    
    //게임매니저 같은걸 만드신거라 생각하고 약간 수정해봤습니다 내용은 유사하고 주석 처리 해놓은 것은 GameContext인 게임매니저가 실행될때 덱 컨트롤러를 자동으로 불러오는 코드를 만들었습니다
    /// <summary>
    /// 단일 인스턴스를 유지하고 씬 전환 간 파괴되지 않도록 설정합니다.
    /// </summary>
    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

    }

}

using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 동료 선택 화면에서 음악과 카드 선택 입력을 관리하는 UI 컨트롤러입니다.
/// </summary>
public class CompanionUI : MonoBehaviour
{
    public static CompanionUI instance;
    private bool hasPressed = false;
    /// <summary>
    /// 싱글턴 인스턴스를 등록합니다.
    /// </summary>
    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 화면 진입 시 배경 음악을 전환합니다.
    /// </summary>
    void Start()
    {
        AudioManager.instance.StopMusic();
        AudioManager.instance.PlayBattleSelectMusic();
    }

    /// <summary>
    /// 현재는 프레임별 처리가 필요 없어 비워 둡니다.
    /// </summary>
    void Update()
    {
        
    }

    /// <summary>
    /// legacy 버튼 클릭 경로로 호출되며 새 덱 생성 흐름 사용을 안내합니다.
    /// </summary>
    public void CompanionCardAdd1()
    {
        if (hasPressed) return;
        hasPressed = true;
        // 레거시 DeckController 경로 제거: 덱 추가는 IDeckService/DB를 통해 처리해야 합니다.
        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService == null)
        {
            GameLog.Warn("[CompanionUI] IDeckService를 찾지 못했습니다. 덱 추가는 CompanionSelectController/IDatabase 경로를 사용하세요.");
            return;
        }
        GameLog.Info("[CompanionUI] 카드 추가는 신규 경로로 통합되어야 합니다. (예: 보상/상점/동료 선택 로직에서 DB/IDeckService 사용)");
    }
}

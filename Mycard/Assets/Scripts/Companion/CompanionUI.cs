using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class CompanionUI : MonoBehaviour
{
    public static CompanionUI instance;
    private bool hasPressed = false;
    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        AudioManager.instance.StopMusic();
        AudioManager.instance.PlayBattleSelectMusic();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CompanionCardAdd1()
    {
        if (hasPressed) return;
        hasPressed = true;
        // 레거시 DeckController 경로 제거: 덱 추가는 IDeckService/DB를 통해 처리해야 합니다.
        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService == null)
        {
            Debug.LogWarning("[CompanionUI] IDeckService를 찾지 못했습니다. 덱 추가는 CompanionSelectController/IDatabase 경로를 사용하세요.");
            return;
        }
        Debug.Log("[CompanionUI] 카드 추가는 신규 경로로 통합되어야 합니다. (예: 보상/상점/동료 선택 로직에서 DB/IDeckService 사용)");
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class ShopUI : MonoBehaviour
{
    [Header("Root & Window")]
    [SerializeField] private CanvasGroup panel;      // ShopUI 루트(CanvasGroup)
    [SerializeField] private RectTransform window;   // 팝업 창(Scale 애니용)
    [SerializeField] private Button dimmerButton;    // 바깥 클릭 닫기
    [SerializeField] private Button closeButton;     // 닫기 버튼

    [Header("M2 Grid")]
    [SerializeField] private Transform gridParent;      // Window/Body/Grid
    [SerializeField] private ShopSlotView slotPrefab;   // ShopSlotView 프리팹

    [Header("Topbar")]
    [SerializeField] private TMP_Text goldText;   // 상단 골드 표시
    [SerializeField] private int testGold = 300;  // 테스트용 시작 골드
    [SerializeField] private TMP_Text rerollPriceText; // 리롤 가격
    [SerializeField] private Button rerollButton;      // 리롤 버튼

    [Header("Reroll Economy")]
    [SerializeField] private int baseReroll = 30;      // 기본 리롤 비용
    [SerializeField] private float rerollGrowth = 1.2f;// 리롤시 매번 가격 20% 증가

    [Header("Deals (오늘의 특가)")]
    [SerializeField, Range(0f,1f)] private float dealChance = 0.25f; // 아이템별 특가 확률
    [SerializeField] private int maxDeals = 2;                         // 이번 상점 최대 특가 수
    [SerializeField] private float dealDiscount = 0.20f;               // 20% 할인

    private const string CardsPath = "Cards"; // Assets/Resources/Cards/*.asset

    [Header("Card Sources")]
    [SerializeField] private List<CardScriptableObject> cardPool = new List<CardScriptableObject>();
    private CardScriptableObject[] _cardSources = new CardScriptableObject[3];

    [Header("Reroll (Cooldown)")]
    [SerializeField, Tooltip("Seconds to lock reroll button after a reroll")]
    private float rerollCooldownSec = 0.20f;   // 리롤 쿨타임
    private bool _isRerollCooling = false;     // 버튼 락 상태


    [SerializeField] private bool verboseLogs = false; //디버그 활성화
    

    // 내부 상태
    private readonly List<ShopSlotView> _views = new();
    private List<ShopSlotVM> _dummy;
    private Coroutine _animCo;
    private bool _isOpen;
    private int _rerollCount;

    private const float OpenDur = 0.18f;   // 페이드/스케일 시간
    private const float CloseDur = 0.16f;
    private const float ScaleFrom = 0.92f; // 팝업 열릴 때 시작 스케일

    // (유물/소모품 풀 – 계속 쓰면 유지)
    private static readonly string[] RelicsPool = {
        "Happy Flower","Anchor","Bronze Idol","Bag of Prep","Kunai","Incense Burner"
    };
    private static readonly string[] ConsumablesPool = {
        "Block Potion","Strength Potion","Dex Potion","Energy Tonic","Small Potion"
    };

    private int Gold 
    {
        get => testGold;
        set {
            testGold = Mathf.Max(0, value);
            goldText?.SetText("{0:#,0}", testGold);
            }
    }

    private void Awake()
    {
        VLog("[ShopUI] Awake running");
        LoadAllCardData();
        if (dimmerButton) dimmerButton.onClick.AddListener(Close);
        if (closeButton)  closeButton.onClick.AddListener(Close);
        if (rerollButton) rerollButton.onClick.AddListener(OnReroll);
        HideImmediate();
    }

    /// <summary>
    /// verboseLogs가 true일 때만 Debug.Log를 출력하는 헬퍼 함수입니다.
    /// </summary>
    /// <param name="message">출력할 메시지</param>
    private void VLog(string message)
    {
        // 스위치가 꺼져있으면(false) 여기서 즉시 함수가 종료됩니다.
        if (!verboseLogs) return;

        // 스위치가 켜져있을 때만 아래 코드가 실행됩니다.
        Debug.Log(message, this);
    }


}
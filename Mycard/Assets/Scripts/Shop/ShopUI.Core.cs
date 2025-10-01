using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class ShopUI : MonoBehaviour
{
    // ==========================================================
    // 1. UI 요소 연결 (View)
    // - 이 스크립트가 제어해야 할 Unity UI 오브젝트들을 연결하는 역할입니다.
    // - 응집도가 매우 높으며, 이 파일에 있는 것이 가장 적절합니다.
    // ==========================================================

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
    [SerializeField] private TMP_Text rerollPriceText; // 리롤 가격
    [SerializeField] private Button rerollButton;      // 리롤 버튼

    // ==========================================================
    // 2. 상점 규칙 및 데이터 (Model / Business Logic)
    // - 상점의 '규칙'(리롤 가격, 할인율 등)과 원본 '데이터'(카드, 유물 목록)입니다.
    // - 이 부분은 나중에 'ShopService'라는 전문가에게 옮기면 더 좋은 기능들입니다.
    // - 지금은 ShopUI가 '화면 표시'뿐만 아니라 '상점 규칙 계산'까지 책임지고 있어 역할이 너무 많습니다.
    // ==========================================================

    [Header("Reroll Economy")]
    [SerializeField] private int testGold = 300;  // 테스트용 시작 골드
    [SerializeField] private int baseReroll = 30;      // 기본 리롤 비용
    [SerializeField] private float rerollGrowth = 1.2f;// 리롤시 매번 가격 20% 증가

    [Header("Deals (오늘의 특가)")]
    [SerializeField, Range(0f,1f)] private float dealChance = 0.25f; // 아이템별 특가 확률
    [SerializeField] private int maxDeals = 2;                         // 이번 상점 최대 특가 수
    [SerializeField] private float dealDiscount = 0.20f;               // 20% 할인

    [Header("Card Sources")]
    [SerializeField] private List<CardScriptableObject> cardPool = new List<CardScriptableObject>(); // 카드 데이터 원본 목록
    private const string CardsPath = "Cards"; // 카드 원본 데이터가 있는 리소스 폴더 경로

    // (유물/소모품 풀 – 계속 쓰면 유지)
    private static readonly string[] RelicsPool = {
        "Happy Flower","Anchor","Bronze Idol","Bag of Prep","Kunai","Incense Burner"
    };
    private static readonly string[] ConsumablesPool = {
        "Block Potion","Strength Potion","Dex Potion","Energy Tonic","Small Potion"
    };

    // ==========================================================
    // 3. 내부 상태 관리 (Controller / State)
    // - 상점이 열려있는 동안의 상태를 저장하고 관리하는 변수들입니다.
    // - 이 기능들은 'ShopPresenter'나 'ShopOverlayController' 같은 중간 관리자가 가져가면 더 좋습니다.
    // - 지금은 ShopUI가 화면 표시(View)와 상태 관리(Controller)를 모두 하고 있어 복잡합니다.
    // ==========================================================

    private CardScriptableObject[] _cardSources = new CardScriptableObject[3]; // 리롤을 위해 현재 카드 3개의 원본을 저장
    private float rerollCooldownSec = 0.20f;   // 리롤 쿨타임
    private bool _isRerollCooling = false;     // 버튼 락 상태

    // 카드 속성 CardId/Name 빠르게 찾기 위한 맵
    private readonly Dictionary<string, CardScriptableObject> _cardIdMap = new();
    private readonly Dictionary<string, CardScriptableObject> _cardNameMap = new(System.StringComparer.OrdinalIgnoreCase);

    // 식별용 해시셋 (이름으로 유물/소모품인지 빠르게 판단)
    private static readonly HashSet<string> _relicsSet = new HashSet<string>(RelicsPool);
    private static readonly HashSet<string> _consumablesSet = new HashSet<string>(ConsumablesPool);


    [SerializeField] private bool verboseLogs = false; //디버그 활성화

    // 상점 진입중 확인용 (노드에 있는동안 다시 열수 있게)
    private bool _sessionInitialized = false;

    private readonly List<ShopSlotView> _views = new(); // 현재 상점에 진열된 아이템 목록의 '실제 UI 오브젝트' (View)
    private List<ShopSlotVM> _dummy; // 현재 상점에 진열된 아이템 목록의 '데이터' (ViewModel)

    private Coroutine _animCo; // UI 애니메이션 코루틴
    private bool _isOpen = false; // 상점이 현재 열려있는지 여부
    public bool IsOpen => _isOpen; // 외부에서 _isOpen 상태를 읽을 수만 있도록 공개
    private int _rerollCount; // 현재 리롤 횟수

    private const float OpenDur = 0.18f;   // 페이드/스케일 시간
    private const float CloseDur = 0.16f;
    private const float ScaleFrom = 0.92f; // 팝업 열릴 때 시작 스케일

    
    

    #region --- 데이터 포장/개봉 (DTO) ---



    // '포장 기술' (ShopUI의 현재 상태를 -> 택배 상자에 담기)
    public ShopSessionDTO ExportSession()
    {
        // 안전장치: 아직 상점이 열리기 전이거나 슬롯이 비어있는 예외 상황을 방어합니다.
        if (_dummy == null || _dummy.Count == 0)
        {
            return new ShopSessionDTO
            {
                rerollCount = _rerollCount,
                slots = System.Array.Empty<SlotDTO>() // 빈 배열을 반환하여 오류를 막습니다.
            };
        }

        var dto = new ShopSessionDTO
        {
            rerollCount = _rerollCount,
            slots = new SlotDTO[_dummy.Count]
        };
        
        for (int i = 0; i < _dummy.Count; i++)
        {
            var vm = _dummy[i];

            // itemId는 반드시 카드의 고유 ID('CardId')로 저장
            string itemId = (vm.cardData != null && !string.IsNullOrEmpty(vm.cardData.CardId))
                ? vm.cardData.CardId
                : (vm.title ?? string.Empty);
            
            // 기준가 보정 로직 (vm.price가 0/미설정인 레거시/초기 상태 대비)
            int basePrice = vm.price;
            if (basePrice <= 0)
            {
                string assumedDetail = string.IsNullOrEmpty(vm.detail) && vm.cardData != null ? "Card" : vm.detail;
                string nameForPrice  = vm.cardData != null ? vm.cardData.cardName : vm.title;
                CardRarity rarityForPrice = vm.cardData != null ? vm.cardData.Rarity : vm.rarity;
                basePrice = BasePriceOf(assumedDetail, nameForPrice, rarityForPrice);
            }

            // DTO에 '최종 가격'을 저장합니다.
            var tmp = vm;
            tmp.price = basePrice;
            int finalPrice = FinalPrice(in tmp);

            dto.slots[i] = new SlotDTO {
                itemId = itemId,
                soldOut = vm.soldOut,
                detail = string.IsNullOrEmpty(vm.detail) && vm.cardData != null ? "Card" : vm.detail,
                price = Mathf.Max(0, finalPrice), // 가격이 음수가 되지 않도록 보정
                isDeal = vm.isDeal,
                rarity = vm.cardData != null ? vm.cardData.Rarity : vm.rarity
            };
        }
        return dto;
    }

    // '개봉 기술' (택배 상자를 열어서 -> ShopUI의 상태를 복원)
    public void ImportSession(ShopSessionDTO dto)
    {
        if (_cardIdMap.Count == 0) LoadAllCardData();
        
        // [CCTV] 함수가 어떤 데이터로 시작하는지 확인
        Debug.Log($"<color=purple>[CCTV] ImportSession 시작. 현재 '카드 전화번호부'에 등록된 카드 수: {_cardIdMap.Count}개</color>", this);


        if (dto == null || dto.slots == null || dto.slots.Length == 0)
        {
            // [CCTV] 유효하지 않은 데이터로 리셋되는지 확인
            Debug.LogWarning($"<color=yellow>[Import] DTO 데이터가 비어있어 상점을 리셋합니다.</color>", this);
            ResetSession();
            return;
        }

        _rerollCount = dto.rerollCount;
        _dummy = new List<ShopSlotVM>(dto.slots.Length);

        // [CCTV] 복원할 아이템 개수와 리롤 횟수 확인
        Debug.Log($"<color=lightblue>[Import] 복원 시작: 총 {dto.slots.Length}개의 아이템, 리롤 횟수 {_rerollCount}</color>", this);

        // 리롤 로직에 필요한 카드 소스 데이터를 초기화합니다.
        for (int i = 0; i < _cardSources.Length; i++) _cardSources[i] = null;

        // 할인율 역산에 사용할 상수를 미리 계산 (0으로 나누는 오류 방지)
        float k = Mathf.Clamp01(1f - dealDiscount);

        for (int i = 0; i < dto.slots.Length; i++)
        {
            var slotData = dto.slots[i];
            ShopSlotVM vm;

            // [CCTV] 각 슬롯의 원본 데이터 확인
            Debug.Log($" - 슬롯 #{i} 복원 시도: itemId='{slotData.itemId}', soldOut={slotData.soldOut}");

            // --- 1. 카드/비카드 판별 ---
            CardScriptableObject so = null;
            string id = slotData.itemId ?? ""; // null 방지
            if (!string.IsNullOrEmpty(id))
            {
                // CardId 우선, 실패 시 CardName으로 찾는 과거 데이터 호환 로직
                if (_cardIdMap.TryGetValue(id, out var byId)) so = byId;
                else if (_cardNameMap.TryGetValue(id, out var byName)) so = byName;
            }

            if (so != null)
            {
                vm = ToVM(so);
                if (i < 3) _cardSources[i] = so; // 리롤 원본 보존
            }
            else
            {
                // DTO에 타입 정보가 있으면 그걸 쓰고, 없으면 추론합니다.
                string detail = !string.IsNullOrEmpty(slotData.detail) ? slotData.detail 
                            : _consumablesSet.Contains(id) ? "Consumable" 
                            : "Relic";
                var rarity = slotData.rarity;
                vm = new ShopSlotVM { title = id, detail = detail, rarity = rarity };
            }

            // --- 2. 가격/특가 복원 (가장 정교한 부분) ---
            int storedFinal = Mathf.Max(0, slotData.price);
            bool isDeal = slotData.isDeal;

            int basePrice;
            if (storedFinal > 0)
            {
                if (isDeal)
                {
                    // ★ 핵심: 최종 할인가에서 원래 기준가를 오차 없이 역으로 계산합니다.
                    basePrice = Mathf.FloorToInt((storedFinal - 1f) / Mathf.Max(0.01f, k)) + 1;
                }
                else
                {
                    basePrice = storedFinal;
                }
            }
            else
            {
                // ★ 안전장치 3: 가격 정보가 없는 과거 데이터를 위한 대비책
                string nameForPrice = (vm.cardData != null) ? vm.cardData.cardName : vm.title;
                CardRarity rarityForPrice = vm.cardData != null ? vm.cardData.Rarity : vm.rarity;
                basePrice = BasePriceOf(vm.detail, nameForPrice, rarityForPrice);
            }
            
            vm.price = Mathf.Max(1, basePrice); // 가격이 최소 1 이상이 되도록 보정
            vm.isDeal = isDeal;
            vm.soldOut = slotData.soldOut;

            _dummy.Add(vm);
        }

        // [CCTV] 최종 복원된 아이템 수 확인
        Debug.Log($"<color=lightblue>[Import] 최종 복원된 아이템 수: {_dummy.Count}개</color>", this);


        _sessionInitialized = true;
        RebuildGrid();
        RefreshTopbar();
    }

    #endregion

    #region --- 지갑 연동 ---

    // 외부에서 진짜 지갑의 기능을 연결해줄 통로 (Delegate)
    public System.Func<int> GetGold;
    public System.Func<int, bool> SpendGold;
    public System.Func<string, bool> TryAddCardToDeck;
    public System.Action OnSessionChanged; // 구매/리롤 시 "상태 바뀌었음!"이라고 알리는 신호

    // 기존 Gold 프로퍼티를 아래와 같이 수정
    private int Gold
    {
        get => GetGold != null ? GetGold() : testGold;
    }

    #endregion

    private bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;

        if (SpendGold != null)
        {
            return SpendGold(amount);
        }

        if (testGold < amount)
        {
            VLog($"[ShopUI] TrySpendGold 실패: 테스트 골드 부족 (have={testGold}, need={amount})");
            return false;
        }

        testGold = Mathf.Max(0, testGold - amount);
        return true;
    }

    private void RefundGold(int amount)
    {
        if (amount <= 0) return;

        if (SpendGold != null)
        {
            SpendGold(-amount);
            return;
        }

        testGold += amount;
    }

    private bool TryResolveCardId(in ShopSlotVM vm, out string cardId)
    {
        cardId = null;

        if (vm.cardData != null && !string.IsNullOrEmpty(vm.cardData.CardId))
        {
            cardId = vm.cardData.CardId;
            return true;
        }

        if (!string.IsNullOrEmpty(vm.title))
        {
            if (_cardIdMap.TryGetValue(vm.title, out var byId) && byId != null && !string.IsNullOrEmpty(byId.CardId))
            {
                cardId = byId.CardId;
                return true;
            }

            if (_cardNameMap.TryGetValue(vm.title, out var byName) && byName != null && !string.IsNullOrEmpty(byName.CardId))
            {
                cardId = byName.CardId;
                return true;
            }
        }

        return false;
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

    // 노드에서 떠나면 진입중에서 벗어남
    public void ResetSession()
    {
        _sessionInitialized = false;
    }


}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUI : MonoBehaviour
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

    


    // 내부 상태
    private readonly List<ShopSlotView> _views = new();
    private List<ShopSlotVM> _dummy;
    private Coroutine _animCo;
    private bool _isOpen;
    private int _rerollCount;

    private const float OpenDur = 0.18f;   // 페이드/스케일 시간
    private const float CloseDur = 0.16f;
    private const float ScaleFrom = 0.92f; // 팝업 열릴 때 시작 스케일

    // 간단 아이템 풀(실제품은 SO/DB로 대체)
    private static readonly string[] CardsPool = {
        "Strike","Defend","Fireball","Zap","Barrier","Dagger","Poison","Shield Bash"
    };
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
            if (goldText) goldText.text = testGold.ToString("N0");
            }
    }

    private void Awake()
    {
        if (dimmerButton) dimmerButton.onClick.AddListener(Close);
        if (closeButton)  closeButton.onClick.AddListener(Close);
        if (rerollButton) rerollButton.onClick.AddListener(OnReroll);
        HideImmediate();
    }

    // === 외부에서 호출(버튼/노드 클릭) ===
    public void Open()
    {
        if (_isOpen) return;

        // 1) 먼저 켠다 (비활성에서 코루틴 시작 불가)
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 2) 초기 표시 상태(투명/입력막기/스케일) 세팅
        panel.blocksRaycasts = false;
        panel.interactable = false;
        panel.alpha = 0f;
        if (window) window.localScale = Vector3.one * ScaleFrom;

        _rerollCount = 0;
        Gold = testGold; // 골드 표시

        // M2 더미 바인딩
        BuildDummySlots();
        ApplyDeals();
        RebuildGrid();
        RefreshTopbar();

        // 3) 애니 시작(중첩 방지)
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateOpen());
        _isOpen = true;
    }

    public void Close()
    {
        if (!_isOpen) return;
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateClose());
        _isOpen = false;
    }

    private void HideImmediate()
    {
        if (panel == null) panel = GetComponent<CanvasGroup>();
        gameObject.SetActive(false);
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        if (window) window.localScale = Vector3.one * ScaleFrom;
    }

    private int BasePriceOf(string detail, string title)
    {
        // 심플 룰: 종류별 기준가 + 이름 길이 보정(선택)
        int baseVal = detail == "Card" ? 50 :
                    detail == "Relic" ? 120 :
                    detail == "Consumable" ? 40 : 50;
        baseVal += Mathf.Clamp((title?.Length ?? 0) * 2, 0, 30);
        return baseVal;
    }

    private int FinalPrice(in ShopSlotVM v)
    {
        // v.isDeal 이면 할인 적용
        return v.isDeal
        ? Mathf.Max(1, Mathf.CeilToInt(v.price * (1f - dealDiscount)))
        : v.price;
    }

    // === M2: 더미 슬롯 6개 구성 ===
    private void BuildDummySlots()
    {
        _dummy = new List<ShopSlotVM>(6) 
        {
            new ShopSlotVM{ title="Strike",        detail="Card" },
            new ShopSlotVM{ title="Defend",        detail="Card" },
            new ShopSlotVM{ title="Fireball",      detail="Card" },
            new ShopSlotVM{ title="Happy Flower",  detail="Relic" },
            new ShopSlotVM{ title="Anchor",        detail="Relic" },
            new ShopSlotVM{ title="Block Potion",  detail="Consumable" },
        };

        for (int i = 0; i < _dummy.Count; i++)
        {
            var vm = _dummy[i];
            vm.price = BasePriceOf(vm.detail, vm.title);
            _dummy[i] = vm;
        }
    }

    private void ApplyDeals()
    {
        // 판매 가능한 슬롯만 초기화
        for (int i = 0; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            var vm = _dummy[i];
            vm.isDeal = false;
            _dummy[i] = vm;
        }

        // 확률로 선택
        var picked = new List<int>(_dummy.Count);
        for (int i = 0; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            if (Random.value < dealChance) picked.Add(i);
        }

        // 최대 개수 제한
        while (picked.Count > Mathf.Max(0, maxDeals))
        picked.RemoveAt(Random.Range(0, picked.Count));

        // 적용
        foreach (var idx in picked)
        {
            var vm = _dummy[idx];
            vm.isDeal = true;
            _dummy[idx] = vm;
        }
    }

    private void RebuildGrid()
    {
        foreach (Transform c in gridParent) Destroy(c.gameObject);
        _views.Clear();

        for (int i = 0; i < _dummy.Count; i++)
        {
            int slotIndex = i;
            var view = Instantiate(slotPrefab, gridParent);
            view.SetDealDiscount(dealDiscount);

            bool canBuy = !_dummy[slotIndex].soldOut && (Gold >= FinalPrice(_dummy[slotIndex]));
            view.Bind(_dummy[slotIndex], () => TryBuy(slotIndex), canBuy);

            _views.Add(view);
        }
    }


    private void RefreshViews()
    {
        for (int i = 0; i < _dummy.Count && i < _views.Count; i++)
        {
            int slotIndex = i; // 람다 캡처용
            _views[i].SetDealDiscount(dealDiscount); 
            bool canBuy = !_dummy[slotIndex].soldOut && (Gold >= FinalPrice(_dummy[slotIndex]));
            _views[i].Bind(_dummy[slotIndex], () => TryBuy(slotIndex), canBuy);
        }
    }


    private void RefreshTopbar()
    {
        int cost = CurrentRerollCost();
        if (rerollPriceText) rerollPriceText.text = cost.ToString("N0");
        if (rerollButton) rerollButton.interactable = (Gold >= cost);
    }

    private int CurrentRerollCost()
    {
        // base * (1.2^count) 반올림
        return Mathf.RoundToInt(baseReroll * Mathf.Pow(rerollGrowth, _rerollCount));
    }



    private void TryBuy(int index)
    {
        if (index < 0 || index >= _dummy.Count) return;
        var vm = _dummy[index]; // "vm은 _dummy 리스트의 index번째 아이템이야" 라고 알려줌

        if (vm.soldOut) return;
        int cost = FinalPrice(vm);

        if (Gold < cost) return; // 부족하면 무시

        // 결제
        Gold -= cost;

        // 품절 처리
        vm.soldOut = true;
        _dummy[index] = vm;

        RefreshViews(); // 버튼/가격 색상 갱신
        RefreshTopbar(); // 리롤 버튼 갱신
    }

        // === M5: 리롤 ===
    private void OnReroll()
    {
        int cost = CurrentRerollCost();
        if (Gold < cost) return;

        if (rerollButton) rerollButton.interactable = false;

        Gold -= cost;
        _rerollCount++;

        // 현재 화면의 모든 아이템(구매하여 SOLD OUT 포함)을 제외
        var exclude = new HashSet<string>();
        for (int i = 0; i < _dummy.Count; i++)
            if (!string.IsNullOrEmpty(_dummy[i].title))
                exclude.Add(_dummy[i].title);

        // 같은 리롤 내 중복 방지를 위해, 새로 뽑은 것도 exclude에 계속 추가
        for (int i = 0; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue; // SOLD OUT 칸은 채우지 않음

            string[] pool = _dummy[i].detail == "Card" ? CardsPool :
                            _dummy[i].detail == "Relic" ? RelicsPool :
                            ConsumablesPool;

            string newId = DrawUnique(pool, exclude);
            if (string.IsNullOrEmpty(newId))
                continue;                 // 이 슬롯은 변경하지 않음

            exclude.Add(newId);
            var vm = _dummy[i];
            vm.title = newId;
            vm.icon = null; // 아이콘은 생략(데이터 연결 시 대체)
            vm.price = BasePriceOf(vm.detail, vm.title);
            // vm.soldOut 유지(어차피 false)
            _dummy[i] = vm;
        }

        ApplyDeals();
        RefreshViews();
        RefreshTopbar();
    }

    private string DrawUnique(string[] pool, HashSet<string> exclude)
    {
        var candidates = new List<string>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
        {
            string p = pool[i];
            if (!exclude.Contains(p))
                candidates.Add(p);
        }

        // 후보가 없으면 교체하지 않음
        if (candidates.Count == 0)
            return null;

        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    // === 애니메이션 ===
    private IEnumerator AnimateOpen()
{
    // 여기의 gameObject.SetActive(true); 는 제거!

    float t = 0f;
    while (t < OpenDur)
    {
        t += Time.unscaledDeltaTime;
        float u = Mathf.Clamp01(t / OpenDur);

        panel.alpha = u; // 페이드 인
        if (window)
        {
            float s = Mathf.SmoothStep(ScaleFrom, 1f, u);
            window.localScale = new Vector3(s, s, 1f);
        }
        yield return null;
    }

    panel.alpha = 1f;
    if (window) window.localScale = Vector3.one;
    panel.blocksRaycasts = true;
    panel.interactable = true;
    _animCo = null;
}

    private IEnumerator AnimateClose()
    {
        panel.blocksRaycasts = false; // 닫히는 동안 바깥 클릭 막음
        panel.interactable = false;

        float startAlpha = panel.alpha;
        float t = 0f;
        while (t < CloseDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / CloseDur);

            panel.alpha = Mathf.Lerp(startAlpha, 0f, u); // 페이드 아웃
            if (window)
            {
                float s = Mathf.SmoothStep(1f, ScaleFrom, u);
                window.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }

        panel.alpha = 0f;
        if (window) window.localScale = Vector3.one * ScaleFrom;
        gameObject.SetActive(false);
        _animCo = null;
    }
}
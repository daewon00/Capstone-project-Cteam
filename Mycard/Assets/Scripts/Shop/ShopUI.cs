using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    // 내부 상태
    private readonly List<ShopSlotView> _views = new();
    private List<ShopSlotVM> _dummy;
    private Coroutine _animCo;
    private bool _isOpen;

    private const float OpenDur = 0.18f;   // 페이드/스케일 시간
    private const float CloseDur = 0.16f;
    private const float ScaleFrom = 0.92f; // 팝업 열릴 때 시작 스케일

    private void Awake()
    {
        if (dimmerButton) dimmerButton.onClick.AddListener(Close);
        if (closeButton)  closeButton.onClick.AddListener(Close);
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

    // M2 더미 바인딩
    BuildDummySlots();
    RebuildGrid();

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

    // === M2: 더미 슬롯 6개 구성 ===
    private void BuildDummySlots()
    {
        _dummy = new List<ShopSlotVM>(6) {
            new ShopSlotVM{ title="Strike",        detail="Card" },
            new ShopSlotVM{ title="Defend",        detail="Card" },
            new ShopSlotVM{ title="Fireball",      detail="Card" },
            new ShopSlotVM{ title="Happy Flower",  detail="Relic" },
            new ShopSlotVM{ title="Anchor",        detail="Relic" },
            new ShopSlotVM{ title="Block Potion",  detail="Consumable" },
        };
    }

    private void RebuildGrid()
    {
        foreach (Transform c in gridParent) Destroy(c.gameObject);
        _views.Clear();

        for (int i = 0; i < _dummy.Count; i++)
        {
            int slotIndex = i;               // ★ 클로저 안전
            var vm = _dummy[slotIndex];
            var view = Instantiate(slotPrefab, gridParent);
            view.Bind(vm, () => TryBuy(slotIndex));   // ★ 여기
            _views.Add(view);
        }
    }

    private void RefreshViews()
    {
        for (int i = 0; i < _dummy.Count && i < _views.Count; i++)
        {
            int slotIndex = i; // ★★ 중요: 캡처용 로컬 변수
             _views[i].Bind(_dummy[i], () => TryBuy(slotIndex));
        }
    }

    private void TryBuy(int index)
    {
        if (index < 0 || index >= _dummy.Count) return;
        if (_dummy[index].soldOut) return;

        // VM은 struct라 통째로 수정해서 다시 대입
        var vm = _dummy[index];
        vm.soldOut = true;
        _dummy[index] = vm;

        RefreshViews(); // 오버레이 갱신
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
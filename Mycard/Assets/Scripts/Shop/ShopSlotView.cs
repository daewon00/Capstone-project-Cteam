using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Save;

/// <summary>
/// 상점 슬롯 하나를 구성하고 클릭 시 콜백을 호출하는 뷰입니다.
/// </summary>
public class ShopSlotView : MonoBehaviour
{
    [Header("Card Display")]
    [SerializeField] private CardDisplay cardDisplay;
    [SerializeField] private CardDisplay cardDisplayPrefab;
    [SerializeField] private RectTransform cardDisplayRoot;

    [Header("Legacy Elements")]
    public Button button;                // 슬롯 클릭 버튼
    public Image icon;                   // 카드 이외 아이템 아이콘
    public TMP_Text titleText;           // 제목 (구형 카드 UI 또는 기타 아이템명)
    public TMP_Text detailText;          // 세부 설명
    public GameObject soldOutOverlay;    // 판매 완료 오버레이
    public TMP_Text originalPriceText;   // (선택) 원가 텍스트(취소선)
    public GameObject dealBadge;         // "-20%" 같은 배지

    [Header("Price")]
    public TMP_Text priceText;           // 최종 가격 텍스트

    private string _logName;

    [SerializeField, Range(0f, 1f)]
    private float dealDiscountVisual = 0.20f;

    public void SetDealDiscount(float value)
    {
        dealDiscountVisual = Mathf.Clamp01(value);
    }

    /// <summary>
    /// 슬롯 UI를 주어진 뷰 모델로 갱신하고 클릭 콜백을 설정합니다.
    /// </summary>
    public void Bind(ShopSlotVM vm, Action onClick, bool canBuy = true)
    {
        _logName = vm.title;

        bool isCardSlot = vm.cardData != null || string.Equals(vm.detail, "Card", StringComparison.OrdinalIgnoreCase);
        var previewState = isCardSlot ? CreatePreviewState(in vm) : null;
        bool useLegacyVisuals = cardDisplay == null || !isCardSlot;

        // 1) 버튼 참조 선확보(Null 방지)
        if (!button) button = GetComponent<Button>();
        EnsureCardDisplay();

        // 2) 텍스트/아이콘 표시
        if (titleText != null)
        {
            titleText.gameObject.SetActive(useLegacyVisuals);
            titleText.SetText(useLegacyVisuals ? vm.title ?? string.Empty : string.Empty);
        }

        if (detailText != null)
        {
            detailText.gameObject.SetActive(useLegacyVisuals);
            detailText.SetText(useLegacyVisuals ? vm.detail ?? string.Empty : string.Empty);
        }

        if (icon != null)
        {
            if (useLegacyVisuals)
            {
                icon.gameObject.SetActive(true);
                icon.sprite = vm.icon;
                icon.enabled = vm.icon != null;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
                icon.gameObject.SetActive(false);
            }
        }

        if (cardDisplay != null)
        {
            cardDisplay.gameObject.SetActive(isCardSlot);
            if (isCardSlot)
            {
                cardDisplay.Bind(vm.cardData, previewState);
            }
            else
            {
                cardDisplay.Clear();
            }
        }

        // 3) 최종가 계산(특가 적용)
        int finalPrice = vm.isDeal
            ? Mathf.Max(1, Mathf.CeilToInt(vm.price * (1f - dealDiscountVisual)))
            : vm.price;

        priceText?.SetText("{0:#,0}", finalPrice);

        // 4) 원가/배지 표시
        if (originalPriceText != null)
        {
            bool showOrig = vm.isDeal;
            originalPriceText.gameObject.SetActive(showOrig);
            if (showOrig)
            {
                originalPriceText.SetText("<s>{0:#,0}</s>", vm.price); // Rich Text ON 필수
            }
        }

        if (dealBadge != null)
        {
            dealBadge.SetActive(vm.isDeal);
        }

        // 5) SoldOut/구매가능 상태 반영
        if (soldOutOverlay != null)
        {
            soldOutOverlay.SetActive(vm.soldOut);
        }

        button.interactable = !vm.soldOut && canBuy;

        // 6) 가격 색상 (부족 시 빨강)
        if (priceText != null)
        {
            priceText.color = (!vm.soldOut && !canBuy) ? new Color(0.9f, 0.2f, 0.2f) : Color.white;
        }

        // 7) 클릭 핸들러
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (vm.soldOut || !canBuy)
                return;

            button.interactable = false;   // 즉시 잠금(더블클릭 방지)
            onClick?.Invoke();
        });
    }

    private void EnsureCardDisplay()
    {
        if (cardDisplay != null)
            return;

        if (cardDisplayPrefab == null || cardDisplayRoot == null)
            return;

        var instance = Instantiate(cardDisplayPrefab, cardDisplayRoot);
        cardDisplay = instance;

        if (instance != null)
        {
            var rect = instance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
        }

        cardDisplay.gameObject.SetActive(false);
    }

    private void Reset()
    {
        if (!button) button = GetComponent<Button>();
        if (!icon) icon = transform.Find("Icon")?.GetComponent<Image>();
        if (!titleText) titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!detailText) detailText = transform.Find("Detail")?.GetComponent<TMP_Text>();
        if (!priceText) priceText = transform.Find("Price")?.GetComponent<TMP_Text>();
        if (!originalPriceText) originalPriceText = transform.Find("OriginalPrice")?.GetComponent<TMP_Text>();
        if (!soldOutOverlay) soldOutOverlay = transform.Find("SoldOutOverlay")?.gameObject;
        if (!dealBadge) dealBadge = transform.Find("DealBadge")?.gameObject;
    }

    private static CardRuntimeState CreatePreviewState(in ShopSlotVM vm)
    {
        if (vm.cardData == null || string.IsNullOrEmpty(vm.cardData.CardId))
            return null;

        var state = new CardRuntimeState
        {
            InstanceId = vm.cardData.CardId,
            CardId = vm.cardData.CardId,
            ModifiersJson = string.Empty
        };
        state.SetUpgraded(false);
        return state;
    }
}

/// <summary>
/// 상점 슬롯에 표시될 데이터를 담는 뷰 모델입니다.
/// </summary>
[Serializable]
public struct ShopSlotVM
{
    public CardScriptableObject cardData;   // 카드 원본(설계도)을 담아둘 공간
    public string title;                    // 예: "Strike"
    public string itemId;                   // 실제 ID (카드ID/유물ID 등)
    public string detail;                   // 예: "Card" / "Relic" / "Consumable"
    public Sprite icon;                     // (선택) 아이콘 없으면 null
    public bool soldOut;                    // 판매 완료 여부
    public int price;                       // 가격
    public bool isDeal;                     // 특가 여부
    public CardRarity rarity;               // 카드 희귀도 (카드가 아닌 경우 기본값)
}

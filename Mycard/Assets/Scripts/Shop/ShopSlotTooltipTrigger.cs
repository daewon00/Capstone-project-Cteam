using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shop slot long-press tooltip handler. Short tap = purchase, 1s press = tooltip.
/// </summary>
public class ShopSlotTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Tooltip("길게 눌러 툴팁을 표시하기까지 필요한 누름 시간(초)")]
    private float longPressDuration = 1f;
    [SerializeField, Tooltip("툴팁을 유지할 시간(초). 경과 후 자동으로 숨깁니다.")]
    private float tooltipHoldSeconds = 2f;

    private bool _isCardSlot;
    private bool _isRelicSlot;
    private ICardTooltipSource _cardTooltipSource;
    private RelicData _relicData;
    private Action _onPurchase;

    private bool _isPressing;
    private bool _pressStartedWithTooltip;
    private float _pressStartTime;
    private bool _tooltipVisible;
    private Coroutine _autoHideRoutine;
    private bool _longPressFired;
    private bool _autoHidePending;

    // 유물 툴팁 위치를 슬롯 아이콘 주변에 고정하기 위한 앵커
    private RectTransform _relicAnchorRect;

    public void Configure(ShopSlotVM vm, bool isCardSlot, bool isRelicSlot, CardDisplay cardDisplay, RelicData relicData, Action onPurchase, RectTransform relicAnchor)
    {
        _isCardSlot = isCardSlot;
        _isRelicSlot = isRelicSlot;
        _cardTooltipSource = isCardSlot ? ResolveCardTooltipSource(cardDisplay, vm) : null;
        _relicData = isRelicSlot ? relicData : null;
        _onPurchase = onPurchase;
        _relicAnchorRect = relicAnchor;
        _longPressFired = false;
        GameLog.Info($"[ShopSlotTooltipTrigger] Configure isCard={isCardSlot} isRelic={isRelicSlot} title={vm.title} detail={vm.detail}");
    }

    public bool ShouldBlockClick => _tooltipVisible || _longPressFired;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_tooltipVisible)
        {
            // 이미 툴팁이 떠 있는 상태에서의 탭은 구매 시도로 처리하고, 재롱프레스는 막는다.
            _pressStartedWithTooltip = true;
            GameLog.Info("[ShopSlotTooltipTrigger] PointerDown while tooltip visible: mark pressStartedWithTooltip");
        }

        _isPressing = true;
        _pressStartTime = Time.time;
        GameLog.Info($"[ShopSlotTooltipTrigger] PointerDown start pressing at {Time.time}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool hadPress = _isPressing || _longPressFired;
        bool longPressed = _longPressFired || (Time.time - _pressStartTime >= longPressDuration);

        if (!hadPress)
            return;

        if (_pressStartedWithTooltip)
        {
            _pressStartedWithTooltip = false;
            _isPressing = false;
            _longPressFired = false;
            HideTooltip("PointerUpWithVisibleTooltip");
            TriggerPurchase();
            GameLog.Info("[ShopSlotTooltipTrigger] PointerUp while tooltip visible -> hide + purchase");
            return;
        }

        if (_tooltipVisible)
        {
            // 롱프레스로 띄운 상태: 이번 입력에서는 구매하지 않고, 손 뗀 뒤 유지 타이머를 시작
            RestartAutoHide();
            _isPressing = false;
            _longPressFired = false;
            GameLog.Info("[ShopSlotTooltipTrigger] PointerUp after long press -> start auto hide, skip purchase");
            return;
        }

        if (!longPressed)
        {
            TriggerPurchase();
            GameLog.Info("[ShopSlotTooltipTrigger] PointerUp short tap -> purchase");
        }
        // longPressed 상태는 Update에서 이미 처리되어 tooltipVisible=true가 됨

        _isPressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltipVisible && _autoHidePending && _autoHideRoutine != null)
        {
            GameLog.Info("[ShopSlotTooltipTrigger] PointerExit ignored because auto-hide pending");
            return;
        }
        CancelPress();
        HideTooltip("PointerExit");
    }

    private void Update()
    {
        if (!_isPressing || _tooltipVisible)
            return;

        if (Time.time - _pressStartTime >= longPressDuration)
        {
            ShowTooltip();
            _longPressFired = true;
            GameLog.Info("[ShopSlotTooltipTrigger] Long press detected -> show tooltip");
        }
    }

    private void ShowTooltip()
    {
        bool shown = false;

        if (_isCardSlot && _cardTooltipSource != null && _cardTooltipSource.IsTooltipValid)
        {
            var svc = ServiceRegistry.Get<ICardTooltipService>();
            if (svc != null)
            {
                svc.Show(_cardTooltipSource);
                shown = true;
                GameLog.Info("[ShopSlotTooltipTrigger] Show card tooltip via CardTooltipService");
            }
            else
            {
                GameLog.Warn("[ShopSlotTooltipTrigger] CardTooltipService missing");
            }
        }
        else if (_isRelicSlot && _relicData != null)
        {
            var tm = TooltipManager.Instance;
            if (tm != null)
            {
                // 상점에서는 유물 툴팁을 슬롯 아이콘 기준으로 표시한다.
                Vector2 screenPos = Input.mousePosition;
                if (_relicAnchorRect != null)
                {
                    var canvas = _relicAnchorRect.GetComponentInParent<Canvas>();
                    Camera cam = null;
                    if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        cam = canvas.worldCamera;
                    }
                    screenPos = RectTransformUtility.WorldToScreenPoint(cam, _relicAnchorRect.position);
                }

                // 카드 툴팁과 비슷하게 위쪽으로 띄우는 오프셋 적용
                var offset = new Vector2(0f, 48f);
                tm.ShowTooltipAtScreenPosition(_relicData.displayName, _relicData.description, screenPos, offset);
                shown = true;
                GameLog.Info("[ShopSlotTooltipTrigger] Show relic tooltip via TooltipManager (anchored)");
            }
            else
            {
                GameLog.Warn("[ShopSlotTooltipTrigger] TooltipManager missing");
            }
        }
        else
        {
            GameLog.Warn("[ShopSlotTooltipTrigger] No valid tooltip source");
        }

        if (!shown)
            return;

        _tooltipVisible = true;
        _autoHidePending = false;
        GameLog.Info("[ShopSlotTooltipTrigger] Tooltip marked visible");
    }

    private void HideTooltip(string reason = "Unknown")
    {
        if (!_tooltipVisible)
            return;

        _tooltipVisible = false;
        _longPressFired = false;
        _autoHidePending = false;

        if (_autoHideRoutine != null)
        {
            StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = null;
        }

        if (_isCardSlot && _cardTooltipSource != null)
        {
            ServiceRegistry.Get<ICardTooltipService>()?.Hide(_cardTooltipSource);
        }
        else if (_isRelicSlot)
        {
            TooltipManager.Instance?.HideTooltip();
        }

        GameLog.Info($"[ShopSlotTooltipTrigger] HideTooltip invoked reason={reason}");
    }

    private void RestartAutoHide()
    {
        if (_autoHideRoutine != null)
            StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = StartCoroutine(AutoHide());
        _autoHidePending = true;
        GameLog.Info($"[ShopSlotTooltipTrigger] AutoHide scheduled in {tooltipHoldSeconds} seconds");
    }

    private IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(tooltipHoldSeconds);
        _autoHideRoutine = null;
        _autoHidePending = false;
        HideTooltip("AutoHideTimerElapsed");
        GameLog.Info("[ShopSlotTooltipTrigger] AutoHide timer elapsed");
    }

    private void TriggerPurchase()
    {
        _onPurchase?.Invoke();
        GameLog.Info("[ShopSlotTooltipTrigger] TriggerPurchase invoked");
    }

    private void CancelPress()
    {
        _isPressing = false;
        _pressStartedWithTooltip = false;
    }

    private void OnDisable()
    {
        CancelPress();
        HideTooltip("OnDisable");
        GameLog.Info("[ShopSlotTooltipTrigger] OnDisable -> cancel + hide");
    }

    private ICardTooltipSource ResolveCardTooltipSource(CardDisplay display, in ShopSlotVM vm)
    {
        if (display != null)
        {
            var src = display.GetComponent<CardDisplayTooltipSource>();
            if (src == null)
            {
                src = display.gameObject.AddComponent<CardDisplayTooltipSource>();
            }
            return src;
        }

        return new ShopCardTooltipSource(vm, transform);
    }

    private class ShopCardTooltipSource : ICardTooltipSource
    {
        private readonly string _title;
        private readonly string _description;
        private readonly Transform _anchor;

        public ShopCardTooltipSource(in ShopSlotVM vm, Transform anchor)
        {
            _title = vm.cardData != null
                ? vm.cardData.GetDisplayName(false)
                : vm.title ?? string.Empty;
            _description = vm.cardData != null
                ? vm.cardData.actionDescription
                : vm.detail ?? string.Empty;
            _anchor = anchor;
        }

        public CardTooltipData GetTooltipData() => new CardTooltipData(_title, _description ?? string.Empty);

        public Vector3 GetTooltipAnchorWorldPos() => _anchor != null ? _anchor.position : Vector3.zero;

        public bool ShouldUseHandOffset => true;

        public bool IsTooltipValid => true;
    }
}

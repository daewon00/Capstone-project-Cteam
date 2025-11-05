using System;
using System.Linq;
using Game.Save;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 선택된 카드의 강화 전/후 상태를 비교하여 표시합니다.
/// </summary>
public class CardUpgradePreviewPanel : MonoBehaviour
{
    [Header("Displays")]
    [SerializeField] private CardDisplay beforeDisplay;
    [SerializeField] private CardDisplay afterDisplay;
    [SerializeField] private CardDisplay displayPrefab;
    [SerializeField] private RectTransform beforeAnchor;
    [SerializeField] private RectTransform afterAnchor;

    [Header("Optional Texts")]
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text guidanceText;
    [SerializeField] private TMP_Text beforeTitleText;
    [SerializeField] private TMP_Text afterTitleText;
    [SerializeField] private TMP_Text centerTitleText;
    [SerializeField] private RectTransform centerTitleContainer;
    [SerializeField] private RectTransform centerAnchor;

    private CardRuntimeState _beforeState;
    private CardRuntimeState _afterState;
    private CardScriptableObject _activeCardData;
    private CardRuntimeState _activeRuntimeState;
    private string _defaultBeforeTitle;
    private string _defaultAfterTitle;
    private string _defaultCenterTitle;
    private TextAlignmentOptions _defaultCenterAlignment;
    private bool _defaultAfterTitleActive = true;
    private bool _defaultCenterTitleActive = true;
    private bool _defaultAfterAnchorActive = true;
    private bool _defaultsCached;
    private bool _slotDefaultsCached;
    private bool _usingCenterSlot;
    private RectTransform _beforeDefaultParent;
    private Vector2 _beforeDefaultAnchorMin;
    private Vector2 _beforeDefaultAnchorMax;
    private Vector2 _beforeDefaultAnchoredPos;
    private Vector2 _beforeDefaultPivot;
    private Vector3 _beforeDefaultLocalScale = Vector3.one;
    private Quaternion _beforeDefaultRotation = Quaternion.identity;
    private bool _beforeAnchorDefaultActive = true;
    private bool _centerAnchorDefaultActive = true;
    private bool _centerTitleDefaultActive = true;

    [Header("Sizing")]
    [SerializeField] private bool useFixedSize = true;
    [SerializeField] private Vector2 cardSize = new Vector2(380f, 420f);
    [SerializeField, Min(0f)] private float uniformScale = 1f;
    [SerializeField] private bool applyInEditor = true;
    [SerializeField] private bool useLayoutElement = false;

    private void Awake()
    {
        TrySpawnDisplays();
        ApplySizing(beforeDisplay);
        ApplySizing(afterDisplay);
        ResolveLabelReferencesIfNeeded();
        CacheLabelDefaults();
        CacheSlotDefaults();
    }

    private void OnValidate()
    {
        if (!applyInEditor) return;
        // Attempt to size any already present displays in editor
        ApplySizing(beforeDisplay);
        ApplySizing(afterDisplay);
    }

    public void Clear()
    {
        CacheSlotDefaults();
        RestoreDefaultSlotLayout();
        _usingCenterSlot = false;
        _activeCardData = null;
        _activeRuntimeState = null;
        _beforeState = null;
        _afterState = null;
        beforeDisplay?.Clear();
        afterDisplay?.Clear();
        if (cardNameText != null) cardNameText.text = string.Empty;
        if (guidanceText != null) guidanceText.text = string.Empty;
        ApplyLabelDefaults();
    }

    public void Show(
        CardScriptableObject cardData,
        CardRuntimeState runtimeState,
        bool showAfterState = true,
        string guidanceOverride = null,
        DeckUpgradeSelectionPanel.CardSelectionConfirmContext context = null)
    {
        TrySpawnDisplays();
        // Ensure sizing is applied after (re)spawn
        ApplySizing(beforeDisplay);
        ApplySizing(afterDisplay);

        if (cardData == null || runtimeState == null)
        {
            Clear();
            return;
        }

        EnsureStates(runtimeState);
        beforeDisplay?.Bind(cardData, _beforeState);
        afterDisplay?.Bind(cardData, _afterState);
        _activeCardData = cardData;
        _activeRuntimeState = runtimeState;
        if (afterDisplay != null)
        {
            afterDisplay.gameObject.SetActive(showAfterState);
        }
        if (afterAnchor != null)
        {
            afterAnchor.gameObject.SetActive(showAfterState);
        }

        if (cardNameText != null)
        {
            cardNameText.text = cardData.GetDisplayName(false);
        }

        if (guidanceText != null)
        {
            if (!string.IsNullOrEmpty(guidanceOverride))
                guidanceText.text = guidanceOverride;
            else
                guidanceText.text = "좌측은 현재, 우측은 강화 후 능력치입니다.";
        }

        ApplyContext(context, showAfterState);
    }

    private void EnsureStates(CardRuntimeState source)
    {
        _beforeState ??= CloneState(source, upgraded: false);
        _afterState ??= CloneState(source, upgraded: true);

        CopyStateValues(source, _beforeState, upgraded: false);
        CopyStateValues(source, _afterState, upgraded: true);
    }

    private static CardRuntimeState CloneState(CardRuntimeState source, bool upgraded)
    {
        if (source == null) return null;
        var clone = new CardRuntimeState
        {
            InstanceId = source.InstanceId,
            RunId = source.RunId,
            CardId = source.CardId,
            Location = source.Location,
            OrderInPile = source.OrderInPile,
            ModifiersJson = source.ModifiersJson
        };
        clone.SetUpgraded(upgraded);
        return clone;
    }

    private static void CopyStateValues(CardRuntimeState source, CardRuntimeState destination, bool upgraded)
    {
        if (source == null || destination == null) return;
        destination.InstanceId = source.InstanceId;
        destination.RunId = source.RunId;
        destination.CardId = source.CardId;
        destination.Location = source.Location;
        destination.OrderInPile = source.OrderInPile;
        destination.ModifiersJson = source.ModifiersJson;
        destination.SetUpgraded(upgraded);
    }

    private void TrySpawnDisplays()
    {
        if (displayPrefab == null)
            return;

        if (beforeDisplay == null && beforeAnchor != null)
        {
            beforeDisplay = Instantiate(displayPrefab, beforeAnchor);
            ResetRect(beforeDisplay.transform as RectTransform);
            ApplySizing(beforeDisplay);
        }

        if (afterDisplay == null && afterAnchor != null)
        {
            afterDisplay = Instantiate(displayPrefab, afterAnchor);
            ResetRect(afterDisplay.transform as RectTransform);
            ApplySizing(afterDisplay);
        }
    }

    private static void ResetRect(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ApplySizing(CardDisplay display)
    {
        if (display == null) return;

        var rect = display.transform as RectTransform;
        if (rect != null)
        {
            if (useFixedSize)
            {
                rect.sizeDelta = cardSize;
            }
            rect.localScale = Vector3.one * Mathf.Max(0f, uniformScale);
        }

        if (useLayoutElement)
        {
            var le = display.GetComponent<LayoutElement>();
            if (le == null) le = display.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = cardSize.x;
            le.preferredHeight = cardSize.y;
        }
        else
        {
            var le = display.GetComponent<LayoutElement>();
            if (le != null)
            {
                // Do not destroy in editor; just neutralize to avoid layout override
                le.preferredWidth = -1;
                le.preferredHeight = -1;
            }
        }
    }

    private void ApplyContext(DeckUpgradeSelectionPanel.CardSelectionConfirmContext context, bool showAfterState)
    {
        ResolveLabelReferencesIfNeeded();
        CacheLabelDefaults();
        CacheSlotDefaults();

        string beforeTitle = context != null && !string.IsNullOrEmpty(context.BeforePreviewTitle)
            ? context.BeforePreviewTitle
            : _defaultBeforeTitle;

        if (beforeTitleText != null)
        {
            beforeTitleText.text = beforeTitle ?? string.Empty;
            beforeTitleText.gameObject.SetActive(!string.IsNullOrEmpty(beforeTitle));
        }

        string afterTitle = context != null && !string.IsNullOrEmpty(context.AfterPreviewTitle)
            ? context.AfterPreviewTitle
            : _defaultAfterTitle;

        bool showAfterTitle = showAfterState && !string.IsNullOrEmpty(afterTitle);
        if (afterTitleText != null)
        {
            afterTitleText.text = showAfterTitle ? afterTitle : string.Empty;
            afterTitleText.gameObject.SetActive(showAfterTitle);
        }

        string centerTitle = context != null && context.CenterPreviewTitle != null
            ? context.CenterPreviewTitle
            : _defaultCenterTitle;

        bool showCenter = context?.ShowCenterPreview ?? _defaultCenterTitleActive;
        bool useCenterSlot = context?.UseCenterSlot ?? false;

        if (!useCenterSlot)
        {
            showCenter = showCenter && !string.IsNullOrEmpty(centerTitle);
        }

        UpdateSlotLayout(useCenterSlot, beforeTitle, ref centerTitle, ref showCenter);

        if (useCenterSlot && beforeDisplay != null && _activeCardData != null && _activeRuntimeState != null)
        {
            beforeDisplay.Bind(_activeCardData, _activeRuntimeState);
        }

        ApplySizing(beforeDisplay);
        if (showAfterState && afterDisplay != null)
        {
            ApplySizing(afterDisplay);
        }

        if (centerTitleText != null)
        {
            centerTitleText.text = centerTitle ?? string.Empty;
            var alignment = context?.CenterPreviewAlignment ?? _defaultCenterAlignment;
            if (useCenterSlot)
            {
                alignment = TextAlignmentOptions.Center;
            }
            centerTitleText.alignment = alignment;
        }
        SetCenterVisible(showCenter);
        SetAfterVisible(showAfterState);
    }

    private void SetAfterVisible(bool visible)
    {
        if (afterDisplay != null) afterDisplay.gameObject.SetActive(visible);
        if (afterAnchor != null) afterAnchor.gameObject.SetActive(visible);
        if (afterTitleText != null) afterTitleText.gameObject.SetActive(visible && !string.IsNullOrEmpty(afterTitleText.text));
    }

    private void SetCenterVisible(bool visible)
    {
        if (centerTitleText != null) centerTitleText.gameObject.SetActive(visible && !string.IsNullOrEmpty(centerTitleText.text));
        if (centerTitleContainer != null) centerTitleContainer.gameObject.SetActive(visible && (centerTitleText == null || centerTitleText.gameObject.activeSelf));
    }

    private void ResolveLabelReferencesIfNeeded()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        if (beforeTitleText == null)
        {
            beforeTitleText = texts.FirstOrDefault(t => !string.IsNullOrEmpty(t.text) && t.text.Contains("강화 전"));
        }
        if (afterTitleText == null)
        {
            afterTitleText = texts.FirstOrDefault(t => !string.IsNullOrEmpty(t.text) && t.text.Contains("강화 후"));
        }
        if (centerTitleText == null)
        {
            centerTitleText = texts.FirstOrDefault(t =>
            {
                var raw = t.text?.Trim();
                return !string.IsNullOrEmpty(raw) && (string.Equals(raw, "X", StringComparison.OrdinalIgnoreCase) || raw == "→" || raw == ">");
            });
        }
        if (centerTitleContainer == null && centerTitleText != null)
        {
            centerTitleContainer = centerTitleText.rectTransform;
        }
    }

    private void CacheLabelDefaults()
    {
        if (_defaultsCached)
            return;

        _defaultBeforeTitle = beforeTitleText != null ? beforeTitleText.text : null;
        _defaultAfterTitle = afterTitleText != null ? afterTitleText.text : null;
        _defaultCenterTitle = centerTitleText != null ? centerTitleText.text : null;
        _defaultCenterAlignment = centerTitleText != null ? centerTitleText.alignment : TextAlignmentOptions.Center;
        _defaultAfterTitleActive = afterTitleText != null ? afterTitleText.gameObject.activeSelf : true;
        _defaultCenterTitleActive = centerTitleText != null ? centerTitleText.gameObject.activeSelf : true;
        _defaultAfterAnchorActive = afterAnchor != null ? afterAnchor.gameObject.activeSelf : true;
        _centerTitleDefaultActive = centerTitleContainer != null ? centerTitleContainer.gameObject.activeSelf : _defaultCenterTitleActive;
        _defaultsCached = true;
    }

    private void ApplyLabelDefaults()
    {
        ResolveLabelReferencesIfNeeded();
        CacheLabelDefaults();
        CacheSlotDefaults();
        RestoreDefaultSlotLayout();
        _usingCenterSlot = false;

        if (beforeTitleText != null)
        {
            beforeTitleText.text = _defaultBeforeTitle ?? beforeTitleText.text;
            beforeTitleText.gameObject.SetActive(!string.IsNullOrEmpty(beforeTitleText.text));
        }

        if (afterTitleText != null)
        {
            afterTitleText.text = _defaultAfterTitle ?? string.Empty;
            afterTitleText.gameObject.SetActive(_defaultAfterTitleActive);
        }

        ApplySizing(beforeDisplay);
        if (afterDisplay != null)
        {
            ApplySizing(afterDisplay);
        }

        if (centerTitleText != null)
        {
            centerTitleText.text = _defaultCenterTitle ?? centerTitleText.text;
            centerTitleText.alignment = _defaultCenterAlignment;
            centerTitleText.gameObject.SetActive(_defaultCenterTitleActive && !string.IsNullOrEmpty(centerTitleText.text));
        }

        SetAfterVisible(_defaultAfterAnchorActive);
        SetCenterVisible(_centerTitleDefaultActive);
    }

    private void CacheSlotDefaults()
    {
        if (_slotDefaultsCached)
            return;

        var beforeRect = beforeDisplay != null ? beforeDisplay.transform as RectTransform : null;
        if (beforeRect != null)
        {
            _beforeDefaultParent = beforeRect.parent as RectTransform;
            _beforeDefaultAnchorMin = beforeRect.anchorMin;
            _beforeDefaultAnchorMax = beforeRect.anchorMax;
            _beforeDefaultAnchoredPos = beforeRect.anchoredPosition;
            _beforeDefaultPivot = beforeRect.pivot;
            _beforeDefaultLocalScale = beforeRect.localScale;
            _beforeDefaultRotation = beforeRect.localRotation;
        }

        if (beforeAnchor != null)
        {
            _beforeAnchorDefaultActive = beforeAnchor.gameObject.activeSelf;
        }

        if (centerAnchor != null)
        {
            _centerAnchorDefaultActive = centerAnchor.gameObject.activeSelf;
        }

        _slotDefaultsCached = true;
    }

    private void UpdateSlotLayout(bool useCenterSlot, string beforeTitle, ref string centerTitle, ref bool showCenter)
    {
        CacheSlotDefaults();

        if (useCenterSlot && centerAnchor != null && beforeDisplay != null)
        {
            var beforeRect = beforeDisplay.transform as RectTransform;
            if (beforeRect != null)
            {
                beforeRect.SetParent(centerAnchor, false);
                beforeRect.anchorMin = new Vector2(0.5f, 0.5f);
                beforeRect.anchorMax = new Vector2(0.5f, 0.5f);
                beforeRect.pivot = new Vector2(0.5f, 0.5f);
                beforeRect.anchoredPosition = Vector2.zero;
                beforeRect.localScale = _beforeDefaultLocalScale;
                beforeRect.localRotation = _beforeDefaultRotation;
            }

            if (beforeAnchor != null)
            {
                beforeAnchor.gameObject.SetActive(false);
            }

            if (centerAnchor != null)
            {
                centerAnchor.gameObject.SetActive(true);
            }

            if (beforeTitleText != null)
            {
                beforeTitleText.gameObject.SetActive(false);
            }

            if (string.IsNullOrEmpty(centerTitle))
            {
                centerTitle = beforeTitle;
            }

            showCenter = !string.IsNullOrEmpty(centerTitle);
            _usingCenterSlot = true;
        }
        else
        {
            if (_usingCenterSlot)
            {
                RestoreDefaultSlotLayout();
            }

            _usingCenterSlot = false;

            if (beforeTitleText != null)
            {
                bool shouldShowBefore = !string.IsNullOrEmpty(beforeTitle);
                beforeTitleText.gameObject.SetActive(shouldShowBefore);
            }
        }
    }

    private void RestoreDefaultSlotLayout()
    {
        if (!_slotDefaultsCached)
            return;

        var beforeRect = beforeDisplay != null ? beforeDisplay.transform as RectTransform : null;
        if (beforeRect != null && _beforeDefaultParent != null)
        {
            beforeRect.SetParent(_beforeDefaultParent, false);
            beforeRect.anchorMin = _beforeDefaultAnchorMin;
            beforeRect.anchorMax = _beforeDefaultAnchorMax;
            beforeRect.pivot = _beforeDefaultPivot;
            beforeRect.anchoredPosition = _beforeDefaultAnchoredPos;
            beforeRect.localScale = _beforeDefaultLocalScale;
            beforeRect.localRotation = _beforeDefaultRotation;
        }

        if (beforeAnchor != null)
        {
            beforeAnchor.gameObject.SetActive(_beforeAnchorDefaultActive);
        }

        if (centerAnchor != null)
        {
            centerAnchor.gameObject.SetActive(_centerAnchorDefaultActive);
        }
    }
}

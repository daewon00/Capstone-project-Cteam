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

    private CardRuntimeState _beforeState;
    private CardRuntimeState _afterState;

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
        _beforeState = null;
        _afterState = null;
        beforeDisplay?.Clear();
        afterDisplay?.Clear();
        if (cardNameText != null) cardNameText.text = string.Empty;
        if (guidanceText != null) guidanceText.text = string.Empty;
    }

    public void Show(CardScriptableObject cardData, CardRuntimeState runtimeState)
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

        if (cardNameText != null)
        {
            cardNameText.text = cardData.GetDisplayName(false);
        }

        if (guidanceText != null)
        {
            guidanceText.text = "좌측은 현재, 우측은 강화 후 능력치입니다.";
        }
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
}

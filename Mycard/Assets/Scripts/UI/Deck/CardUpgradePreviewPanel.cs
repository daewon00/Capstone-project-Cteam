using Game.Save;
using UnityEngine;
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

    private void Awake()
    {
        TrySpawnDisplays();
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
        }

        if (afterDisplay == null && afterAnchor != null)
        {
            afterDisplay = Instantiate(displayPrefab, afterAnchor);
            ResetRect(afterDisplay.transform as RectTransform);
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
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save; // CardRuntimeState

// 역할: 카드 한 장의 UI를 제어하고, 데이터를 받아서 내용을 채워넣는다.
[DisallowMultipleComponent]
public class DeckCardItemView : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image cardIconImage;
    [SerializeField] private GameObject countBadge;   // (선택사항) 카드 매수 표시용
    [SerializeField] private TMP_Text countText;      // (선택사항)

    // 바인딩에 사용된 최근 데이터(디버깅/툴팁용)
    public string BoundInstanceId { get; private set; }
    public string BoundCardId { get; private set; }

    public void Clear()
    {
        BoundInstanceId = null;
        BoundCardId = null;
        if (cardNameText != null) cardNameText.text = string.Empty;
        if (costText != null) costText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (cardIconImage != null) cardIconImage.sprite = null;
        if (countBadge != null) countBadge.SetActive(false);
    }

    /// <summary>
    /// 카드 데이터를 받아서 UI에 내용을 채워넣는 메인 함수.
    /// CardScriptableObject와 현재 런타임 상태를 기반으로 표시 요소를 채웁니다.
    /// </summary>
    public void Bind(CardScriptableObject cardSO, CardRuntimeState cardState, int? groupedCount = null)
    {
        BoundInstanceId = cardState?.InstanceId ?? string.Empty;
        BoundCardId = cardSO?.CardId ?? cardState?.CardId ?? string.Empty;

        if (cardSO != null)
        {
            if (cardNameText != null)
                cardNameText.text = cardSO.cardName;

            if (costText != null)
                costText.text = cardSO.manaCost.ToString();

            if (descriptionText != null)
                descriptionText.text = cardSO.actionDescription;

            if (cardIconImage != null)
                cardIconImage.sprite = cardSO.characterSprite != null ? cardSO.characterSprite : cardSO.bgSprite;
        }
        else
        {
            if (cardNameText != null) cardNameText.text = BoundCardId;
            if (costText != null) costText.text = string.Empty;
            if (descriptionText != null) descriptionText.text = string.Empty;
        }

        // (선택사항) 카드 매수 표시 로직
        if (countBadge != null)
        {
            if (groupedCount.HasValue && groupedCount.Value > 1)
            {
                countBadge.SetActive(true);
                if (countText != null) countText.text = $"x{groupedCount.Value}";
            }
            else
            {
                countBadge.SetActive(false);
            }
        }
    }
}


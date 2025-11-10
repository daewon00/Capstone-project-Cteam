using UnityEngine;
using TMPro;

/// <summary>
/// 덱 서비스에서 더미 카드 수 변화를 받아 상단 카운터를 갱신합니다.
/// </summary>
public class CountersUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _drawPileCountText;
    [SerializeField] private TextMeshProUGUI _discardPileCountText;

    private IDeckService _deckService;

    private void OnEnable()
    {
        try
        {
            _deckService = ServiceRegistry.GetRequired<IDeckService>();
            _deckService.OnPileCountsChanged += UpdateCounters;
            UpdateCounters(_deckService.GetPileCounts());
        }
        catch (System.Exception e)
        {
            GameLog.Warn($"[CountersUI] IDeckService를 찾을 수 없어 UI를 비활성화합니다: {e.Message}");
            this.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (_deckService != null)
        {
            _deckService.OnPileCountsChanged -= UpdateCounters;
        }
    }

    /// <summary>
    /// 덱 서비스에서 전달한 더미 카드 수를 텍스트에 반영합니다.
    /// </summary>
    private void UpdateCounters(PileCounts counts)
    {
        if (_drawPileCountText != null)
            _drawPileCountText.text = counts.Draw.ToString();

        if (_discardPileCountText != null)
            _discardPileCountText.text = counts.Discard.ToString();
    }
}


using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 맵 상단에 골드 수치를 표시하고 지갑 서비스 변화를 반영하는 UI 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class TopBarUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;

    private IWalletService _wallet;

    void OnEnable()
    {
        // 1) 즉시 바인딩 시도
        _wallet = ServiceRegistry.Get<IWalletService>();

        if (_wallet == null)
        {
            // 2) 아직 초기화 전이면 한 프레임 뒤 재시도
            StartCoroutine(DeferredBind());
            return;
        }

        // 3) 준비되었다면 즉시 동기화 및 구독
        UpdateGoldText(_wallet.Gold);
        _wallet.OnGoldChanged += UpdateGoldText;
    }

    void OnDisable()
    {
        if (_wallet != null)
        {
            _wallet.OnGoldChanged -= UpdateGoldText;
            _wallet = null;
        }
    }

    private IEnumerator DeferredBind()
    {
        yield return null; // 한 프레임 대기

        _wallet = ServiceRegistry.Get<IWalletService>();
        if (_wallet == null)
        {
            Debug.LogWarning("[TopBarUI] IWalletService를 찾을 수 없습니다. 골드 UI가 비어있을 수 있습니다.");
            yield break;
        }

        UpdateGoldText(_wallet.Gold);
        _wallet.OnGoldChanged += UpdateGoldText;
    }

    private void UpdateGoldText(int newGold)
    {
        if (goldText != null)
            goldText.text = $"{newGold:N0}";
    }
}


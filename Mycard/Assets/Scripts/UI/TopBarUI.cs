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
    [SerializeField] private RollingNumberAnimator goldAnimator = new RollingNumberAnimator();

    private IWalletService _wallet;
    private Coroutine _deferredBindRoutine;

    void OnEnable()
    {
        if (goldAnimator == null)
        {
            goldAnimator = new RollingNumberAnimator();
        }
        goldAnimator.Bind(this, goldText);

        if (TryBindWallet()) return;

        _deferredBindRoutine = StartCoroutine(DeferredBind());
    }

    void OnDisable()
    {
        if (_deferredBindRoutine != null)
        {
            StopCoroutine(_deferredBindRoutine);
            _deferredBindRoutine = null;
        }

        if (_wallet != null)
        {
            _wallet.OnGoldChanged -= HandleGoldChanged;
            _wallet = null;
        }

        goldAnimator.Unbind();
    }

    private IEnumerator DeferredBind()
    {
        yield return null; // 한 프레임 대기
        _deferredBindRoutine = null;

        if (!TryBindWallet())
        {
            GameLog.Warn("[TopBarUI] IWalletService를 찾을 수 없습니다. 골드 UI가 비어있을 수 있습니다.");
        }
    }

    private bool TryBindWallet()
    {
        _wallet = ServiceRegistry.Get<IWalletService>();
        if (_wallet == null) return false;

        goldAnimator.SetInstant(_wallet.Gold);
        _wallet.OnGoldChanged += HandleGoldChanged;
        return true;
    }

    private void HandleGoldChanged(int newGold)
    {
        goldAnimator.AnimateTo(newGold);
    }
}

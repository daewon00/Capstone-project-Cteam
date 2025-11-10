using System.Collections;
using UnityEngine;

public partial class ShopUI : MonoBehaviour
{
    
    // ==========================================================
    // 7. UI 공개 API (Public API)
    // - 외부(예: ShopOverlayController)에서 이 UI를 제어하기 위한 공식적인 명령입니다.
    // - Open과 Close는 '화면 표시'와 관련된 핵심 기능이므로, 이 파일에 있는 것이 적절합니다.
    // ==========================================================

    /// <summary>
    /// 상점 UI를 화면에 표시합니다.
    /// </summary>
    public void Open()
    {
        // 이미 열려있으면 아무것도 안 함
        if (_isOpen) return;


        // UI 애니메이션을 위한 준비
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        panel.blocksRaycasts = false;
        panel.interactable = false;
        panel.alpha = 0f;
        if (window) window.localScale = Vector3.one * ScaleFrom;


        // '방문중' (_sessionInitialized)을 확인합니다.
        // 만약 이번 노드 방문에서 처음 여는 것이라면, (방문중이 아니라면)
        if (!_sessionInitialized)
        {
            // "방문중" 팻말을 세웁니다. 이제 방문중에는 이 코드가 실행되지 않습니다.
            _sessionInitialized = true;

            // --- 상점의 상태를 초기화하고 상품을 처음 진열하는 코드는 모두 이 안으로 들어옵니다 ---
            _rerollCount = 0;

            BuildDummySlots();
            BuildCardSlotsInitial();
            ApplyDeals();

            var displayItems = new System.Text.StringBuilder();
            foreach (var vm in _dummy)
            {
                displayItems.Append(vm.title + ", ");
            }
            GameLog.Info($"<color=cyan>[CCTV] 화면 표시용 아이템 목록:</color> {displayItems}", this);
        }

        // --- 최종 화면 갱신 ---
        // 결정된 상품 목록(_dummy)을 기반으로 실제 UI 요소들을 만들고(RebuildGrid),
        // 골드와 리롤 가격을 갱신합니다(RefreshTopbar).
        RebuildGrid();
        RefreshTopbar();

        // UI를 여는 애니메이션 실행
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateOpen());
        _isOpen = true;
    }

    /// <summary>
    /// 상점 UI를 화면에서 닫습니다.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return; // 이미 닫혀있으면 중복 실행 방지
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateClose());
        _isOpen = false;
    }



    // ==========================================================
    // 8. UI 애니메이션 및 내부 처리 (Private Implementation)
    // - UI를 열고 닫는 세부적인 애니메이션과 상태 처리를 담당합니다.
    // - 이 기능들은 오직 이 파일 내부에서만 사용되며, 응집도가 높습니다.
    // ==========================================================

    /// <summary>
    /// UI를 즉시 숨깁니다. (Awake에서 초기 상태 설정용)
    /// </summary>
    private void HideImmediate()
    {
        if (panel == null) panel = GetComponent<CanvasGroup>();
        gameObject.SetActive(false);
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        if (window) window.localScale = Vector3.one * ScaleFrom;
    }

    /// <summary>
    /// UI를 부드럽게 나타나게 하는 애니메이션 코루틴입니다.
    /// </summary>
    private IEnumerator AnimateOpen()
    {
        float t = 0f;
        while (t < OpenDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / OpenDur);

            panel.alpha = u;
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
    /// <summary>
    /// UI를 부드럽게 사라지게 하는 애니메이션 코루틴입니다.
    /// </summary>
    private IEnumerator AnimateClose()
    {
        panel.blocksRaycasts = false;
        panel.interactable = false;

        float startAlpha = panel.alpha;
        float t = 0f;
        while (t < CloseDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / CloseDur);

            panel.alpha = Mathf.Lerp(startAlpha, 0f, u);
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
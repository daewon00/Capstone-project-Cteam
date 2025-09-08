using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 필드 위에 카드 놓일 자리 에 대한 정보
public class CardPlacePoint : MonoBehaviour
{
    //public static CardPlacePoint instance;

    public Card activeCard; // 놓여있는 카드 정보
    public bool isPlayerPoint; // 플레이어 영역 참 거짓
    public Transform cameraFocusPoint; // 카메라 포커스 변수

    // =============== 드롭 하이라이트 (신호등) ===============
    public enum HighlightState { Off, Allowed, Blocked }

    [Header("Highlight (optional)")]
    [SerializeField] private GameObject highlightEffect; // 프리팹 자식의 하이라이트 오브젝트
    [SerializeField] private Color allowedColor = new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private HighlightVisual highlightVisual; // 신규 하이라이트 시스템(있으면 우선 사용)

    private HighlightState _highlightState = HighlightState.Off;
    private Renderer _highlightRenderer;

    void Awake()
    {
        if (highlightEffect != null)
            _highlightRenderer = highlightEffect.GetComponentInChildren<Renderer>(true);

        // 신규 하이라이트 시각화 컴포넌트 자동 탐색(있으면 위임)
        if (highlightVisual == null)
        {
            if (highlightEffect != null)
                highlightVisual = highlightEffect.GetComponentInChildren<HighlightVisual>(true);
            if (highlightVisual == null)
                highlightVisual = GetComponentInChildren<HighlightVisual>(true);
        }
        // 기본은 꺼둔다
        ApplyState(HighlightState.Off);
    }

    public void SetHighlightState(HighlightState state)
    {
        if (_highlightState == state) return; // 중복 호출 방지
        ApplyState(state);
    }

    private void ApplyState(HighlightState state)
    {
        _highlightState = state;
        // On/Off 가시성은 기존 오브젝트 토글 유지(레거시 호환)
        if (highlightEffect != null)
        {
            switch (state)
            {
                case HighlightState.Off:
                    if (highlightEffect.activeSelf) highlightEffect.SetActive(false);
                    break;
                case HighlightState.Allowed:
                case HighlightState.Blocked:
                    if (!highlightEffect.activeSelf) highlightEffect.SetActive(true);
                    break;
            }
        }

        // 신규 시스템이 있으면 우선 사용
        if (highlightVisual != null)
        {
            var mapped = HighlightProfile.HighlightStateType.Off;
            if (state == HighlightState.Allowed) mapped = HighlightProfile.HighlightStateType.Allowed;
            else if (state == HighlightState.Blocked) mapped = HighlightProfile.HighlightStateType.Blocked;
            highlightVisual.SetState(mapped);
            return; // 신규 경로 사용 시 레거시 색 지정은 생략
        }

        // 폴백: 레거시 색상 지정
        if (highlightEffect == null)
            return;

        switch (state)
        {
            case HighlightState.Allowed:
                TrySetColor(allowedColor);
                break;
            case HighlightState.Blocked:
                TrySetColor(blockedColor);
                break;
        }
    }

    private void TrySetColor(Color c)
    {
        if (_highlightRenderer == null) return;
        // 머티리얼 인스턴스 안전하게 색상 반영
        var mat = _highlightRenderer.material;
        if (mat.HasProperty("_Color")) mat.color = c;
    }
}

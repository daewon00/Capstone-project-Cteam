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

    private HighlightState _highlightState = HighlightState.Off;
    private Renderer _highlightRenderer;

    void Awake()
    {
        if (highlightEffect != null)
            _highlightRenderer = highlightEffect.GetComponentInChildren<Renderer>(true);
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
        if (highlightEffect == null)
            return;

        switch (state)
        {
            case HighlightState.Off:
                if (highlightEffect.activeSelf) highlightEffect.SetActive(false);
                break;
            case HighlightState.Allowed:
                if (!highlightEffect.activeSelf) highlightEffect.SetActive(true);
                TrySetColor(allowedColor);
                break;
            case HighlightState.Blocked:
                if (!highlightEffect.activeSelf) highlightEffect.SetActive(true);
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

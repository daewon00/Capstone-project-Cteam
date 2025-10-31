using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Companion Select 씬에서 확인 버튼 클릭 시 연출(애니메이션, SFX)을 재생한 뒤 호출자에게 완료 콜백을 전달합니다.
/// </summary>
public sealed class CompanionSelectFxController : MonoBehaviour
{
    [Header("FX References")]
    [SerializeField] private CanvasGroup cardHighlightCanvas;
    [SerializeField] private RectTransform fxPivot;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip confirmSfx;

    [Header("Buttons (optional disable during FX)")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button secondaryButton;

    [Header("Timing")]
    [Tooltip("애니메이션 + 오디오가 끝날 때까지 기다릴 시간(초)")]
    [SerializeField, Range(0.1f, 2f)] private float sequenceDuration = 0.45f;
    [SerializeField, Range(0f, 0.3f)] private float buttonScaleAmplitude = 0.08f;
    [SerializeField, Range(0f, 0.3f)] private float pivotScaleAmplitude = 0.05f;
    [SerializeField, Range(0f, 1f)] private float highlightMinAlpha = 0.3f;

    private bool _isPlaying;
    private Vector3 _startButtonBaseScale = Vector3.one;
    private Vector3 _pivotBaseScale = Vector3.one;
    private float _cardBaseAlpha = 1f;

    private RectTransform StartButtonRect => startButton != null ? startButton.transform as RectTransform : null;

    private void Awake()
    {
        var startRect = StartButtonRect;
        if (startRect != null)
        {
            _startButtonBaseScale = startRect.localScale;
        }
        if (fxPivot != null)
        {
            _pivotBaseScale = fxPivot.localScale;
        }
        if (cardHighlightCanvas != null)
        {
            _cardBaseAlpha = cardHighlightCanvas.alpha;
        }
    }

    /// <summary>
    /// 확인 연출을 재생하고 완료되면 onCompleted를 호출합니다.
    /// </summary>
    public void PlayConfirmFX(Action onCompleted)
    {
        if (_isPlaying)
        {
            return;
        }

        StartCoroutine(PlayRoutine(onCompleted));
    }

    private IEnumerator PlayRoutine(Action onCompleted)
    {
        _isPlaying = true;

        bool startButtonInteractable = startButton != null && startButton.interactable;
        bool secondaryInteractable = secondaryButton != null && secondaryButton.interactable;

        if (startButton != null) startButton.interactable = false;
        if (secondaryButton != null) secondaryButton.interactable = false;

        if (sfxSource != null && confirmSfx != null)
        {
            sfxSource.PlayOneShot(confirmSfx);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, sequenceDuration);
        var startRect = StartButtonRect;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI); // 0 -> 1 -> 0

            if (startRect != null)
            {
                float scaleFactor = 1f + buttonScaleAmplitude * pulse;
                startRect.localScale = _startButtonBaseScale * scaleFactor;
            }

            if (fxPivot != null)
            {
                float scaleFactor = 1f + pivotScaleAmplitude * pulse;
                fxPivot.localScale = _pivotBaseScale * scaleFactor;
            }

            if (cardHighlightCanvas != null)
            {
                float targetAlpha = Mathf.Lerp(_cardBaseAlpha, highlightMinAlpha, pulse);
                cardHighlightCanvas.alpha = targetAlpha;
            }

            yield return null;
        }

        if (startRect != null)
        {
            startRect.localScale = _startButtonBaseScale;
        }
        if (fxPivot != null)
        {
            fxPivot.localScale = _pivotBaseScale;
        }
        if (cardHighlightCanvas != null)
        {
            cardHighlightCanvas.alpha = _cardBaseAlpha;
        }

        if (startButton != null) startButton.interactable = startButtonInteractable;
        if (secondaryButton != null) secondaryButton.interactable = secondaryInteractable;

        _isPlaying = false;
        onCompleted?.Invoke();
    }
}

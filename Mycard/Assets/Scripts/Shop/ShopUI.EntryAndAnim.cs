using System.Collections;
using UnityEngine;

public partial class ShopUI : MonoBehaviour
{
    

    public void Open()
    {
        if (_isOpen) return;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        panel.blocksRaycasts = false;
        panel.interactable = false;
        panel.alpha = 0f;
        if (window) window.localScale = Vector3.one * ScaleFrom;

        _rerollCount = 0;
        Gold = testGold;

        BuildDummySlots();
        BuildCardSlotsInitial();
        ApplyDeals();
        RebuildGrid();
        RefreshTopbar();

        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateOpen());
        _isOpen = true;
    }

    public void Close()
    {
        if (!_isOpen) return;
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(AnimateClose());
        _isOpen = false;
    }

    private void HideImmediate()
    {
        if (panel == null) panel = GetComponent<CanvasGroup>();
        gameObject.SetActive(false);
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        if (window) window.localScale = Vector3.one * ScaleFrom;
    }

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
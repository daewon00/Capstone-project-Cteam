using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardTooltipTriggerUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private MonoBehaviour sourceBehaviour;
    [SerializeField, Tooltip("길게 누른 후 툴팁이 나타나기까지의 지연 시간(초)")]
    private float pressDelay = 0.25f;

    private ICardTooltipSource _source;
    private Coroutine _showRoutine;
    private bool _tooltipShown;

    private void Awake()
    {
        if (sourceBehaviour != null)
            _source = sourceBehaviour as ICardTooltipSource;
        if (_source == null)
            _source = GetComponent<ICardTooltipSource>();
    }

    public void SetSource(ICardTooltipSource source)
    {
        _source = source;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_source == null || !_source.IsTooltipValid)
            return;

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        CancelTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelTooltip();
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(pressDelay);

        if (_source != null && _source.IsTooltipValid)
        {
            var svc = ServiceRegistry.Get<ICardTooltipService>();
            svc?.Show(_source);
            _tooltipShown = true;
        }
        _showRoutine = null;
    }

    private void CancelTooltip()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        if (_tooltipShown)
        {
            ServiceRegistry.Get<ICardTooltipService>()?.Hide(_source);
            _tooltipShown = false;
        }
    }

    private void OnDisable()
    {
        CancelTooltip();
    }
}

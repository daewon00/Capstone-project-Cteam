using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardTooltipTriggerUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private MonoBehaviour sourceBehaviour;
    [SerializeField, Tooltip("길게 누른 후 툴팁이 나타나기까지의 지연 시간(초)")]
    private float pressDelay = 0.25f;
    [SerializeField, Tooltip("툴팁 입력 디버그 로그 출력 여부")]
    private bool debugLogging = true;

    private ICardTooltipSource _source;
    private Coroutine _showRoutine;
    private bool _tooltipShown;
    private string _sourceName;

    private void Awake()
    {
        if (sourceBehaviour != null)
            _source = sourceBehaviour as ICardTooltipSource;
        if (_source == null)
            _source = GetComponent<ICardTooltipSource>();

        Log("Awake completed.");
    }

    public void SetSource(ICardTooltipSource source)
    {
        _source = source;
        _sourceName = source != null ? source.ToString() : "null";
        Log($"SetSource -> {_sourceName}");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Log($"OnPointerDown pointer={eventData?.pointerId ?? -1} sourceNull={_source == null}");

        if (_source == null)
        {
            Log("OnPointerDown aborted: _source is null");
            return;
        }

        if (!_source.IsTooltipValid)
        {
            Log($"OnPointerDown ignored: source invalid (valid={_source.IsTooltipValid}, active={isActiveAndEnabled})");
            return;
        }

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Log($"OnPointerUp pointer={eventData?.pointerId ?? -1}");
        CancelTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Log($"OnPointerExit pointer={eventData?.pointerId ?? -1}");
        CancelTooltip();
    }

    private IEnumerator ShowAfterDelay()
    {
        Log($"ShowAfterDelay started delay={pressDelay}");
        yield return new WaitForSeconds(pressDelay);

        if (_source != null && _source.IsTooltipValid)
        {
            var svc = ServiceRegistry.Get<ICardTooltipService>();
            svc?.Show(_source);
            _tooltipShown = true;
            Log("Tooltip shown via service");
        }
        else
        {
            Log("ShowAfterDelay aborted: source missing or invalid");
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
            Log("Tooltip hide requested");
        }
    }

    private void OnDisable()
    {
        Log("OnDisable -> cancelling tooltip");
        CancelTooltip();
    }

    public void SetDebugLogging(bool enabled)
    {
        debugLogging = enabled;
    }

    private void Log(string message)
    {
        if (!debugLogging)
            return;

        UnityEngine.Debug.Log($"[CardTooltipTriggerUI] {message} (obj={name})", this);
    }
}

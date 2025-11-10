using System;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CardDisplay))]
public class CardDisplaySelectionItem : MonoBehaviour, IPointerClickHandler
{
    private CardDisplay _cardDisplay;
    private Button _button;
    private Graphic _raycastGraphic;
    private Vector3 _baseScale = Vector3.one;

    public event Action<CardDisplaySelectionItem> Clicked;

    public CardRuntimeState RuntimeState { get; private set; }
    public CardScriptableObject CardData { get; private set; }

    private void Awake()
    {
        _cardDisplay = GetComponent<CardDisplay>();
        if (_cardDisplay == null)
        {
            GameLog.Error("[CardDisplaySelectionItem] CardDisplay 컴포넌트를 찾을 수 없습니다.", this);
        }

        _button = GetComponent<Button>();
        if (_button == null)
        {
            _button = gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
        }

        EnsureRaycastGraphic();
        _button.onClick.AddListener(HandleClick);
        _baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        SetSelected(false, true);
        GameLog.Info($"[CardDisplaySelectionItem] Awake on {gameObject.name}", this);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Bind(CardScriptableObject cardData, CardRuntimeState runtimeState)
    {
        CardData = cardData;
        RuntimeState = runtimeState;
        if (_cardDisplay != null)
        {
            _cardDisplay.Bind(cardData, runtimeState);
        }
        SetSelected(false, true);
    }

    public void SetSelected(bool selected, bool force = false)
    {
        float targetScale = selected ? 1.05f : 1f;
        transform.localScale = _baseScale * targetScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick();
    }

    private void HandleClick()
    {
        GameLog.Info($"[CardDisplaySelectionItem] Clicked instance={(RuntimeState != null ? RuntimeState.InstanceId : "null")}", this);
        Clicked?.Invoke(this);
    }

    private void EnsureRaycastGraphic()
    {
        _raycastGraphic = GetComponent<Graphic>();
        if (_raycastGraphic == null)
        {
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            _raycastGraphic = image;
        }
        else
        {
            _raycastGraphic.raycastTarget = true;
        }

        if (_button != null && _button.targetGraphic == null)
        {
            _button.targetGraphic = _raycastGraphic;
        }
    }
}

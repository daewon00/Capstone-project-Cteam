using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 동료 카드(CompanionDetailView)에서 발생하는 드래그 입력을 캐러셀 프레젠터로 전달합니다.
/// </summary>
public sealed class CompanionCarouselDragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CompanionCarouselPresenter _presenter;

    public void Initialize(CompanionCarouselPresenter presenter)
    {
        _presenter = presenter;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _presenter?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _presenter?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _presenter?.OnEndDrag(eventData);
    }
}

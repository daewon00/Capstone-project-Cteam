using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// RawImage UV를 이동시켜 배경 이미지를 드래그로 스크롤하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BackGroundScroller : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("스크롤 속도를 조절합니다.")]
    public float scrollSpeed = 0.5f;

    // UI RawImage 컴포넌트를 담을 변수
    private RawImage backgroundImage;
    // 드래그 시작 시의 마우스 위치
    private Vector2 dragStartPosition;

    void Awake()
    {
        // 이 스크립트가 붙어있는 오브젝트의 RawImage 컴포넌트를 가져옵니다.
        backgroundImage = GetComponent<RawImage>();
    }

    // 드래그가 시작될 때 호출됩니다.
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 현재 마우스 위치를 기록합니다.
        dragStartPosition = eventData.position;
    }

    // 드래그 중 매 프레임 호출됩니다.
    public void OnDrag(PointerEventData eventData)
    {
        // 시작 위치와 현재 위치의 차이를 계산합니다.
        Vector2 delta = eventData.position - dragStartPosition;

        // y축 이동값을 계산하고 화면 높이로 정규화해 화면 크기와 무관하게 일정한 속도로 이동시킵니다.
        float moveY = delta.y / Screen.height * scrollSpeed;

        // RawImage의 uvRect를 조절하여 텍스처를 이동시킵니다.
        Rect currentRect = backgroundImage.uvRect;
        currentRect.y += moveY;
        backgroundImage.uvRect = currentRect;

        // 다음 프레임을 위해 현재 위치를 시작 위치로 갱신합니다.
        dragStartPosition = eventData.position;
    }

    // 드래그가 끝났을 때 호출됩니다.
    public void OnEndDrag(PointerEventData eventData)
    {
        // 필요 시 드래그 종료 시점 로직을 여기에 추가할 수 있습니다.
    }
}

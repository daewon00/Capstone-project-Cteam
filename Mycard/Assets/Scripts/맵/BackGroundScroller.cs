using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


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

    // 드래그가 시작될 때 호출되는 함수
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 현재 마우스 위치를 기록합니다.
        dragStartPosition = eventData.position;
    }

    // 드래그하는 동안 계속해서 호출되는 함수
    public void OnDrag(PointerEventData eventData)
    {
        // 시작 위치와 현재 위치의 차이를 계산합니다.
        Vector2 delta = eventData.position - dragStartPosition;

        // y축(세로)으로 얼마나 움직였는지 계산합니다.
        // 화면 높이로 나눠주어, 화면 크기와 상관없이 일정한 속도로 움직이게 합니다.
        float moveY = delta.y / Screen.height * scrollSpeed;

        // RawImage의 uvRect를 조절하여 텍스처를 움직입니다.
        // 이것이 바로 배경이 스크롤되는 것처럼 보이게 하는 핵심 로직입니다.
        Rect currentRect = backgroundImage.uvRect;
        currentRect.y += moveY;
        backgroundImage.uvRect = currentRect;

        // 다음 계산을 위해 현재 위치를 다시 시작 위치로 업데이트합니다.
        dragStartPosition = eventData.position;
    }

    // 드래그가 끝났을 때 호출되는 함수 (지금은 비워둡니다)
    public void OnEndDrag(PointerEventData eventData)
    {
        // 필요하다면 드래그가 끝났을 때의 로직을 여기에 추가할 수 있습니다.
    }
}

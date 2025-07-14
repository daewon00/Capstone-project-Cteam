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
}

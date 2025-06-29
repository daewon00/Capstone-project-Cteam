using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardPlacePoint : MonoBehaviour
{
    public static CardPlacePoint instance;

    public Card activeCard;
    public bool isPlayerPoint;
    public Transform cameraFocusPoint;
}

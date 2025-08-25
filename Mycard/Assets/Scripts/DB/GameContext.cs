using UnityEngine;
using System;

public class GameContext : MonoBehaviour
{
    public static GameContext I { get; private set; }

    [Header("Session")]
    public string ProfileId = "P1"; // 임시 기본값
    public string RunId;
    public string SelectedCompanionId; // "WARRIOR" 처럼 저장

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);
    }
}
using System;

/// <summary>
/// 이벤트 HUD에 표시할 런 상태(체력/골드 등)를 전달하기 위한 스냅샷입니다.
/// </summary>
[Serializable]
public struct EventRunSnapshot
{
    public string RunId;
    public int CurrentHp;
    public int MaxHpBase;
    public int MaxHpFromPerks;
    public int MaxHpFromRelics;
    public int EnergyMax;
    public int Gold;
}

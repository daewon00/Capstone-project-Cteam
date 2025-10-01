using System;
using System.Collections.Generic;

/// <summary>
/// 전투 보상 묶음을 표현하며 개별 보상 및 선택지를 포함합니다.
/// </summary>
[Serializable]
public class RewardContainer
{
    public int Version = 1;
    public List<RewardItem> Items = new List<RewardItem>();
    // v2.0: 카드 선택지(경량 DTO)
    public List<RewardCardOption> SelectableCards = new List<RewardCardOption>();
}

/// <summary>
/// 기본 보상 항목(골드, 카드, 유물 등)을 정의합니다.
/// </summary>
[Serializable]
public class RewardItem
{
    /// <summary>
    /// 보상 유형(예: "Gold", "Card", "Relic").
    /// </summary>
    public string Type; // "Gold", "Card", "Relic" 등
    public int Amount;
    /// <summary>
    /// 카드나 유물 보상에 사용될 식별자입니다.
    /// </summary>
    public string Id; // 카드 보상 등에서 사용
    public bool IsUpgraded;
}

/// <summary>
/// 선택 가능한 카드 보상 항목을 정의합니다.
/// </summary>
[Serializable]
public class RewardCardOption
{
    public string CardId;
    public bool IsUpgraded;
    public CardRarity Rarity;
}

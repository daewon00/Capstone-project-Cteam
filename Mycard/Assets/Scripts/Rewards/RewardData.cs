using System;
using System.Collections.Generic;

[Serializable]
public class RewardContainer
{
    public int Version = 1;
    public List<RewardItem> Items = new List<RewardItem>();
}

[Serializable]
public class RewardItem
{
    public string Type; // "Gold", "Card", "Relic" 등
    public int Amount;
    public string Id; // 카드 보상 등에서 사용
    public bool IsUpgraded;
}


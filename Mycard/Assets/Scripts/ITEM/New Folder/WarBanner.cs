using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WarBanner : Item
{
    /*
    public int attackBoostAmount = 2; // 올려줄 공격력 수치

    private void Awake()
    {
        this.ItemNumber = 3;
        this.ItemName = "전투 깃발";
        this.ItemSprite = Resources.Load<Sprite>("Sprites/3_WarBanner");
        this.ItemImage = new GameObject("WarBannerImage").AddComponent<Image>();
        this.get_Count = 1;
        this.isget = true;
    }

    public override void OnAddItem()
    {
        if (PlayerBuffs.instance == null)
        {
            var go = new GameObject("GlobalPlayerBuffs");
            go.AddComponent<PlayerBuffs>();
        }

        PlayerBuffs.instance.AddAttackBonus(attackBoostAmount);
    }
    */
}
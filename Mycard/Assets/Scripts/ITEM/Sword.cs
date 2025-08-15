using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Sword : Item
{
    private void Awake()
    {
        this.ItemNumber = 2;
        this.ItemName = "검";
        this.ItemSprite = Resources.Load<Sprite>("Sprites/1_Sword");
        this.ItemImage = new GameObject("SwordImage").AddComponent<Image>();
        this.get_Count = 1;
        this.isget = true;

    }

    // Update is called once per frame
    public virtual void OnAddItem() //아이템에서 얻은 수치를 적용합니다
    {
        BattleController.instance.playermaxMana += 1;
        BattleController.instance.playerMana += 1;
        UIController.instance.SetPlayerManaText(BattleController.instance.playerMana);
    
       
    }
}

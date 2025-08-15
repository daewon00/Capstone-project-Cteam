using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Banana : Item
{
    
    

    private void Awake()
    {
        this.ItemNumber = 1;
        this.ItemName = "바나나";
        this.ItemSprite = Resources.Load<Sprite>("Sprites/2_Banana");
        this.ItemImage = new GameObject("BananaImage").AddComponent<Image>();
        this.get_Count = 1;
        this.isget = true;

    }
    public virtual void OnAddItem() //아이템에서 얻은 수치를 적용합니다
    {
       

        BattleController.instance.playerHealth += 10;

        UIController.instance.setPlayerHealthText(BattleController.instance.playerHealth);
        
        
    }
    
    
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WarBanner : Item
{
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
        // 현재 필드에 있는 모든 플레이어 카드 공격력 상승
        foreach (CardPlacePoint point in CardPointsController.instance.playerCardPoints)
        {
            if (point.activeCard != null && point.activeCard.isPlayer)
            {
                point.activeCard.attackPower += attackBoostAmount;
                point.activeCard.UpdateCardDisplay();

                // 외곽선(Outline) 추가
                Outline outline = point.activeCard.attackText.gameObject.GetComponent<Outline>();
                if (outline == null)
                    outline = point.activeCard.attackText.gameObject.AddComponent<Outline>();

                outline.effectColor = Color.green;
                outline.effectDistance = new Vector2(2, 2);
            }
        }

        //  앞으로 새로 뽑는 카드도 적용되도록 HandController의 heldCards도 처리
        foreach (Card card in HandController.instance.heldCards)
        {
            if (card.isPlayer)
            {
                card.attackPower += attackBoostAmount;
                card.UpdateCardDisplay();

                Outline outline = card.attackText.gameObject.GetComponent<Outline>();
                if (outline == null)
                    outline = card.attackText.gameObject.AddComponent<Outline>();

                outline.effectColor = Color.green;
                outline.effectDistance = new Vector2(2, 2);
            }
        }
    }
}
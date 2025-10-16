using System.Collections.Generic;
using UnityEngine;


public class PlayerBuffs : MonoBehaviour
{
    /*
    public static PlayerBuffs instance;

    [Header("Global Buffs")]
    public int attackBonus = 0;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬 이동해도 유지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddAttackBonus(int amount)
    {
        attackBonus += amount;
        RecomputeAllPlayerCards();
    }

    public void RecomputeAllPlayerCards()
    {
        // 1) 필드 위 플레이어 카드 재계산
        if (CardPointsController.instance != null)
        {
            foreach (var p in CardPointsController.instance.playerCardPoints)
            {
                if (p.activeCard != null && p.activeCard.isPlayer)
                {
                    ApplyAttackToCardFromBase(p.activeCard);
                }
            }
        }

        // 2) 손패 카드 재계산
        if (HandController.instance != null)
        {
            foreach (var c in HandController.instance.heldCards)
            {
                if (c != null && c.isPlayer) ApplyAttackToCardFromBase(c);
            }
        }
    }

    private void ApplyAttackToCardFromBase(Card card)
    {
        // 항상 "기본값 + 전역버프"로 재설정(중복 가산 방지)
        if (card.cardSO != null)
        {
            card.attackPower = card.cardSO.attackPower + attackBonus;
            card.UpdateCardDisplay();
            card.ApplyAttackBuffOutline(attackBonus > 0);
        }
    }*/
}
//사용하지말것 불안전함 warbanner추가전에 공격력 올라가는 버그 발견
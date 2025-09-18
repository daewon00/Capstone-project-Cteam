using System.Collections;
using UnityEngine;

public class CardPointsController : MonoBehaviour
{   //카드의 턴동안의 행동을 담당하는 스크립트입니다

    public static CardPointsController instance;

    private void Awake()
    {
        instance = this;
    }

    public CardPlacePoint[] playerCardPoints, enemyCardPoints, enemyStayPoints;

    public float timeBetweenAttacks = .25f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PlayerAttack()
    {
        BattleSnapshotScheduler.Instance?.SetCombatResolving(true);
        StartCoroutine(PlayerAttackCo());
        CameraController.instance.MoveTo(CameraController.instance.battleTransform);

    }

    IEnumerator PlayerAttackCo()
    {
        
        yield return new WaitForSeconds(timeBetweenAttacks);

        for (int i = 0; i < playerCardPoints.Length; i++)
        {
            var playerCard = (i < playerCardPoints.Length && playerCardPoints[i] != null)
                ? playerCardPoints[i].activeCard : null;
            int baseAtk = playerCard?.attackPower ?? 0;
            int finalAtk = GameEvents.ModifyPlayerAttack?.Invoke(baseAtk) ?? baseAtk;
            if (playerCardPoints[i].activeCard != null) //1번 칸에 있을때
             {
                if (enemyCardPoints[i].activeCard != null) //적카드포인트도 1번칸에 있을때
                {
                    //적카드공격
                    //enemyCardPoints[i].activeCard.DamageCard(playerCardPoints[i].activeCard.attackPower);
                    enemyCardPoints[i].activeCard.DamageCard(finalAtk);//추가+++


                }
                else
                {
                    //BattleController.instance.DamageEnemy(playerCardPoints[i].activeCard.attackPower); //카드가 없다면 직접공격
                    //적카드전체체력
                    BattleController.instance.DamageEnemy(finalAtk);//추가+++
                }

                playerCardPoints[i].activeCard.anim.SetTrigger("Attack");//Attack불러오기

               
                yield return new WaitForSeconds(timeBetweenAttacks);
            }

            if (BattleController.instance.battleEnded == true)
            {
                i = playerCardPoints.Length;
            }
        }

        CheckAssignedCards();

        BattleSnapshotScheduler.Instance?.SetCombatResolving(false);
        BattleController.instance.AdvanceTurn();
    }

    public void EnemyAttack()
    {
        BattleSnapshotScheduler.Instance?.SetCombatResolving(true);
        StartCoroutine(EnemyAttackCo());



    }
    IEnumerator EnemyAttackCo()
    {
        


        yield return new WaitForSeconds(timeBetweenAttacks);


        for (int i = 0; i < enemyCardPoints.Length; i++)
        {


            if (enemyCardPoints[i].activeCard != null)
            {
                if (playerCardPoints[i].activeCard != null)
                {
                    //플레이어카드공격
                    playerCardPoints[i].activeCard.DamageCard(enemyCardPoints[i].activeCard.attackPower);

                    
                }
                else
                {
                    //플레이어전체체력
                    BattleController.instance.DamagePlayer(enemyCardPoints[i].activeCard.attackPower);
                }

                enemyCardPoints[i].activeCard.anim.SetTrigger("Attack");//Attack불러오기

                

                yield return new WaitForSeconds(timeBetweenAttacks);
            }

            if(BattleController.instance.battleEnded == true)
            {
                i = enemyCardPoints.Length;
            }
        }

        CheckAssignedCards();

        GameEvents.OnTurnEnd?.Invoke(false);//추가+++
        BattleSnapshotScheduler.Instance?.SetCombatResolving(false);
        BattleController.instance.AdvanceTurn();
    }

    public void CheckAssignedCards()
    {
        foreach(CardPlacePoint point in enemyCardPoints)
        {
            if (point.activeCard != null)
            {
                if(point.activeCard.currentHealth <= 0)
                {
                    point.activeCard = null;
                }
            }
        }
        
        foreach (CardPlacePoint point in playerCardPoints)
        {
            if (point.activeCard != null)
            {
                if (point.activeCard.currentHealth <= 0)
                {
                    point.activeCard = null;
                }
            }
        }
    }
}

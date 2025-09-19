using System.Collections;
using UnityEngine;

public class CardPointsController : MonoBehaviour
{   //카드의 턴동안의 행동을 담당하는 스크립트입니다

    public static CardPointsController instance;
    private ICardEffectService _effectService;

    private void Awake()
    {
        instance = this;
    }

    public CardPlacePoint[] playerCardPoints, enemyCardPoints, enemyStayPoints;

    public float timeBetweenAttacks = .25f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _effectService = ServiceRegistry.Get<ICardEffectService>();
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
        if (_effectService == null)
            _effectService = ServiceRegistry.Get<ICardEffectService>();

        yield return new WaitForSeconds(timeBetweenAttacks);

        for (int i = 0; i < playerCardPoints.Length; i++)
        {
            var playerCard = (i < playerCardPoints.Length && playerCardPoints[i] != null)
                ? playerCardPoints[i].activeCard : null;
            int baseAtk = playerCard?.attackPower ?? 0;
            int finalAtk = GameEvents.ModifyPlayerAttack?.Invoke(baseAtk) ?? baseAtk;
            if (playerCard == null)
                continue;

            bool usePierce = _effectService?.HasEffect(playerCard, CardEffectType.Pierce) ?? false;

            Card targetCard = (!usePierce && enemyCardPoints[i].activeCard != null)
                ? enemyCardPoints[i].activeCard
                : null;

            int damageToCard = 0;
            int damageToLeader = 0;

            if (targetCard != null)
            {
                var damageResult = targetCard.DamageCard(finalAtk, playerCard, DamageSourceKind.Attack);
                damageToCard = damageResult.AppliedDamage;
            }
            else
            {
                damageToLeader = BattleController.instance.DamageEnemy(finalAtk);
            }

            if (HandController.instance != null)
                playerCard.SetCardScale(HandController.instance.GetBoardScale());

            playerCard.anim.SetTrigger("Attack");

            if (_effectService != null)
            {
                var context = new CardAttackContext(
                    playerCard,
                    attackerIsPlayer: true,
                    laneIndex: i,
                    baseAttack: finalAtk,
                    damageToPrimary: damageToCard,
                    damageToLeader: damageToLeader,
                    primaryTarget: targetCard,
                    hitCard: targetCard != null);
                _effectService.HandleAttackResolved(context);
            }

            yield return new WaitForSeconds(timeBetweenAttacks);

            if (BattleController.instance.battleEnded)
                break;
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
        if (_effectService == null)
            _effectService = ServiceRegistry.Get<ICardEffectService>();



        yield return new WaitForSeconds(timeBetweenAttacks);


        for (int i = 0; i < enemyCardPoints.Length; i++)
        {
            var enemyCard = enemyCardPoints[i].activeCard;
            if (enemyCard == null)
                continue;

            int baseAtk = enemyCard.attackPower;
            int finalAtk = GameEvents.ModifyEnemyAttack?.Invoke(baseAtk) ?? baseAtk;
            bool usePierce = _effectService?.HasEffect(enemyCard, CardEffectType.Pierce) ?? false;

            Card targetCard = (!usePierce && playerCardPoints[i].activeCard != null)
                ? playerCardPoints[i].activeCard
                : null;

            int damageToCard = 0;
            int damageToLeader = 0;

            if (targetCard != null)
            {
                var damageResult = targetCard.DamageCard(finalAtk, enemyCard, DamageSourceKind.Attack);
                damageToCard = damageResult.AppliedDamage;
            }
            else
            {
                damageToLeader = BattleController.instance.DamagePlayer(finalAtk);
            }

            if (HandController.instance != null)
                enemyCard.SetCardScale(HandController.instance.GetBoardScale());

            enemyCard.anim.SetTrigger("Attack");

            if (_effectService != null)
            {
                var context = new CardAttackContext(
                    enemyCard,
                    attackerIsPlayer: false,
                    laneIndex: i,
                    baseAttack: finalAtk,
                    damageToPrimary: damageToCard,
                    damageToLeader: damageToLeader,
                    primaryTarget: targetCard,
                    hitCard: targetCard != null);
                _effectService.HandleAttackResolved(context);
            }

            yield return new WaitForSeconds(timeBetweenAttacks);

            if (BattleController.instance.battleEnded)
                break;
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

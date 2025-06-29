using System.Collections;
using UnityEngine;

public class BattleController : MonoBehaviour
{

    public static BattleController instance;

    private void Awake()
    {
        instance = this;
    }


    public int startingMana = 4, maxMana = 12;
    public int playerMana, enemyMana;
    private int currentPlayerMaxMana, currentEnemyMaxMana;


    public int startingcardAmount = 5;
    public int cardToDrawPerTurn = 2;

    public enum TurnOrder { playerActive, playerCardAttacks, enemyActive, enemyCardAttacks }
    public TurnOrder currentPhase;

    public Transform discardPoint;
    public int playerHealth, enemyHealth;

    public bool battleEnded;

    public float resultScreenDelayTime = 1f;

    [Range(0f,1f)]
    public float playerFirstChance = .5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //playerMana = startingMana;
        //UIController.instance.SetPlayerManaText(playerMana);

        currentPlayerMaxMana = startingMana;

        FillPlayerMana();

        DeckController.instance.DrawMulitpleCards(startingcardAmount);

        UIController.instance.setPlayerHealthText(playerHealth);
        UIController.instance.setEnemyHealthText(enemyHealth);

        currentEnemyMaxMana = startingMana;
        FillEnemyMana();

        if(Random.value > playerFirstChance) //랜덤턴 지우면 플레이어 선공임
        {
            currentPhase = TurnOrder.playerCardAttacks;
            AdvanceTurn();
        }

        //AudioManager.instance.PlayBGM();

    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.T))
        {
            AdvanceTurn();
        }
    }

    public void SpendPlayerMana(int amountToSpend)
    {
        playerMana = playerMana - amountToSpend;


        if(playerMana < 0) 
        {
            playerMana = 0;
        }

        UIController.instance.SetPlayerManaText(playerMana);
    }

    public void FillPlayerMana()
    {
        //playerMana = startingMana;
        playerMana = currentPlayerMaxMana;
        UIController.instance.SetPlayerManaText(playerMana);
    }
    public void SpendEnemyrMana(int amountToSpend)
    {
        enemyMana -= amountToSpend;


        if (enemyMana < 0)
        {
            enemyMana = 0;
        }

        UIController.instance.SetEnemyManaText(enemyMana);
    }

    public void FillEnemyMana()
    {
        
        enemyMana = currentEnemyMaxMana;
        UIController.instance.SetEnemyManaText(enemyMana);
    }

    public void AdvanceTurn()
    {
        if (battleEnded == false)
        {
            currentPhase++;

            if ((int)currentPhase >= System.Enum.GetValues(typeof(TurnOrder)).Length)
            {
                currentPhase = 0;
            }

            switch (currentPhase)
            {
                case TurnOrder.playerActive:
                    CameraController.instance.MoveTo(CameraController.instance.homeTransform);
                    UIController.instance.endTurnButton.SetActive(true);
                    UIController.instance.drawCardButton.SetActive(true);

                    if (currentPlayerMaxMana < maxMana)
                    {
                        currentPlayerMaxMana++;
                    }

                    FillPlayerMana();

                    DeckController.instance.DrawMulitpleCards(cardToDrawPerTurn);

                    break;

                case TurnOrder.playerCardAttacks:

                    //Debug.Log("Skipping player card attacks");
                    //AdvanceTurn();
                    CardPointsController.instance.PlayerAttack();

                    break;

                case TurnOrder.enemyActive:

                    //Debug.Log("Skipping enemy actions");
                    //AdvanceTurn();
                    
                    if (currentEnemyMaxMana < maxMana)
                    {
                        currentEnemyMaxMana++;
                    }

                    FillEnemyMana();

                    EnemyController.instance.StartAction();

                    break;

                case TurnOrder.enemyCardAttacks:

                    //Debug.Log("Skipping enemy card attacks");
                    //AdvanceTurn();
                    CardPointsController.instance.EnemyAttack();

                    break;

            }
        }
    }

    public void EndPlayerTurn()
    {
        UIController.instance.endTurnButton.SetActive(false);
        UIController.instance.drawCardButton.SetActive(false);
        AdvanceTurn();
    }

    public void DamagePlayer(int damageAmount)
    {
        if (playerHealth > 0 || !battleEnded)
        {
            playerHealth -= damageAmount;

            if(playerHealth <= 0)
            {
                playerHealth = 0;

                //END BATTLe
                EndBattle();
            }


            UIController.instance.setPlayerHealthText(playerHealth);

            UIDamageIndicator damageClone = Instantiate(UIController.instance.playerDamage, UIController.instance.playerDamage.transform.parent);
            damageClone.damageText.text = damageAmount.ToString();
            damageClone.gameObject.SetActive(true);

            AudioManager.instance.PlaySFX(6);
        }
    }

    public void DamageEnemy(int damageAmount)
    {
        if (enemyHealth > 0 || battleEnded == false)
        {
            enemyHealth -= damageAmount;

            if (enemyHealth <= 0)
            {
                enemyHealth = 0;

                //END BATTLe
                EndBattle();
            }

            UIController.instance.setEnemyHealthText(enemyHealth);

            UIDamageIndicator damageClone = Instantiate(UIController.instance.enemyDamage, UIController.instance.enemyDamage.transform.parent);
            damageClone.damageText.text = damageAmount.ToString();
            damageClone.gameObject.SetActive(true);

            AudioManager.instance.PlaySFX(5);
        }
    }

    void EndBattle()
    {
        battleEnded = true;

        HandController.instance.EmptyHand();

        if(enemyHealth <= 0)
        {
            UIController.instance.battleResultText.text = "You Won!";

            foreach(CardPlacePoint point in CardPointsController.instance.enemyCardPoints)
            {
                if(point.activeCard != null)
                {
                    point.activeCard.MoveToPoint(discardPoint.position, point.activeCard.transform.rotation);
                }
            }
        }
        else
        {
            UIController.instance.battleResultText.text = "You Lose!";

            foreach (CardPlacePoint point in CardPointsController.instance.playerCardPoints)
            {
                if (point.activeCard != null)
                {
                    point.activeCard.MoveToPoint(discardPoint.position, point.activeCard.transform.rotation);
                }
            }
        }


        StartCoroutine(ShowResultCo());
    }

    IEnumerator ShowResultCo()
    {
        yield return new WaitForSeconds(resultScreenDelayTime);

        UIController.instance.battleEndScreen.SetActive(true);
    }
}

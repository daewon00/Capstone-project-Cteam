using System.Collections;
using UnityEngine;

public class BattleController : MonoBehaviour
{

    public static BattleController instance;

    // 이 스크립트가 생성될 때 instance에 자기 자신 할당
    private void Awake()
    {
        instance = this;
    }

    // --- 전투 기본 설정 변수들 ---
    public int startingMana = 3, playermaxMana = 3, enemymaxMana = 3;  //시작마나, 최대 마나
    public int playerMana, enemyMana;   //플레이어 마나, 적 마나
    private int currentPlayerMaxMana, currentEnemyMaxMana;  // 플레이어와 적의 현재 턴의 최대 마나 (턴마다 1씩 증가)


    public int startingcardAmount = 5;  //첫 드로우 카드 수
    public int cardToDrawPerTurn = 2;   //매턴 드로우 카드 수

    public enum TurnOrder { playerActive, playerCardAttacks, enemyActive, enemyCardAttacks }    //전투 단계
    public TurnOrder currentPhase;  // 지금 단계 저장

    public Transform discardPoint;  //파괴 카드 위치
    public int playerHealth, enemyHealth;   //플레이어 체력, 적 체력

    public bool battleEnded;    //전투 끝 참거짓

    public float resultScreenDelayTime = 1f;    // 전투 종료 후 결과창 딜레이 시간

    [Range(0f,1f)]
    public float playerFirstChance = .5f;   // 플레이어가 선공할 확률 (0.5 = 50%)

    // 첫 프레임 시작 전에 호출
    void Start()
    {
        //playerMana = startingMana;
        //UIController.instance.SetPlayerManaText(playerMana);

        currentPlayerMaxMana = startingMana;    //마나값을 시작 마나값으로 초기화

        FillPlayerMana();   //플레이어 마나를 채운다

        DeckController.instance.DrawMulitpleCards(startingcardAmount);  // 시작 카드 수 만큼 뽑기

        UIController.instance.setPlayerHealthText(playerHealth);    //플레이어 체력 UI 표기
        UIController.instance.setEnemyHealthText(enemyHealth);  //적 체력 UI 표기

        currentEnemyMaxMana = startingMana; //적 마나 시작 마나값으로 초기화
        FillEnemyMana();    //적 마나를 채운다

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
        //테스트용 코드 T를 누르면 강제로 턴 진행 *나중에 꼭 삭제*
        if(Input.GetKeyDown(KeyCode.T))
        {
            AdvanceTurn();
        }
    }
    // 플레이어의 마나를 amountToSpend만큼 소모
    public void SpendPlayerMana(int amountToSpend)
    {
        playerMana = playerMana - amountToSpend;

        // 음수가 되면 0으로 애초에 음수가 안되야 될텐데 *수정*
        if(playerMana < 0) 
        {
            playerMana = 0;
        }

        UIController.instance.SetPlayerManaText(playerMana);
    }

    //플레이어의 마나를 최대치까지 채움
    public void FillPlayerMana()
    {
        //playerMana = startingMana;
        playerMana = currentPlayerMaxMana;
        UIController.instance.SetPlayerManaText(playerMana);
    }

    // 적의 마나를 소모 *필요한가? 음수도 조정*
    public void SpendEnemyrMana(int amountToSpend)
    {
        enemyMana -= amountToSpend;


        if (enemyMana < 0)
        {
            enemyMana = 0;
        }

        UIController.instance.SetEnemyManaText(enemyMana);
    }

    //적의 마나를 최대치까지 채움
    public void FillEnemyMana()
    {
        
        enemyMana = currentEnemyMaxMana;
        UIController.instance.SetEnemyManaText(enemyMana);
    }

    //턴 진행
    public void AdvanceTurn()
    {
        if (battleEnded == false)   //배틀이 끝나지 않았을때
        {
            currentPhase++;

            if ((int)currentPhase >= System.Enum.GetValues(typeof(TurnOrder)).Length)
            {
                currentPhase = 0;   // 턴 단계 다 끝나면 턴 단계 초기화
            }

            
            switch (currentPhase)   //턴 단계에 따라 실행
            {
                case TurnOrder.playerActive:
                    CameraController.instance.MoveTo(CameraController.instance.homeTransform);  //카메라 위치 초기화
                    UIController.instance.endTurnButton.SetActive(true);    // 턴종료 버튼 활성화
                    UIController.instance.drawCardButton.SetActive(true);   //카드 뽑기 버튼 활성화

                    if (currentPlayerMaxMana < playermaxMana) // 최대마나보다 작으면 플레이어 마나증가 *첫턴은 증가하면 안될텐데*
                    {
                        currentPlayerMaxMana++;
                    }

                    FillPlayerMana();   //마나를 가득 채움

                    DeckController.instance.DrawMulitpleCards(cardToDrawPerTurn);   //정해준 변수만큼 카드 드로우

                    break;

                case TurnOrder.playerCardAttacks:   //플레이어 공격

                    //Debug.Log("Skipping player card attacks");
                    //AdvanceTurn();
                    CardPointsController.instance.PlayerAttack();   //CardPointsController에 PlayerAttack함수 실행(플레이어 공격 매커니즘)

                    break;

                case TurnOrder.enemyActive:

                    //Debug.Log("Skipping enemy actions");
                    //AdvanceTurn();
                    
                    if (currentEnemyMaxMana < enemymaxMana)  // 최대마나보다 작으면 플레이어 마나증가 *첫턴은 증가하면 안될텐데*
                    {
                        currentEnemyMaxMana++;
                    }

                    FillEnemyMana();    //적 마나를 채운다

                    EnemyController.instance.StartAction(); //EnemyController에 StartAction함수 실행(적 플레이 매커니즘)

                    break;

                case TurnOrder.enemyCardAttacks:    //적 공격

                    //Debug.Log("Skipping enemy card attacks");
                    //AdvanceTurn();
                    CardPointsController.instance.EnemyAttack();    ////CardPointsController에 EnemyAttack함수 실행(적 공격 매커니즘)

                    break;

            }
        }
    }

    public void EndPlayerTurn() //턴 종료 눌리면 버튼 비활성화 하고 턴 진행
    {
        UIController.instance.endTurnButton.SetActive(false);
        UIController.instance.drawCardButton.SetActive(false);
        AdvanceTurn();
    }

    //플레이어에게 데미지를 주는 함수
    public void DamagePlayer(int damageAmount)
    {
        if (playerHealth > 0 || !battleEnded)
        {
            playerHealth -= damageAmount;

            if(playerHealth <= 0)   //체력이 0이하가 되면 배틀 종료
            {
                playerHealth = 0;

                EndBattle();    //END BATTLE
            }


            UIController.instance.setPlayerHealthText(playerHealth);    //UI 체력 갱신

            //데미지 숫자 표시
            UIDamageIndicator damageClone = Instantiate(UIController.instance.playerDamage, UIController.instance.playerDamage.transform.parent);
            damageClone.damageText.text = damageAmount.ToString();
            damageClone.gameObject.SetActive(true);

            AudioManager.instance.PlaySFX(6);   //6번 효과음 재생
        }
    }

    //적에게 데미지를 주는 함수
    public void DamageEnemy(int damageAmount)
    {
        if (enemyHealth > 0 || battleEnded == false)
        {
            enemyHealth -= damageAmount;

            if (enemyHealth <= 0)
            {
                enemyHealth = 0;

                EndBattle();    //END BATTLE
            }

            UIController.instance.setEnemyHealthText(enemyHealth);

            UIDamageIndicator damageClone = Instantiate(UIController.instance.enemyDamage, UIController.instance.enemyDamage.transform.parent);
            damageClone.damageText.text = damageAmount.ToString();
            damageClone.gameObject.SetActive(true);

            AudioManager.instance.PlaySFX(5);   //5번 효과음 재생
        }
    }

    //전투 종료
    void EndBattle()
    {
        battleEnded = true;

        HandController.instance.EmptyHand();    //핸드 제거

        if(enemyHealth <= 0)    // 적 체력 0 이하 승리시
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
        else // 패배시 *필드에 남아있는 카드 제거 하는거 꼭 해야되는거면 패배 승리 상관 없이 전부 해야되는거 아닌가?*
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


        StartCoroutine(ShowResultCo()); //결과 화면 
    }

    IEnumerator ShowResultCo()
    {
        yield return new WaitForSeconds(resultScreenDelayTime); // 지연 시키고

        UIController.instance.battleEndScreen.SetActive(true);  // 결과 UI 표시
    }
}

using System.Collections;
using UnityEngine;
using Game.Save;
using BattleSnapshot;

// Phase 2: 조립 책임자 역할 부여. 다른 컨트롤러보다 먼저 실행되도록 우선순위 부여
[DefaultExecutionOrder(-9000)]
public class BattleController : MonoBehaviour
{

    public static BattleController instance;
    private IRunService _runService; // 전투 결과 보고 대상
    private ICardEffectService _effectService;
    internal static bool SkipInitialSetup { get; set; }

    [Header("Dependencies")]

    [Header("전투 규칙 설정")]
    [SerializeField] private int _initialHandCount = 5;
    [SerializeField] private int _handLimit = 10;
    [SerializeField] private int _drawCardCost = 2; // 드로우 버튼 비용

    // Phase 2 준비: 서비스 주입(전투 흐름에서 IDeckService 사용 예정)
    private IDeckService _deckService;
    private bool _isInitialized;
    private bool _battleStarted;
    private bool _isAdvancingTurn = false; // 중복 턴 진행 방지
    private int _turnCounter = 1;
    private bool _waitingForPlayerTurnStartEffects;

    // 이 스크립트가 생성될 때 instance에 자기 자신 할당
    private void Awake()
    {
        instance = this;

        // --- 부트스트래핑: 필수 서비스 주입 ---
        // GameInitializer(-10000)에서 등록 완료됨
        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService != null)
        {
            try { Initialize(deckService); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleController] Initialize(deckService) 실패: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[BattleController] IDeckService를 찾지 못했습니다. 추후 단계에서 연결 예정.");
        }

        _effectService = ServiceRegistry.Get<ICardEffectService>();
        if (_effectService == null)
        {
            Debug.LogWarning("[BattleController] ICardEffectService를 찾지 못했습니다. 카드 효과가 적용되지 않습니다.");
        }

    }


    /// <summary>
    /// Bootstrap/초기화 시점에 IDeckService를 주입받습니다. (향후 전투 시작/턴 흐름에서 사용)
    /// </summary>
    public void Initialize(IDeckService deckService)
    {
        _deckService = deckService;
        if (_deckService != null)
        {
            _deckService.SetHandLimit(_handLimit);
            _isInitialized = true;
        }
    }
    /// <summary>
    /// 플레이어 턴 시작 시 드로우를 요청하고, 드로우 완료 후 유물 효과를 발동할 수 있도록 대기 상태로 표시합니다.
    /// </summary>
    private void RequestPlayerTurnStartDraw(int drawCount)
    {
        if (!_isInitialized || _deckService == null)
        {
            Debug.LogWarning("[BattleController] IDeckService가 초기화되지 않아 드로우를 건너뛰고 턴 시작 효과를 즉시 발동합니다.");
            GameEvents.RaiseTurnStart(true);
            return;
        }

        _waitingForPlayerTurnStartEffects = true;
        try
        {
            _deckService.DrawCards(drawCount, DrawReason.TurnStart);
        }
        catch
        {
            _waitingForPlayerTurnStartEffects = false;
            throw;
        }
    }

    /// <summary>
    /// 핸드 생성이 완료된 뒤(HandServiceBinder 경유) 호출되어 플레이어 턴 시작 효과를 발동합니다.
    /// </summary>
    internal void NotifyPlayerTurnStartReady()
    {
        if (!_waitingForPlayerTurnStartEffects)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BattleController] NotifyPlayerTurnStartReady가 예기치 않게 호출되었습니다. 이미 효과가 발동되었을 수 있습니다.");
#endif
        }

        _waitingForPlayerTurnStartEffects = false;
        GameEvents.RaiseTurnStart(true);
    }


    [Header("Player Fallback Defaults")]
    [SerializeField] private int _fallbackPlayerHealth = 30;
    [SerializeField] private int _fallbackPlayerMaxMana = 3;
    [SerializeField] private int _fallbackPlayerStartingMana = 3;

    [Header("Enemy Defaults")]
    [SerializeField] private int _fallbackEnemyHealth = 10;
    public int enemymaxMana = 3;
    public int startingEnemeyMana = 3;

    [Header("Turn Draw Settings")]
    public int startingcardAmount = 5;  //첫 드로우 카드 수
    public int cardToDrawPerTurn = 2;   //매턴 드로우 카드 수

    // --- 전투 중 갱신되는 런타임 값들 ---
    public int playerMana { get; set; }   //플레이어 마나
    public int enemyMana { get; set; }    //적 마나
    public int playermaxMana { get; set; } // 플레이어 최대 마나(턴 성장 상한)
    public int currentPlayerMaxMana { get; set; } // 현재 턴 플레이어 최대 마나
    public int currentEnemyMaxMana { get; set; }   // 현재 턴 적 최대 마나

    public int playerHealth { get; set; }   //플레이어 체력
    public int enemyHealth { get; set; }    //적 체력

    private bool _playerStatsInitialized;

    public enum TurnOrder { playerActive, playerCardAttacks, enemyActive, enemyCardAttacks }    //전투 단계
    public TurnOrder currentPhase;  // 지금 단계 저장

    public int CurrentTurnNumber => _turnCounter;
    public TurnOrder CurrentPhase => currentPhase;

    private void RequestSnapshot(string reason)
    {
        BattleSnapshotScheduler.Instance?.RequestSnapshot(reason);
    }

    /// <summary>
    /// 런 저장 데이터를 반영해 체력과 에너지 최대치를 전투 컨트롤러에 주입합니다.
    /// </summary>
    public void ApplyRunStats(int currentHp, int maxHp, int energyMax)
    {
        int resolvedMaxHp = Mathf.Max(1, maxHp);
        playerHealth = Mathf.Clamp(currentHp, 0, resolvedMaxHp);

        int resolvedEnergy = Mathf.Max(1, energyMax);
        playermaxMana = resolvedEnergy;
        currentPlayerMaxMana = resolvedEnergy;
        
        FillPlayerMana();
        UIController.instance?.setPlayerHealthText(playerHealth);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleController] ApplyRunStats hp={playerHealth}/{resolvedMaxHp} energy={resolvedEnergy}");
#endif
        _playerStatsInitialized = true;
    }

    public void SetTurnStateFromSnapshot(int turnNumber, TurnOrder phase, int playerManaValue, int playerMaxManaValue, int enemyManaValue, int enemyMaxManaValue, bool ended)
    {
        _turnCounter = Mathf.Max(1, turnNumber);
        currentPhase = phase;
        int resolvedPlayerMax = Mathf.Max(1, playerMaxManaValue);
        playermaxMana = resolvedPlayerMax;
        currentPlayerMaxMana = resolvedPlayerMax;
        playerMana = Mathf.Clamp(playerManaValue, 0, currentPlayerMaxMana);
        enemyMana = enemyManaValue;
        currentEnemyMaxMana = enemyMaxManaValue;
        battleEnded = ended;

        UIController.instance?.SetPlayerManaText(playerMana);
        UIController.instance?.SetEnemyManaText(enemyMana);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleController] SetTurnStateFromSnapshot: turn={_turnCounter}, phase={currentPhase}, playerMana={playerMana}/{currentPlayerMaxMana}, enemyMana={enemyMana}/{currentEnemyMaxMana}");
#endif
        _playerStatsInitialized = true;
    }

    public void MarkRestored()
    {
        _battleStarted = true;
        _playerStatsInitialized = true;
    }

    public Transform discardPoint;  //파괴 카드 위치

    public bool battleEnded;    //전투 끝 참거짓

    public float resultScreenDelayTime = 1f;    // 전투 종료 후 결과창 딜레이 시간

    [Range(0f,1f)]
    public float playerFirstChance = .5f;   // 플레이어가 선공할 확률 (0.5 = 50%)

    // 첫 프레임 시작 전에 호출
    void Start()
    {
        // Always cache the run service so restored combats can report their result.
        _runService = ServiceRegistry.Get<IRunService>();

        if (SkipInitialSetup)
        {
            SkipInitialSetup = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BattleController] Start skipped due to snapshot restore");
#endif
            return;
        }

        GameEvents.RaiseBattleStart(); // 추가 +++

        if (!_playerStatsInitialized)
        {
            ApplyFallbackPlayerStats();
            FillPlayerMana();
        }
        else
        {
            playerMana = Mathf.Clamp(playerMana, 0, currentPlayerMaxMana);
            UIController.instance?.SetPlayerManaText(playerMana);
        }

        // 초기 드로우는 BattleSceneBootstrap -> StartBattle() 경로에서 처리됩니다.
        
        if (enemyHealth <= 0)
            enemyHealth = Mathf.Max(0, _fallbackEnemyHealth);

        UIController.instance.setPlayerHealthText(playerHealth);    //플레이어 체력 UI 표기
        UIController.instance.setEnemyHealthText(enemyHealth);  //적 체력 UI 표기

        currentEnemyMaxMana = startingEnemeyMana; //적 마나 시작 마나값으로 초기화
        FillEnemyMana();    //적 마나를 채운다

        if(Random.value > playerFirstChance) //랜덤턴 지우면 플레이어 선공임
        {
            currentPhase = TurnOrder.playerCardAttacks;
            AdvanceTurn();
        }
        AudioManager.instance.StopMusic();
        AudioManager.instance.PlayBGM();

    }

    private void ApplyFallbackPlayerStats()
    {
        int maxManaCap = Mathf.Max(1, _fallbackPlayerMaxMana);
        playermaxMana = maxManaCap;
        currentPlayerMaxMana = Mathf.Clamp(_fallbackPlayerStartingMana, 1, maxManaCap);
        playerMana = currentPlayerMaxMana;
        playerHealth = Mathf.Max(0, _fallbackPlayerHealth);
        _playerStatsInitialized = true;
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
        playerMana = currentPlayerMaxMana;

        playerMana = GameEvents.ApplyPlayerManaModifiers(playerMana);

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
        if (_isAdvancingTurn) return;
        _isAdvancingTurn = true;
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

                    RequestPlayerTurnStartDraw(cardToDrawPerTurn);

                    break;

                case TurnOrder.playerCardAttacks:   //플레이어 공격

                    //Debug.Log("Skipping player card attacks");
                    //AdvanceTurn();
                    CardPointsController.instance.PlayerAttack();   //CardPointsController에 PlayerAttack함수 실행(플레이어 공격 매커니즘)

                    break;

                case TurnOrder.enemyActive:
                    GameEvents.RaiseTurnStart(false); // 추가 +++ 적 턴 시작
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
        _isAdvancingTurn = false;
        if (currentPhase == TurnOrder.playerActive && !battleEnded)
        {
            _turnCounter = Mathf.Max(1, _turnCounter + 1);
        }
    }

    public void EndPlayerTurn() //턴 종료 눌리면 버튼 비활성화 하고 턴 진행
    {
        UIController.instance.endTurnButton.SetActive(false);
        UIController.instance.drawCardButton.SetActive(false);

        RequestSnapshot("BeforePlayerEndTurn");
        GameEvents.RaiseTurnEnd(true);   // 추가 +++ 플레이어 턴 종료
        AdvanceTurn();
    }

    /// <summary>
    /// 카드 사용을 시도하는 중앙 관문. 규칙 검사 후 성공 시 마나 차감과 덱 서비스 호출을 수행합니다.
    /// </summary>
    public bool AttemptPlayCard(Card card)
    {
        if (battleEnded) return false;
        if (!_isInitialized || _deckService == null)
        {
            Debug.LogError("[BattleController] AttemptPlayCard 실패: IDeckService가 초기화되지 않았습니다.");
            return false;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleController] AttemptPlayCard: instance={(card!=null?card.InstanceId:"<null>")}, mana={playerMana}/{playermaxMana}, phase={currentPhase}");
#endif
        if (currentPhase != TurnOrder.playerActive)
        {
            Debug.LogWarning("[BattleController] 플레이어 턴이 아니므로 카드를 사용할 수 없습니다.");
            return false;
        }
        if (card == null)
        {
            Debug.LogWarning("[BattleController] AttemptPlayCard: card가 null 입니다.");
            return false;
        }
        if (playerMana < card.manaCost)
        {
            UIController.instance?.ShowManaWarning();
            return false;
        }

        // 규칙 통과: 마나 차감 후 서비스에 사용 통보
        SpendPlayerMana(card.manaCost);
        var result = _deckService.PlayCard(card.InstanceId);
        if (result == null || result.Code != PlayResult.ResultCode.Success)
        {
            Debug.LogWarning($"[BattleController] PlayCard 실패: {(result==null?"null":result.Code.ToString())}");
            return false;
        }
        _effectService?.RegisterBoardCard(card, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleController] PlayCard success: instance={card.InstanceId}");
#endif
        return true;
    }

    // --------- 소프트 체크(부작용 없음) ---------
    public enum Playability { Ok, NotPlayerTurn, NotEnoughMana }

    public Playability EvaluatePlayability(Card card)
    {
        if (battleEnded) return Playability.NotPlayerTurn;
        if (currentPhase != TurnOrder.playerActive)
            return Playability.NotPlayerTurn;
        if (card == null) return Playability.NotEnoughMana;
        if (playerMana < card.manaCost)
            return Playability.NotEnoughMana;
        return Playability.Ok;
    }

    /// <summary>
    /// 플레이어가 드로우 버튼을 눌렀을 때, 마나 규칙을 검사하고 드로우를 시도합니다.
    /// </summary>
    public void AttemptPlayerDraw()
    {
        if (battleEnded) return;
        if (!_isInitialized || _deckService == null)
        {
            Debug.LogError("[BattleController] AttemptPlayerDraw 실패: IDeckService가 초기화되지 않았습니다.");
            return;
        }
        if (currentPhase != TurnOrder.playerActive)
        {
            Debug.LogWarning("[BattleController] 플레이어 턴이 아니므로 드로우할 수 없습니다.");
            return;
        }

        if (playerMana >= _drawCardCost)
        {
            SpendPlayerMana(_drawCardCost);
            _deckService.DrawCards(1, DrawReason.ManualButton);
        }
        else
        {
            UIController.instance.ShowManaWarning();
            UIController.instance.drawCardButton.SetActive(false);
        }
    }

    /// <summary>
    /// 전투 시작: 초기 패 드로우를 서비스 경로로 수행합니다(중복 방지 포함).
    /// </summary>
    public void StartBattle()
    {
        if (!_isInitialized)
            throw new System.InvalidOperationException("[BattleController] 서비스가 초기화되지 않았습니다. Bootstrap을 확인하세요.");

        if (_battleStarted)
        {
            Debug.LogWarning("[BattleController] StartBattle이 중복 호출되었습니다.");
            return;
        }
        _battleStarted = true;

        Debug.Log("[BattleController] 전투 시작! 초기 드로우를 요청합니다.");
        try
        {
            _deckService.SetHandLimit(_handLimit);
            // 새 전투 시작 전, 더미 초기화/셔플을 보장
            _deckService.PrepareNewCombat();
            //_deckService.DrawCards(_initialHandCount, DrawReason.TurnStart);
            RequestPlayerTurnStartDraw(_initialHandCount);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleController] 초기 드로우 실패: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        // 씬 전환 등으로 파괴될 때, 전투가 시작된 상태였다면 남은 핸드 정리를 보장
        try
        {
            if (_isInitialized && _battleStarted && _deckService != null && battleEnded)
            {
                _deckService.CleanupAfterCombat();
            }
        }
        catch { }
    }

    //플레이어에게 데미지를 주는 함수
    public int DamagePlayer(int damageAmount)
    {
        if (playerHealth <= 0 || battleEnded)
            return 0;

        int appliedDamage = damageAmount;
        if (_effectService != null)
        {
            var mitigation = _effectService.ProcessLeaderDamage(true, damageAmount);
            appliedDamage = mitigation.RemainingDamage;
        }

        if (appliedDamage <= 0)
            return 0;

        playerHealth -= appliedDamage;
        GameEvents.RaiseDamageDealt(appliedDamage, false);
        if (playerHealth <= 0)
        {
            playerHealth = 0;
            EndBattle();
        }

        UIController.instance.setPlayerHealthText(playerHealth);

        UIDamageIndicator damageClone = Instantiate(UIController.instance.playerDamage, UIController.instance.playerDamage.transform.parent);
        damageClone.damageText.text = appliedDamage.ToString();
        damageClone.gameObject.SetActive(true);

        AudioManager.instance.PlaySFX(6);
        return appliedDamage;
    }

    //적에게 데미지를 주는 함수
    public int DamageEnemy(int damageAmount)
    {
        if (enemyHealth <= 0 || battleEnded)
            return 0;

        int appliedDamage = damageAmount;
        if (_effectService != null)
        {
            var mitigation = _effectService.ProcessLeaderDamage(false, damageAmount);
            appliedDamage = mitigation.RemainingDamage;
        }

        if (appliedDamage <= 0)
            return 0;

        enemyHealth -= appliedDamage;
        GameEvents.RaiseDamageDealt(appliedDamage, true);
        if (enemyHealth <= 0)
        {
            enemyHealth = 0;
            EndBattle();
        }

        UIController.instance.setEnemyHealthText(enemyHealth);

        UIDamageIndicator damageClone = Instantiate(UIController.instance.enemyDamage, UIController.instance.enemyDamage.transform.parent);
        damageClone.damageText.text = appliedDamage.ToString();
        damageClone.gameObject.SetActive(true);

        AudioManager.instance.PlaySFX(5);
        return appliedDamage;
    }

    public void HealLeader(bool isPlayerLeader, int amount)
    {
        if (amount <= 0)
            return;

        if (isPlayerLeader)
        {
            playerHealth += amount;
            UIController.instance.setPlayerHealthText(playerHealth);
        }
        else
        {
            enemyHealth += amount;
            UIController.instance.setEnemyHealthText(enemyHealth);
        }
    }

    //전투 종료
    void EndBattle()
    {
        battleEnded = true;
        GameEvents.RaiseBattleEnd();      // +++ 전투 종료 알림
        // 덱 서비스 측 상태 정리(남은 핸드 → Discard 등)
        try { _deckService?.CleanupAfterCombat(); } catch { }
        HandController.instance.EmptyHand();    //핸드 제거

        if(enemyHealth <= 0)    // 적 체력 0 이하 승리시
        {
            
            UIController.instance.battleResultText1.text = "You Won!";

            foreach(CardPlacePoint point in CardPointsController.instance.enemyCardPoints)
            {
                if(point.activeCard != null)
                {
                    point.activeCard.MoveToPoint(discardPoint.position, point.activeCard.transform.rotation);
                }
            }
            foreach (CardPlacePoint point in CardPointsController.instance.enemyStayPoints)
            {
                if (point.activeCard != null)
                {
                    point.activeCard.MoveToPoint(discardPoint.position, point.activeCard.transform.rotation);
                }
            }
        }
        else // 패배시 *필드에 남아있는 카드 제거 하는거 꼭 해야되는거면 패배 승리 상관 없이 전부 해야되는거 아닌가?*
        {
            
            UIController.instance.battleResultText2.text = "You Lose!";

            foreach (CardPlacePoint point in CardPointsController.instance.playerCardPoints)
            {
                if (point.activeCard != null)
                {
                    point.activeCard.MoveToPoint(discardPoint.position, point.activeCard.transform.rotation);
                }
            }

        }
        
        UIController.instance.EnemyUI.SetActive(false);

        PersistPlayerHealth();

        // 전투 결과를 런 서비스에 보고하여 DB/라우팅을 위임합니다.
        try
        {
            var result = (enemyHealth <= 0) ? CombatResult.Victory : CombatResult.Defeat;
            _runService?.ReportCombatEnded(result);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BattleController] ReportCombatEnded 실패: {e.Message}");
        }

        StartCoroutine(ShowResultCo()); //결과 화면 
    }

    IEnumerator ShowResultCo()
    {
        yield return new WaitForSeconds(resultScreenDelayTime); // 지연 시키고

        if (enemyHealth <= 0)
        {
            // 보스전 승리 시에는 별도의 'RUN CLEARED!' 오버레이가 표시되므로 기본 승리 화면은 생략합니다.
            var kind = GameContext.I != null ? GameContext.I.CurrentBattleKind : GameContext.BattleKind.Normal;
            Debug.Log($"[BossFlow][BattleController] ShowResultCo victory, battleKind={kind}");
            if (kind != GameContext.BattleKind.Boss)
            {
                UIController.instance.battleEndScreen_win.SetActive(true);  // 결과 UI 표시
            }
        }
        else
        {
            UIController.instance.battleEndScreen_lose.SetActive(true);
        }
    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 개발 빌드 전용: 버튼 OnClick에 연결하여 즉시 전투 승리를 트리거합니다.
    public void DevForceWin()
    {
        if (battleEnded) return;
        // 현재 남은 체력만큼 피해를 주어 정상 승리 경로(EndBattle)로 진입
        int dmg = Mathf.Max(1, enemyHealth);
        DamageEnemy(dmg);
    }
#endif

    private void PersistPlayerHealth()
    {
        string runId = (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
            ? GameContext.I.RunId
            : PlayerPrefs.GetString("lastRunId", string.Empty);

        if (string.IsNullOrEmpty(runId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BattleController] PersistPlayerHealth skipped: runId missing.");
#endif
            return;
        }

        var db = ServiceRegistry.Get<IDatabase>();
        if (db == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BattleController] PersistPlayerHealth skipped: IDatabase not available.");
#endif
            return;
        }

        try
        {
            db.UpdateRunHp(runId, playerHealth);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BattleController] PersistPlayerHealth failed: {e.Message}");
        }
    }
}

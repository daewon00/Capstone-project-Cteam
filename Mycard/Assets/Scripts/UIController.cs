using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
public class UIController : MonoBehaviour
{
    public static UIController instance;

    private void Awake()
    {
        instance = this;
    }

    public TMP_Text playerManaText, playerHealthText, enemyHealthText, enemyManaText;

    public GameObject manawarning;
    public float manawarningTime;
    private float manawarningCounter;
    public GameObject drawCardButton, endTurnButton;

    public UIDamageIndicator playerDamage, enemyDamage;

    public GameObject battleEndScreen_win, battleEndScreen_lose;
    public TMP_Text battleResultText1, battleResultText2;

    public string mainMenuScene, battleSelectScene;

    public GameObject PauseScreen;
    public GameObject FieldShowButton;
    public GameObject FieldBackButton;

    public GameObject EnemyUI;

    // 드래그 중 임시로 숨길 UI 그룹들(CanvasGroup 사용 권장)
    [Header("Drag Hide UI Groups (CanvasGroup)")]
    [SerializeField] private CanvasGroup _enemyUIGroup;       // EnemyUI 루트에 CanvasGroup 부착 권장
    [SerializeField] private CanvasGroup _playerActionGroup;  // end/draw 버튼을 감싼 부모에 CanvasGroup 부착 권장
    private int _dragHideRefCount = 0;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (manawarningCounter > 0)
        {
            manawarningCounter -= Time.deltaTime;

            if (manawarningCounter <= 0)
            {
                manawarning.SetActive(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PauseUnPause();
        }
    }

    public void SetPlayerManaText(int manaAmount)
    {
        playerManaText.text = "" + manaAmount + "/" + BattleController.instance.playermaxMana;
    }
    public void SetEnemyManaText(int manaAmount)
    {
        enemyManaText.text = "" + manaAmount;
    }

    public void setPlayerHealthText(int healthAmount)
    {
        playerHealthText.text = "" + healthAmount;
    }
    public void setEnemyHealthText(int healthAmount)
    {
        enemyHealthText.text = "" + healthAmount;
    }

    public void ShowManaWarning()
    {
        manawarning.SetActive(true);
        manawarningCounter = manawarningTime;
    }

    public void DrawCard()
    {
        BattleController.instance.AttemptPlayerDraw();
        AudioManager.instance.PlaySFX(0);
    }

    public void EndPlayerTurn()
    {


        BattleController.instance.EndPlayerTurn();

        AudioManager.instance.PlaySFX(0);
    }

    public void AddRelicTest()
    {
        RelicSystem.Instance.AddRelicById("EnemyFirstCardWeakener", stacks: 1);
        Debug.LogWarning("Relic추가됨");

    }

    public void Addknight()
    {
        RelicSystem.Instance.AddRelicById("COMP_COMP_Knight", stacks: 1);
    }

    public void AddWarBanner()
    {
        /*GameObject WarBannerObject = new GameObject("WarBannerItem");
        WarBanner banner = WarBannerObject.AddComponent<WarBanner>();
        Inventory.Instance.AddItem(banner);
        banner.OnAddItem();*/
    }

    public void FieldButton()
    {
        if (FieldBackButton.activeSelf == false)
        {
            CameraController.instance.MoveTo(CameraController.instance.battleTransform);
            endTurnButton.SetActive(false);
            FieldShowButton.SetActive(false);
            FieldBackButton.SetActive(true);
            EnemyUI.SetActive(false);

        }
    }

    public void FieldBack()
    {
        if (FieldShowButton.activeSelf == false)
        {
            CameraController.instance.MoveTo(CameraController.instance.homeTransform);
            endTurnButton.SetActive(true);
            FieldBackButton.SetActive(false);
            FieldShowButton.SetActive(true);

            Invoke("EnableEnemyUI", .4f);
        }
    }

    void EnableEnemyUI()
    {
        EnemyUI.SetActive(true);
    }

    /// <summary>
    /// 카드 드래그 중 UI를 임시로 숨기거나 복원합니다.
    /// SetActive 대신 CanvasGroup으로 투명/상호작용 차단만 적용하여 다른 로직과 충돌을 피합니다.
    /// 여러 곳에서 동시에 호출해도 안전하도록 참조 카운트로 관리합니다.
    /// </summary>
    public void SetDragModeUIVisibility(bool visible)
    {
        if (!visible)
        {
            _dragHideRefCount++;
            if (_dragHideRefCount == 1)
            {
                ApplyGroupVisible(_enemyUIGroup, false);
                ApplyGroupVisible(_playerActionGroup, false);
            }
        }
        else
        {
            _dragHideRefCount = Mathf.Max(0, _dragHideRefCount - 1);
            if (_dragHideRefCount == 0)
            {
                ApplyGroupVisible(_enemyUIGroup, true);
                ApplyGroupVisible(_playerActionGroup, true);
            }
        }
    }

    private static void ApplyGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    /*public void ChAdd()
    {
        // 1) Resources/Characters 폴더 안의 "Cat.asset" 파일 불러오기
        CharacterSO newChar = Resources.Load<CharacterSO>("Characters/Cat");

        if (newChar != null)
        {
            // 2) PortraitInventory에 캐릭터 추가
            PortraitInventory.instance.AddCharacter(newChar);

            Debug.Log(newChar.characterName + " 추가됨!");
        }
        else
        {
            Debug.LogError("캐릭터 ScriptableObject를 찾을 수 없습니다!");
        }
    }*/

    public void CardAdd1()
    {
        // 레거시 DeckController 경로 제거: 이 기능은 신규 덱/보상 시스템으로 대체되어야 합니다.
        Debug.LogWarning("[UIController] CardAdd1은 레거시입니다. 덱 추가는 보상/상점/동료 선택 로직을 통해 처리하세요.");
    }
    
    public void AddRelic()
    {
        //RelicSystem.Instance.AddRelicById("WarBanner", stacks: 1);
    }

    public void Pauseup()
    {
        PauseUnPause();
    }

    public void MainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);

        Time.timeScale = 1f;

        AudioManager.instance.PlaySFX(0);
    }
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        Time.timeScale = 1f;

        AudioManager.instance.PlaySFX(0);
    }
    public void ChooseNewBattle()
    {
        SceneManager.LoadScene(battleSelectScene);

        Time.timeScale = 1f;

        AudioManager.instance.PlaySFX(0);
    }

    public void PauseUnPause()
    {
        if(PauseScreen.activeSelf == false)
        {
            PauseScreen.SetActive(true);

            Time.timeScale = 0f;
        }
        else
        {
            PauseScreen.SetActive(false);
            Time.timeScale = 1f;
        }
        AudioManager.instance.PlaySFX(0);
    }
}

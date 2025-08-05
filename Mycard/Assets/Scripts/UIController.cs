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

    public GameObject battleEndScreen;
    public TMP_Text battleResultText;

    public string mainMenuScene, battleSelectScene;

    public GameObject PauseScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(manawarningCounter > 0)
        {
            manawarningCounter -= Time.deltaTime;

            if(manawarningCounter <= 0)
            {
                manawarning.SetActive(false);
            }
        }

        if(Input.GetKeyDown(KeyCode.Escape))
        {
            PauseUnPause();
        }
    }

    public void SetPlayerManaText(int manaAmount)
    {
        playerManaText.text = "Mana : " + manaAmount;
    }
    public void SetEnemyManaText(int manaAmount)
    {
        enemyManaText.text = "Mana : " + manaAmount;
    }

    public void setPlayerHealthText(int healthAmount)
    {
        playerHealthText.text = "Player HealTh: " + healthAmount;
    }
    public void setEnemyHealthText(int healthAmount)
    {
        enemyHealthText.text = "Enemy HealTh: " + healthAmount;
    }

    public void ShowManaWarning()
    {
        manawarning.SetActive(true);
        manawarningCounter = manawarningTime;
    }

    public void DrawCard()
    {
        DeckController.instance.DrawCardForMana();

        AudioManager.instance.PlaySFX(0);
    }

    public void EndPlayerTurn()
    {


        BattleController.instance.EndPlayerTurn();

        AudioManager.instance.PlaySFX(0);
    }

    public void AddBanana()
    {
        GameObject bananaObject = new GameObject("BananaItem"); //아이템 오브젝트를 새로 만들어줍니다
        Banana bananaItem = bananaObject.AddComponent<Banana>(); //아이템 스크립트를 불러와요
        Inventory.Instance.AddItem(bananaItem);
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

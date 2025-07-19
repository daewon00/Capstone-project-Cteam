using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string battleSelectScene;    //현재 맵이랑 연결되어 있음 나중에는 튜토선택이나 동료선택이 될듯

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioManager.instance.PlayMenuMusic();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartGame()
    {
        SceneManager.LoadScene(battleSelectScene);

        AudioManager.instance.PlaySFX(0);
    }
    public void QuitGame()
    {
        Application.Quit();

        Debug.Log("Quit game");

        AudioManager.instance.PlaySFX(0);
    }
}

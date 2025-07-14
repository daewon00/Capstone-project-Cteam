using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//이 스크립트 자체는 나중에 맵 선택으로 변할 예정 중요한 부분이 아니다.
public class BattleSelectButton : MonoBehaviour
{
    public string levelToLoad;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioManager.instance.PlayBattleSelectMusic();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SelectBattle()
    {
        SceneManager.LoadScene(levelToLoad);

        AudioManager.instance.PlaySFX(0);
    }
}

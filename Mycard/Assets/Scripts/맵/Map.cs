using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 씬 진입 시 배경 음악을 재생하고 전투 씬으로 이동하는 버튼을 제공합니다.
/// </summary>
public class Map : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        AudioManager.instance.PlayMapMusic();
    }

    // Update is called once per frame
    void Update()
    {

    }


    /// <summary>
    /// 기본 전투 씬을 로드하고 효과음을 재생합니다.
    /// </summary>
    public void GoToBattleScene()   //배틀 씬을 불러온다
    {
        SceneManager.LoadScene("Battle");

        AudioManager.instance.PlaySFX(0);
    }
}

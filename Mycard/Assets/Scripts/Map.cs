using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Map : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }


    public void GoToBattleScene()   //배틀 씬을 불러온다
    {
        SceneManager.LoadScene("Battle");

        AudioManager.instance.PlaySFX(0);
    }
}

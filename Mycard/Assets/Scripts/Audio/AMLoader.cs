using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//오디오 로더 실행시 오디오 매니저 실행중인지 확인 후 비실행시 실행
//브금이 특정씬에서 아예 재생이 안될때 제일 먼저 확인해 봐야됨

public class AMLoader : MonoBehaviour
{
    public AudioManager theAM;

    private void Awake()
    {
        if(FindObjectOfType<AudioManager>() == null)
        {
            AudioManager.instance = Instantiate(theAM);
            DontDestroyOnLoad(AudioManager.instance.gameObject);
            //씬 전환시에도 유지
        }
    }
}

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    private void Awake()
    {
        if (instance == null)
        {

            instance = this;

            DontDestroyOnLoad(gameObject);
        } else if(instance != this)
        {
            Destroy(gameObject);
        }
    }

    public AudioSource menuMusic;   //메인 메뉴 BGM
    public AudioSource battleSelectMusic;   //전투 화면 BGM
    public AudioSource[] bgm;   //BGM 목록
    private int currentBGM;     //BGM 인덱스
    private bool playingBGM;    //BGM 목록 재생 참거짓

    public AudioSource[] sfx;   //효과음 목록

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // 프레임 마다 호출 함수
    void Update()
    {
        if(playingBGM)  //BGM 재생시
        {
            if (bgm[currentBGM].isPlaying == false) //BGM 인덱스 재생이 끝나면
            {
                currentBGM++; //BGM 인덱스 증가, 마지막 곡을 넘기면 0번으로
                if(currentBGM >= bgm.Length)
                {
                    currentBGM = 0;
                }

                bgm[currentBGM].Play(); //증가된 인덱스로 BGM 재생
            }
        }
    }

    //BGM 정지 함수
    public void StopMusic()
    {
        menuMusic.Stop();
        battleSelectMusic.Stop();
        foreach(AudioSource track in bgm)
        {
            track.Stop();
        }

        playingBGM = false; //BGM 재생 거짓으로
    }

    //메인 메뉴 BMG 재생
    public void PlayMenuMusic()
    {
        StopMusic();
        menuMusic.Play();
    }

    //배틀 BGM 재생
    public void PlayBattleSelectMusic()
    {
        if (battleSelectMusic.isPlaying == false)
        {
            StopMusic();
            battleSelectMusic.Play();
        }
    }

    //BGM 목록중 랜덤 BGM 재생
    public void PlayBGM()
    {
        StopMusic();

        currentBGM = Random.Range(0,bgm.Length);

        bgm[currentBGM].Play();
        playingBGM = true;
    }

    //효과음 재생
    public void PlaySFX(int sfxToPlay)
    {
        sfx[sfxToPlay].Stop();
        sfx[sfxToPlay].Play();
    }
}

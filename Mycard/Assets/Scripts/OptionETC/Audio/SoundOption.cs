using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SoundOption : MonoBehaviour
{
    public static SoundOption instance;
    public AudioMixer audioMixer;
    public Slider BgmSlider;
    public Slider SfxSlider;
    public Slider MasterSlider;

    public GameObject MasterMixeroption;
    public GameObject MuteOn, MuteOff;
    // Start is called before the first frame update
    /*
    public void AudioControl()
    {
        float sound = BgmSlider.value;

        if (sound == -40f) audioMixer.SetFloat("BGM", -80f);
        else audioMixer.SetFloat("BGM", sound);
    }
    */

    private void Awake()
    {
        if (BgmSlider) BgmSlider.onValueChanged.AddListener(_ => SetBgmVolume()); 
        if (SfxSlider) SfxSlider.onValueChanged.AddListener(_ => SetSFXVolume()); 
        if (MasterSlider) MasterSlider.onValueChanged.AddListener(_ => SetMasterVolume());


        //LoadVolumeSettings(); // 씬 시작할 때 이전 설정 불러오기
    }



    public void SetBgmVolume()
    {
        float db = VolumePrefs.SliderToDb(BgmSlider.value);
        audioMixer.SetFloat("BGM", db);
        VolumePrefs.Save(VolumePrefs.KeyBgm, db);

        /*float bgm = BgmSlider.value;

        if (bgm == -40f) audioMixer.SetFloat("BGM", -80f);
        else audioMixer.SetFloat("BGM", bgm);
        PlayerPrefs.SetFloat("BGM_VOLUME", BgmSlider.value);
        PlayerPrefs.Save();*/

    }

    public void SetSFXVolume()
    {
        float db = VolumePrefs.SliderToDb(SfxSlider.value);
        audioMixer.SetFloat("SFX", db);
        VolumePrefs.Save(VolumePrefs.KeySfx, db);

        /*float sfx = SfxSlider.value;

        if (sfx == -40f) audioMixer.SetFloat("SFX", -80f);
        else audioMixer.SetFloat("SFX", sfx);
        PlayerPrefs.SetFloat("SFX_VOLUME", SfxSlider.value);
        PlayerPrefs.Save();*/

    }

    public void SetMasterVolume()
    {
        float db = VolumePrefs.SliderToDb(MasterSlider.value);
        audioMixer.SetFloat("Master", db);
        VolumePrefs.Save(VolumePrefs.KeyMaster, db);

        bool sliderRequestsMute = db <= -79.5f;
        bool isMuted = VolumePrefs.LoadMute();
        if (sliderRequestsMute != isMuted)
        {
            SetMuted(sliderRequestsMute);
        }

        /*float master = MasterSlider.value;

        if (master == -40f) audioMixer.SetFloat("Master", -80f);
        else audioMixer.SetFloat("Master", master);
        PlayerPrefs.SetFloat("MASTER_VOLUME", MasterSlider.value);
        PlayerPrefs.Save();*/

    }
    private void OnEnable()
    {
        bool muted = VolumePrefs.LoadMute();
        SyncMuteUIAndAudio(muted);


        float bgm = VolumePrefs.Load(VolumePrefs.KeyBgm, 0f);
        float sfx = VolumePrefs.Load(VolumePrefs.KeySfx, 0f);
        float master = VolumePrefs.Load(VolumePrefs.KeyMaster, 0f);

        if (BgmSlider) BgmSlider.SetValueWithoutNotify(bgm <= -80f ? -40f : bgm);
        if (SfxSlider) SfxSlider.SetValueWithoutNotify(sfx <= -80f ? -40f : sfx);
        if (MasterSlider)
        {
            float target = muted ? -40f : (master <= -80f ? -40f : master);
            MasterSlider.SetValueWithoutNotify(target);
        }


        //LoadVolumeSettings();
    }

    public void OnClickMuteOn()
    {
        // 음소거 "켜기"
        SetMuted(true);
    }

    public void OnClickMuteOff()
    {
        // 음소거 "끄기"
        SetMuted(false);
    }

    private void SetMuted(bool muted)
    {
        VolumePrefs.SaveMute(muted);              // 상태 저장
        SyncMuteUIAndAudio(muted);                // 버튼 표시 갱신

        if (!MasterSlider) return;

        if (muted)
        {
            MasterSlider.SetValueWithoutNotify(-40f);
        }
        else
        {
            float master = VolumePrefs.Load(VolumePrefs.KeyMaster, 0f);
            MasterSlider.SetValueWithoutNotify(master <= -80f ? -40f : master);
        }
    }

    private void SyncMuteUIAndAudio(bool muted)
    {
        if (MuteOn) MuteOn.SetActive(!muted); // 음소거 아니면 MuteOn 보이기
        if (MuteOff) MuteOff.SetActive(muted); // 음소거이면 MuteOff 보이기
        AudioListener.volume = muted ? 0f : 1f;            // 안전하게 한 번 더 맞춰줌
    }

    public void ToggleAudioVolume()
    {
        //AudioListener.volume = AudioListener.volume == 0 ? 1 : 0;
        bool willMute = AudioListener.volume > 0.0001f; // 현재 소리가 있으면 → 음소거로
        SetMuted(willMute);
        
    }

    public void MasterMixerExit()
    {
        MasterMixeroption.SetActive(false);
    }

    public void CallMasterMixer()
    {
        MasterMixeroption.SetActive(true);
    }

    /*public void LoadVolumeSettings()
    {
        if (PlayerPrefs.HasKey("BGM_VOLUME"))
        {
            float bgm = PlayerPrefs.GetFloat("BGM_VOLUME");
            BgmSlider.value = bgm;
            audioMixer.SetFloat("BGM", bgm == -40f ? -80f : bgm);
        }

        if (PlayerPrefs.HasKey("SFX_VOLUME"))
        {
            float sfx = PlayerPrefs.GetFloat("SFX_VOLUME");
            SfxSlider.value = sfx;
            audioMixer.SetFloat("SFX", sfx == -40f ? -80f : sfx);
        }

        if (PlayerPrefs.HasKey("MASTER_VOLUME"))
        {
            float master = PlayerPrefs.GetFloat("MASTER_VOLUME");
            MasterSlider.value = master;
            audioMixer.SetFloat("Master", master == -40f ? -80f : master);
        }
    }*/

    
}

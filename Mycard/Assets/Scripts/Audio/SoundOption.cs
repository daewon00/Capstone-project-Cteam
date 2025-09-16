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

        /*float master = MasterSlider.value;

        if (master == -40f) audioMixer.SetFloat("Master", -80f);
        else audioMixer.SetFloat("Master", master);
        PlayerPrefs.SetFloat("MASTER_VOLUME", MasterSlider.value);
        PlayerPrefs.Save();*/

    }
    private void OnEnable()
    {
        float bgm = VolumePrefs.Load(VolumePrefs.KeyBgm, 0f);
        float sfx = VolumePrefs.Load(VolumePrefs.KeySfx, 0f);
        float master = VolumePrefs.Load(VolumePrefs.KeyMaster, 0f);

        if (BgmSlider) BgmSlider.SetValueWithoutNotify(bgm <= -80f ? -40f : bgm);
        if (SfxSlider) SfxSlider.SetValueWithoutNotify(sfx <= -80f ? -40f : sfx);
        if (MasterSlider) MasterSlider.SetValueWithoutNotify(master <= -80f ? -40f : master);


        //LoadVolumeSettings();
    }
    public void ToggleAudioVolume()
    {
        AudioListener.volume = AudioListener.volume == 0 ? 1 : 0;
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

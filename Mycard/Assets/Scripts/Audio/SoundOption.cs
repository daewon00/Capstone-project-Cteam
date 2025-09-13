using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SoundOption : MonoBehaviour
{

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
        LoadVolumeSettings(); // 씬 시작할 때 이전 설정 불러오기
    }
    public void SetBgmVolume()
    {
        float sound1 = BgmSlider.value;

        if (sound1 == -40f) audioMixer.SetFloat("BGM", -80f);
        else audioMixer.SetFloat("BGM", sound1);
        PlayerPrefs.SetFloat("BGM_VOLUME", BgmSlider.value);

    }

    public void SetSFXVolume()
    {
        float sound2 = SfxSlider.value;

        if (sound2 == -40f) audioMixer.SetFloat("SFX", -80f);
        else audioMixer.SetFloat("SFX", sound2);
        PlayerPrefs.SetFloat("SFX_VOLUME", SfxSlider.value);
    }

    public void SetMasterVolume()
    {
        float sound3 = MasterSlider.value;

        if (sound3 == -40f) audioMixer.SetFloat("Master", -80f);
        else audioMixer.SetFloat("Master", sound3);
        PlayerPrefs.SetFloat("MASTER_VOLUME", MasterSlider.value);
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

    void LoadVolumeSettings()
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
    }
}

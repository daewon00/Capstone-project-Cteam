using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

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
    public void SetBgmVolume()
    {
        float sound1 = BgmSlider.value;

        if (sound1 == -40f) audioMixer.SetFloat("BGM", -80f);
        else audioMixer.SetFloat("BGM", sound1);
        
    }

    public void SetSFXVolume()
    {
        float sound2 = SfxSlider.value;

        if (sound2 == -40f) audioMixer.SetFloat("SFX", -80f);
        else audioMixer.SetFloat("SFX", sound2);
    }

    public void SetMasterVolume()
    {
        float sound3 = MasterSlider.value;

        if (sound3 == -40f) audioMixer.SetFloat("Master", -80f);
        else audioMixer.SetFloat("Master", sound3);
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
}

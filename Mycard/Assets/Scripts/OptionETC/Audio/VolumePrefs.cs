using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class VolumePrefs
{
    public const string KeyBgm = "BGM_VOLUME";
    public const string KeySfx = "SFX_VOLUME";
    public const string KeyMaster = "MASTER_VOLUME";
    public const string KeyMute = "GLOBAL_MUTE";
    public static float SliderToDb(float slider) => (slider <= -40f) ? -80f : slider;

    public static void Save(string key, float db)
    {
        PlayerPrefs.SetFloat(key, db);
        PlayerPrefs.Save();
    }

    public static float Load(string key, float fallbackDb = 0f)
        => PlayerPrefs.GetFloat(key, fallbackDb);

    public static void SaveMute(bool isMuted)
    {
        PlayerPrefs.SetInt(KeyMute, isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool LoadMute()
    {
        return PlayerPrefs.GetInt(KeyMute, 0) == 1; // ⺻: Ұ ƴ
    }

    static bool SetParam(AudioMixer m, string name, float v)
    {
        if (!m.SetFloat(name, Mathf.Clamp(v, -80f, 0f)))
        {
            GameLog.Error($"[VolumePrefs] AudioMixer exposed parameter '{name}' ϴ. (AudioMixer νͿ Expose ߴ Ȯ)");
            return false;
        }
        return true;
    }

    /// <summary>  ͼ ϰ,    α׸ ϴ.</summary>
    public static bool ApplyToMixerWithLogs(AudioMixer mixer, string where = "")
    {
        if (mixer == null) { GameLog.Error("[VolumePrefs] AudioMixer  null Դϴ."); return false; }

        float bgm = Load(KeyBgm, 0f);
        float sfx = Load(KeySfx, 0f);
        float master = Load(KeyMaster, 0f);

        bool ok = true;
        ok &= SetParam(mixer, "BGM", bgm);
        ok &= SetParam(mixer, "SFX", sfx);
        ok &= SetParam(mixer, "Master", master);

        if (!string.IsNullOrEmpty(where))
            GameLog.Info($"[VolumePrefs] {where}  (BGM={bgm}, SFX={sfx}, Master={master})");

        return ok;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class VolumePrefs
{
    public const string KeyBgm = "BGM_VOLUME";
    public const string KeySfx = "SFX_VOLUME";
    public const string KeyMaster = "MASTER_VOLUME";

    public static float SliderToDb(float slider) => (slider <= -40f) ? -80f : slider;

    public static void Save(string key, float db)
    {
        PlayerPrefs.SetFloat(key, db);
        PlayerPrefs.Save();
    }

    public static float Load(string key, float fallbackDb = 0f)
        => PlayerPrefs.GetFloat(key, fallbackDb);

    static bool SetParam(AudioMixer m, string name, float v)
    {
        if (!m.SetFloat(name, Mathf.Clamp(v, -80f, 0f)))
        {
            Debug.LogError($"[VolumePrefs] AudioMixer exposed parameter '{name}'가 없습니다. (AudioMixer 인스펙터에서 Expose 했는지 확인)");
            return false;
        }
        return true;
    }

    /// <summary>저장된 볼륨을 믹서에 적용하고, 문제 있으면 경고 로그를 남깁니다.</summary>
    public static bool ApplyToMixerWithLogs(AudioMixer mixer, string where = "")
    {
        if (mixer == null) { Debug.LogError("[VolumePrefs] AudioMixer 참조가 null 입니다."); return false; }

        float bgm = Load(KeyBgm, 0f);
        float sfx = Load(KeySfx, 0f);
        float master = Load(KeyMaster, 0f);

        bool ok = true;
        ok &= SetParam(mixer, "BGM", bgm);
        ok &= SetParam(mixer, "SFX", sfx);
        ok &= SetParam(mixer, "Master", master);

        if (!string.IsNullOrEmpty(where))
            Debug.Log($"[VolumePrefs] {where} 적용됨 (BGM={bgm}, SFX={sfx}, Master={master})");

        return ok;
    }
}
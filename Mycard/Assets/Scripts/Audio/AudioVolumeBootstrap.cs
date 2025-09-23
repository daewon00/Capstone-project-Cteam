using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;


[DefaultExecutionOrder(-10000)]
public class AudioVolumeBootstrap : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    private void Awake()
    {
        AudioListener.volume = 1f; // 혹시 어딘가에서 0으로 놔둔 경우 대비
        VolumePrefs.ApplyToMixerWithLogs(audioMixer, "Awake");
        bool muted = VolumePrefs.LoadMute();
        AudioListener.volume = muted ? 0f : 1f;
        SceneManager.activeSceneChanged += OnSceneChanged;
        // DontDestroyOnLoad(gameObject); // 전 씬 유지가 필요하면 주석 해제
    }

    private void Start()
    {
        // 어떤 Awake에서 기본값을 다시 세팅하는 경우 대비하여 Start에서 재적용
        VolumePrefs.ApplyToMixerWithLogs(audioMixer, "Start");
        bool muted = VolumePrefs.LoadMute();
        AudioListener.volume = muted ? 0f : 1f;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        VolumePrefs.ApplyToMixerWithLogs(audioMixer, $"SceneChanged -> {newScene.name}");
    }
}
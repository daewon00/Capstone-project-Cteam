using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 전투 씬을 미리 Additive 로드해두고 필요 시 즉시 활성화할 수 있도록 관리하는 매니저입니다.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class BattlePreloadManager : MonoBehaviour
{
    public enum State
    {
        None,
        Loading,
        Ready,
        Activating,
        Active
    }

    private static BattlePreloadManager _instance;
    public static BattlePreloadManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<BattlePreloadManager>();
            if (_instance != null) return _instance;
            var go = new GameObject(nameof(BattlePreloadManager));
            _instance = go.AddComponent<BattlePreloadManager>();
            return _instance;
        }
    }

    [SerializeField] private State _state = State.None;
    [SerializeField] private string _sceneName;

    private AsyncOperation _loadOperation;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 배틀이 끝난 뒤 단일 모드로 맵이 다시 로드되면 상태를 초기화합니다.
        if (mode == LoadSceneMode.Single)
        {
            _state = State.None;
            _sceneName = null;
            _loadOperation = null;
            return;
        }

        if (mode == LoadSceneMode.Additive && !string.IsNullOrEmpty(_sceneName) && scene.name == _sceneName)
        {
            // Additive 전환 완료 시 상태를 Active로 갱신
            _state = State.Active;
        }
    }

    /// <summary>
    /// 지정한 전투 씬을 미리 로드하도록 요청합니다.
    /// </summary>
    public void EnsurePreloadStarted(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (_state == State.Active && string.Equals(_sceneName, sceneName, StringComparison.Ordinal)) return;
        if (_state == State.Loading || _state == State.Ready)
        {
            if (string.Equals(_sceneName, sceneName, StringComparison.Ordinal)) return;
            // 다른 씬을 로드하려는 경우 기존 프리로드를 정리합니다.
            StartCoroutine(CancelPreloadRoutine());
        }
        _sceneName = sceneName;
        if (_state == State.None)
        {
            StartCoroutine(PreloadRoutine(sceneName));
        }
    }

    /// <summary>
    /// 미리 로드된 씬이 준비되어 있다면 즉시 활성화를 시도합니다.
    /// </summary>
    /// <param name="sceneName">활성화하려는 전투 씬 이름</param>
    /// <param name="mapSceneName">현재 맵 씬 이름(활성화 후 언로드용)</param>
    /// <returns>즉시 활성화를 시작했다면 true, 아니면 false</returns>
    public bool TryActivatePreloadedScene(string sceneName, string mapSceneName)
    {
        if (_state != State.Ready) return false;
        if (!string.Equals(_sceneName, sceneName, StringComparison.Ordinal)) return false;
        if (_loadOperation == null) return false;

        StartCoroutine(ActivateRoutine(sceneName, mapSceneName));
        return true;
    }

    /// <summary>
    /// 프리로드 진행 중 상태를 강제로 초기화합니다.
    /// </summary>
    public void CancelPreload()
    {
        StartCoroutine(CancelPreloadRoutine());
    }

    private IEnumerator PreloadRoutine(string sceneName)
    {
        _state = State.Loading;
        _loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (_loadOperation == null)
        {
            _state = State.None;
            yield break;
        }
        _loadOperation.allowSceneActivation = false;
        // 0.9f에 도달할 때까지 대기(로딩 완료 직전)
        while (_loadOperation.progress < 0.9f)
        {
            yield return null;
        }
        _state = State.Ready;
    }

    private IEnumerator ActivateRoutine(string sceneName, string mapSceneName)
    {
        if (_loadOperation == null)
        {
            _state = State.None;
            yield break;
        }
        _state = State.Activating;
        DisableCurrentEventSystem();
        _loadOperation.allowSceneActivation = true;
        while (!_loadOperation.isDone)
        {
            yield return null;
        }
        _loadOperation = null;
        _state = State.Active;

        // 새로운 전투 씬을 활성화합니다.
        var battleScene = SceneManager.GetSceneByName(sceneName);
        if (battleScene.IsValid())
        {
            SceneManager.SetActiveScene(battleScene);
        }

        // 기존 맵 씬을 언로드하여 중복 실행을 방지합니다.
        if (!string.IsNullOrEmpty(mapSceneName))
        {
            var mapScene = SceneManager.GetSceneByName(mapSceneName);
            if (mapScene.IsValid())
            {
                SceneManager.UnloadSceneAsync(mapScene);
            }
        }
    }

    private IEnumerator CancelPreloadRoutine()
    {
        if (_loadOperation != null)
        {
            // allowSceneActivation을 true로 돌려 완료시킨 뒤 언로드합니다.
            _loadOperation.allowSceneActivation = true;
            while (!_loadOperation.isDone)
            {
                yield return null;
            }
            if (!string.IsNullOrEmpty(_sceneName))
            {
                var scene = SceneManager.GetSceneByName(_sceneName);
                if (scene.IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }
        _loadOperation = null;
        _sceneName = null;
        _state = State.None;
    }

    private static void DisableCurrentEventSystem()
    {
        var currentEventSystem = EventSystem.current;
        if (currentEventSystem == null) return;
        currentEventSystem.SetSelectedGameObject(null);
        currentEventSystem.enabled = false;
    }
}

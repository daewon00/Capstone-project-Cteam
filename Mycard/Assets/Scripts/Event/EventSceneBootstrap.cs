using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Save; // EventChoiceDTO를 사용하기 위함
using UnityEngine.SceneManagement;

/// <summary>
/// 이벤트 씬에서 UI를 구성하고 선택 결과를 적용한 뒤 다음 씬으로 전환합니다.
/// </summary>
public class EventSceneBootstrap : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TMP_Text descriptionText; // 1. 이벤트 설명 텍스트
    [SerializeField] private RectTransform choicesParent; // 선택지 버튼이 배치될 부모
    [SerializeField] private Button choiceButtonTemplate;  // 동적 선택지 생성에 사용할 버튼 템플릿
    [SerializeField, Min(0f)] private float buttonSpacing = 16f;

    [Header("캠프파이어 UI")]
    [SerializeField] private DeckUpgradeSelectionPanel upgradeSelectionPanel;

    [Header("씬 이름/기본값")]
    [SerializeField] private string mapSceneName = "Map Scene"; // 하드코딩 제거
    [SerializeField] private string fallbackEventId = "GoldenIdolEvent";


    private IEventManager _eventManager;
    private EventSessionDTO _currentSession;
    private bool _isResolving; // 중복 입력을 막기 위한 '잠금 장치'
    private RunStagePayloads.Event _eventStageCache;
    private readonly List<Button> _spawnedButtons = new();

    /// <summary>
    /// 이벤트 매니저를 확보하고 세션을 불러와 UI를 채웁니다.
    /// </summary>
    void Awake()
    {
        if (choiceButtonTemplate == null)
        {
            Debug.LogError("[EventScene] choiceButtonTemplate이 설정되지 않았습니다.");
            return;
        }

        if (choicesParent == null)
        {
            choicesParent = choiceButtonTemplate.transform.parent as RectTransform;
        }

        choiceButtonTemplate.gameObject.SetActive(false);

        if (upgradeSelectionPanel != null)
        {
            upgradeSelectionPanel.HideImmediate();
        }
    }

    void Start()
    {
        // EventManager가 없으면 안전하게 맵으로 돌아갑니다.
        try { _eventManager = ServiceRegistry.GetRequired<IEventManager>(); }
        catch (System.Exception e)
        {
            Debug.LogError($"[EventScene] EventManager가 없습니다: {e.Message}");
            SafeGoMap();
            return;
        }

        // EventManager에게 현재 진행 중인 이벤트 정보를 요청합니다.
        // "fallbackEventId"은 혹시 모를 비상 상황을 대비한 기본값입니다.
        _currentSession = _eventManager.LoadActiveOrCreate(fallbackEventId);
        if (_currentSession == null)
        {
            Debug.LogError("[EventScene] 세션을 불러올 수 없습니다.");
            SafeGoMap();
            return;
        }

        // ★ 예외 처리: 이미 해결된 이벤트라면 즉시 맵으로 복귀합니다.
        if (_currentSession.resolved)
        {
            Debug.LogWarning("[EventScene] 이미 해결된 이벤트 세션입니다. 맵으로 복귀합니다.");
            SafeGoMap();
            return;
        }

        // 받아온 정보로 UI를 채웁니다.
        BindUI();
        MarkStageAsEvent();
    }

    /// <summary>
    /// 현재 이벤트 세션에 맞춰 설명과 선택지 버튼을 갱신합니다.
    /// </summary>
    private void BindUI()
    {

        // 1. 설명 텍스트를 채웁니다.
        descriptionText.text = _currentSession.description ?? "";
        Debug.Log($"[EventScene] Stage '{_currentSession.stageId}' 로딩 (choices={_currentSession.choices?.Length ?? 0})");
        ClearChoiceButtons();

        var choices = _currentSession.choices;
        if (choices == null || choices.Length == 0)
        {
            Debug.LogWarning("[EventScene] 선택지가 없는 이벤트 세션입니다. 맵으로 복귀합니다.");
            SafeGoMap();
            return;
        }

        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var button = CreateChoiceButton(choice, i);
            if (button != null)
            {
                _spawnedButtons.Add(button);
            }
        }
    }

    /// <summary>
    /// 선택지 클릭 시 중복 입력을 막고 결과를 적용한 뒤 맵 씬으로 복귀합니다.
    /// </summary>
    private void OnChoicePicked(EventChoiceDTO choice)
    {
        Debug.Log($"[EventScene] OnChoicePicked choiceId={choice?.id}", this);
        if (_isResolving) return; // 중복 클릭 방지
        _isResolving = true;

        foreach (var button in _spawnedButtons)
        {
            if (button != null)
            {
                button.interactable = false;
            }
        }

        var shouldReturn = _eventManager.ApplyChoice(_currentSession, choice);
        Debug.Log($"[EventScene] ApplyChoice result shouldReturn={shouldReturn}", this);

        if (shouldReturn)
        {
            SafeGoMap();
            return;
        }

        // ReturnToMap 효과가 없으면 이벤트 씬을 유지합니다.
        var activeSession = _eventManager.TryLoadActive();
        if (activeSession != null)
        {
            _currentSession = activeSession;
            Debug.Log("[EventScene] ReturnToMap 효과가 없어 이벤트 씬을 유지합니다. 선택지를 갱신합니다.");
            BindUI();
            _isResolving = false;
            return;
        }
        else
        {
            Debug.LogWarning("[EventScene] 활성 이벤트 세션이 없어 맵으로 복귀합니다.");
            SafeGoMap();
            return;
        }
    }
    
    // 씬 전환을 위한 안전한 함수
    /// <summary>
    /// 런 단계 정보를 업데이트하고 안전하게 맵 씬으로 전환합니다.
    /// </summary>
    private void SafeGoMap()
    {
        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            RunStagePayloads.Location payload = null;

            if (_eventStageCache != null)
            {
                payload = new RunStagePayloads.Location
                {
                    act = _eventStageCache.act,
                    floor = _eventStageCache.floor,
                    nodeIndex = _eventStageCache.nodeIndex
                };
            }
            else
            {
                var runId = PlayerPrefs.GetString("lastRunId", string.Empty);
                var runData = string.IsNullOrEmpty(runId) ? null : DatabaseManager.Instance.LoadCurrentRun(runId);
                if (runData?.Run != null)
                {
                    payload = new RunStagePayloads.Location
                    {
                        act = runData.Run.Act,
                        floor = runData.Run.Floor,
                        nodeIndex = runData.Run.NodeIndex
                    };
                }
            }

            stageService.SetStage(RunStageType.Map, mapSceneName, payload != null ? RunStageService.ToJson(payload) : null);
        }

        if (!string.IsNullOrEmpty(mapSceneName))
            SceneManager.LoadScene(mapSceneName);
        else
            Debug.LogError("[EventScene] mapSceneName이 비어있어 씬 전환이 불가합니다.");
    }

    /// <summary>
    /// 런 스테이지 서비스에 현재 이벤트 진행 상황을 기록합니다.
    /// </summary>
    private void MarkStageAsEvent()
    {
        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService == null) return;

        if (!stageService.TryGetPayload(out _eventStageCache) || _eventStageCache == null)
        {
            _eventStageCache = new RunStagePayloads.Event();
            var runId = PlayerPrefs.GetString("lastRunId", string.Empty);
            var runData = string.IsNullOrEmpty(runId) ? null : DatabaseManager.Instance.LoadCurrentRun(runId);
            if (runData?.Run != null)
            {
                _eventStageCache.act = runData.Run.Act;
                _eventStageCache.floor = runData.Run.Floor;
                _eventStageCache.nodeIndex = runData.Run.NodeIndex;
            }
        }

        if (_currentSession != null && !string.IsNullOrEmpty(_currentSession.eventId))
        {
            _eventStageCache.eventId = _currentSession.eventId;
        }

        stageService.SetStage(RunStageType.Event, SceneManager.GetActiveScene().name, RunStageService.ToJson(_eventStageCache));
    }

    private void ClearChoiceButtons()
    {
        foreach (var button in _spawnedButtons)
        {
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            Destroy(button.gameObject);
        }
        _spawnedButtons.Clear();
    }

    private Button CreateChoiceButton(EventChoiceDTO choice, int index)
    {
        if (choiceButtonTemplate == null)
        {
            Debug.LogError("[EventScene] choiceButtonTemplate이 설정되어 있지 않아 선택지를 생성할 수 없습니다.");
            return null;
        }

        var parent = choicesParent != null ? choicesParent : choiceButtonTemplate.transform.parent as RectTransform;
        if (parent == null)
        {
            Debug.LogError("[EventScene] 선택지 부모 RectTransform을 찾을 수 없습니다.");
            return null;
        }

        var button = Instantiate(choiceButtonTemplate, parent);
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        if (RequiresUpgradeSelection(choice))
        {
            button.onClick.AddListener(() => BeginUpgradeSelection(choice));
        }
        else
        {
            button.onClick.AddListener(() => OnChoicePicked(choice));
        }

        var buttonRect = button.transform as RectTransform;
        var templateRect = choiceButtonTemplate.transform as RectTransform;
        if (buttonRect != null && templateRect != null)
        {
            ApplyButtonTransform(templateRect, buttonRect, index);
        }

        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = choice.label ?? string.Empty;
        }

        button.gameObject.name = string.IsNullOrEmpty(choice.id) ? "Choice" : $"Choice_{choice.id}";
        return button;
    }

    private bool RequiresUpgradeSelection(EventChoiceDTO choice)
    {
        if (choice?.effects == null || choice.effects.Length == 0)
            return false;

        return choice.effects.Any(effect => effect != null && effect.type == EventEffectType.UpgradeRandomCard);
    }

    private void BeginUpgradeSelection(EventChoiceDTO choice)
    {
        if (upgradeSelectionPanel == null)
        {
            Debug.LogWarning("[EventScene] upgradeSelectionPanel이 없어 랜덤 강화로 대체합니다.");
            OnChoicePicked(choice);
            return;
        }

        if (_isResolving)
            return;

        SetChoiceButtonsInteractable(false);

        bool opened = upgradeSelectionPanel.Show(
            onConfirm: state =>
            {
                Debug.Log($"[EventScene] UpgradeConfirm instance={(state != null ? state.InstanceId : "null")}", this);
                if (state != null)
                {
                    _eventManager?.QueueUpgradeSelection(state.InstanceId);
                }
                _isResolving = false;
                SetChoiceButtonsInteractable(true);
                OnChoicePicked(choice);
            },
            onCancel: () =>
            {
                Debug.Log("[EventScene] UpgradeCancel", this);
                _isResolving = false;
                SetChoiceButtonsInteractable(true);
            });

        if (!opened)
        {
            _isResolving = false;
            SetChoiceButtonsInteractable(true);
            OnChoicePicked(choice);
        }
        else
        {
            Debug.Log("[EventScene] UpgradeSelectionPanel opened", this);
            _isResolving = true;
        }
    }

    private void SetChoiceButtonsInteractable(bool interactable)
    {
        foreach (var button in _spawnedButtons)
        {
            if (button == null) continue;
            button.interactable = interactable;
        }
    }

    private void ApplyButtonTransform(RectTransform templateRect, RectTransform buttonRect, int index)
    {
        buttonRect.anchorMin = templateRect.anchorMin;
        buttonRect.anchorMax = templateRect.anchorMax;
        buttonRect.pivot = templateRect.pivot;
        buttonRect.sizeDelta = templateRect.sizeDelta;
        buttonRect.localScale = templateRect.localScale;

        var spacing = templateRect.sizeDelta.y + buttonSpacing;
        buttonRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -index * spacing);
    }

}

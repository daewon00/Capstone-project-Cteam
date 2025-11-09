using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Save;
using UnityEngine.SceneManagement; // SceneManager를 사용하기 위해 추가

/// <summary>
/// 맵 노드 이동과 상점/이벤트/전투 씬 전환을 관리하고 런 진행 정보를 DB와 동기화합니다.
/// </summary>
public class MapTraversalController : MonoBehaviour
{
    [Header("Marker")]
    public Transform playerMarker; // 현재 위치 마커(없으면 표시만 생략)
    [SerializeField] private float markerMoveDuration = 0.6f;
    [SerializeField] private Ease markerEase = Ease.InOutSine;
    [SerializeField] private bool allowMarkerSkip = true;

    Dictionary<(int floor, int index), NodeGoScene> _nodes;
    string _runId;
    CurrentRun _run;
    private bool _isMoving = false; // 노드 이동 중 재진입 방지
    private Tween _markerTween;
    private PendingStageOperation _pendingOperation;

    private sealed class PendingStageOperation
    {
        public NodeGoScene Target;
        public bool IsMoveToChild;
        public bool IsReclickSameNode;
        public RunStagePayloads.Location LocationPayload;
        public MapNodeState PendingVisited;
        public int PreviousFloor;
        public int PreviousNodeIndex;
        public int PreviousAct;
        public RunStagePayloads.Event EventPayload;
        public EventSessionDTO PreparedEventSession;
        public RunStagePayloads.Shop ShopPayload;
        public RunStagePayloads.Battle BattlePayload;
        public string BattleSceneToLoad;
        public GameContext.BattleKind? PreparedBattleKind;
        public bool TriggerAssignedScene;
        public GameContext.BattleKind PreviousBattleKind;
        public bool ShouldPersistPosition = true;
    }

    [SerializeField] private ShopOverlayController _shopOverlay; //상점 오버레이 저장
    [SerializeField] private string eventSceneName = "Event";            // 공통 이벤트 씬 이름
    [SerializeField] private string defaultEventId = "GoldenIdolEvent"; // 기본 이벤트 ID
    [SerializeField] private string battleSceneName = "Battle_android"; // 전투 씬의 이름을 에디터에서 설정


    void Awake()
    {
        // 게임 시작 시 상점 오버레이를 한 번만 찾아둡니다.
        _shopOverlay = FindObjectOfType<ShopOverlayController>(true);
    }
    


    void Start()
    {
        DatabaseManager.Instance.Connect();

        _runId = PlayerPrefs.GetString("lastRunId", "");
        var data = string.IsNullOrEmpty(_runId) ? null : DatabaseManager.Instance.LoadCurrentRun(_runId);
        if (data == null) { Debug.LogError("[Traversal] 런 로드 실패"); return; }

        _run = data.Run;

        Debug.Log($"[MapTraversalController] Start runId={_runId}, act={_run.Act}, floor={_run.Floor}, nodeIndex={_run.NodeIndex}");

        // 씬의 모든 노드 수집
        var list = FindObjectsByType<NodeGoScene>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        _nodes = list.ToDictionary(n => (n.floor, n.index), n => n);
        RegisterNodeTargets();

        // 초기 표시
        PlaceMarker(_run.Floor, _run.NodeIndex);
        UpdateReachable(_run.Floor, _run.NodeIndex);

        // --- 전투 보상 처리: 현재 노드에 RewardsJson이 있으면 보상 UI를 트리거합니다. ---
        try
        {
            var currentNode = data.Nodes?.FirstOrDefault(n =>
                n.Act == _run.Act && n.Floor == _run.Floor && n.NodeIndex == _run.NodeIndex);
            TriggerRewardUIIfNeeded(currentNode);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MapTraversalController] 보상 처리 중 오류: {e.Message}");
        }

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null)
        {
            var locationPayload = new RunStagePayloads.Location
            {
                act = _run.Act,
                floor = _run.Floor,
                nodeIndex = _run.NodeIndex
            };

            var currentStage = stageService.Current;
            Debug.Log($"[MapTraversalController] Stage on entry: {(currentStage != null ? currentStage.Stage.ToString() : "(null)")}, sceneHint='{currentStage?.SceneHint}'");
            if (currentStage == null)
            {
                stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
            }
            else
            {
                switch (currentStage.Stage)
                {
                    case RunStageType.BattlePending:
                        if (!TryRestorePreviousMapPosition(stageService, currentStage.PayloadJson, locationPayload))
                        {
                            stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
                        }
                        break;
                    case RunStageType.Battle:
                    {
                        var resumeScene = !string.IsNullOrEmpty(currentStage.SceneHint)
                            ? currentStage.SceneHint
                            : battleSceneName;
                        bool payloadParsed = RunStageService.TryParse(currentStage.PayloadJson, out RunStagePayloads.Battle pendingPayload) && pendingPayload != null;
                        if (payloadParsed && !string.IsNullOrEmpty(pendingPayload.sceneName))
                        {
                            resumeScene = pendingPayload.sceneName;
                        }

                        var currentSceneName = SceneManager.GetActiveScene().name;
                        bool resumeLooksBattle = !string.IsNullOrEmpty(resumeScene) &&
                            resumeScene.IndexOf("Battle", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        bool sameScene = string.Equals(resumeScene, currentSceneName, System.StringComparison.Ordinal);

                        if (!resumeLooksBattle || sameScene)
                        {
                            Debug.Log("[MapTraversalController] Stage reported Battle but resume scene is invalid or current. Attempting to restore previous map position.");

                            if (!payloadParsed || !TryRestorePreviousMapPosition(stageService, currentStage.PayloadJson, locationPayload))
                            {
                                stageService.SetStage(RunStageType.Map, currentSceneName, RunStageService.ToJson(locationPayload));
                            }
                            break;
                        }

                        RunCacheSynchronizer.Sync();
                        SceneManager.LoadScene(resumeScene);
                        return;
                    }
                    case RunStageType.Map:
                    case RunStageType.Unknown:
                        stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
                        break;
                    case RunStageType.ShopOverlay:
                        if (stageService.TryGetPayload(out RunStagePayloads.Shop shopPayload))
                        {
                            if (shopPayload.floor == _run.Floor && shopPayload.nodeIndex == _run.NodeIndex)
                            {
                                _shopOverlay?.OpenForNode(_run.Floor, _run.NodeIndex);
                            }
                        }
                        break;
                    case RunStageType.Reward:
                        // 보상 수령 전이면 그대로 유지하여 TriggerRewardUIIfNeeded가 처리하게 둡니다.
                        break;
                    case RunStageType.Event:
                        // 이벤트 씬에서 돌아왔으면 맵 상태로 정리.
                        stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
                        break;
                }
            }
        }

        var stageAfterInit = stageService?.Current?.Stage.ToString() ?? "null";
        Debug.Log($"[MapTraversalController] Stage after Start: {stageAfterInit}, act={_run.Act}, floor={_run.Floor}, node={_run.NodeIndex}");

        // 전투 돌입 시 로딩 지연을 줄이기 위해 전투 씬 프리로드를 시작합니다.
        ServiceRegistry.Get<RunStatOverlay>()?.RefreshFallback();
        StartCoroutine(DeferredBattlePreload());
    }

    private void StartMarkerTween(IRunStageService stageService)
    {
        if (_pendingOperation == null)
        {
            FinalizePendingOperation(stageService);
            return;
        }

        ClearMarkerTween();

        var targetNode = _pendingOperation.Target;
        if (targetNode == null)
        {
            FinalizePendingOperation(stageService);
            return;
        }

        var markerRect = playerMarker as RectTransform;
        if (markerRect != null)
        {
            if (TryComputeAnchoredPosition(targetNode, markerRect, out var anchoredPosition))
            {
                markerRect.SetAsLastSibling();
                _markerTween = DOTween.To(() => markerRect.anchoredPosition, v => markerRect.anchoredPosition = v, anchoredPosition, markerMoveDuration);
                _markerTween.SetEase(markerEase);
                _markerTween.SetUpdate(UpdateType.Normal, false);
                _markerTween.SetTarget(markerRect);
                _markerTween.OnComplete(() =>
                {
                    _markerTween = null;
                    FinalizePendingOperation(stageService);
                });
                return;
            }
        }

        _markerTween = playerMarker.DOMove(targetNode.transform.position, markerMoveDuration);
        _markerTween.SetEase(markerEase);
        _markerTween.SetUpdate(UpdateType.Normal, false);
        _markerTween.OnComplete(() =>
        {
            _markerTween = null;
            FinalizePendingOperation(stageService);
        });
    }

    private void RegisterNodeTargets()
    {
        var tutorialService = ServiceRegistry.Get<ITutorialService>();
        if (tutorialService == null || _nodes == null) return;

        foreach (var node in _nodes.Values)
        {
            if (node == null) continue;
            var target = node.GetComponent<TutorialTarget>() ?? node.gameObject.AddComponent<TutorialTarget>();
            // 씬에서 미리 지정된 ID가 있다면 유지합니다.
            if (string.IsNullOrEmpty(target.TargetId))
            {
                target.SetId($"map-node-{node.floor}-{node.index}");
            }
            // FocusRect는 작성자가 별도 지정하지 않았다면(기본 transform) 그대로 두어도 동일 동작
            // 필요 시 명시적으로 지정
            target.SetFocusRect(node.transform as RectTransform);
        }
    }

    private void FinalizePendingOperation(IRunStageService stageService)
    {
        try
        {
            if (_pendingOperation == null)
            {
                return;
            }

            var operation = _pendingOperation;

            IDatabase db = null;
            if (operation.PendingVisited != null || operation.ShouldPersistPosition)
            {
                try
                {
                    db = ServiceRegistry.GetRequired<IDatabase>();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MapTraversalController] DB 조회 중 오류: {e.Message}");
                }
            }

            if (operation.PendingVisited != null && db != null)
            {
                try
                {
                    db.UpsertNodeState(operation.PendingVisited);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MapTraversalController] 노드 상태 저장 중 오류: {e.Message}");
                }
            }

            if (operation.ShouldPersistPosition && db != null)
            {
                try
                {
                    db.UpdateRunPosition(_run.RunId, _run.Act, _run.Floor, _run.NodeIndex);
                    ServiceRegistry.Get<ITutorialService>()?.NotifyMapNodeVisited(_run.Act, _run.Floor, _run.NodeIndex);

                    if (operation.PendingVisited != null && operation.PreviousFloor != operation.PendingVisited.Floor)
                    {
                        try
                        {
                            MetaEvents.RaiseFloorReached(new MetaEvents.FloorReachedPayload
                            {
                                RunId = _run.RunId,
                                Act = _run.Act,
                                Floor = _run.Floor
                            });
                        }
                        catch { }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MapTraversalController] 위치 저장 중 오류: {e.Message}");
                }
            }

            PlaceMarker(_run.Floor, _run.NodeIndex);
            UpdateReachable(_run.Floor, _run.NodeIndex);
            HandleStageOperation(operation, stageService);
        }
        finally
        {
            ClearMarkerTween();
            _pendingOperation = null;
            _isMoving = false;
        }
    }

    private void HandleStageOperation(PendingStageOperation operation, IRunStageService stageService)
    {
        var target = operation.Target;
        if (target == null)
        {
            return;
        }

        Debug.Log($"--- Final Action --- Deciding action for node type: {target.nodeType}");

        var locationPayload = operation.LocationPayload ?? new RunStagePayloads.Location
        {
            act = _run.Act,
            floor = _run.Floor,
            nodeIndex = _run.NodeIndex
        };

        if (target.nodeType == NodeType.Shop)
        {
            var shopPayload = operation.ShopPayload ?? new RunStagePayloads.Shop
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex
            };

            stageService?.SetStage(RunStageType.ShopOverlay, SceneManager.GetActiveScene().name, RunStageService.ToJson(shopPayload));
            _shopOverlay?.OpenForNode(_run.Floor, _run.NodeIndex);
            return;
        }

        if (target.nodeType == NodeType.Event || target.nodeType == NodeType.Rest || target.nodeType == NodeType.CardRemove)
        {
            try
            {
                var em = ServiceRegistry.GetRequired<IEventManager>();

                if (operation.IsReclickSameNode && !operation.IsMoveToChild)
                {
                    var activeSession = em.TryLoadActive();
                    if (activeSession != null)
                    {
                        stageService?.SetStage(RunStageType.Event, eventSceneName, stageService?.Current?.PayloadJson);
                        RunCacheSynchronizer.Sync();
                        SceneManager.LoadScene(eventSceneName);
                    }
                    return;
                }

                if (operation.EventPayload != null)
                {
                    stageService?.SetStage(RunStageType.Event, eventSceneName, RunStageService.ToJson(operation.EventPayload));
                    RunCacheSynchronizer.Sync();
                    SceneManager.LoadScene(eventSceneName);
                }
                else
                {
                    Debug.LogWarning("[MapTraversalController] 이벤트 페이로드가 준비되지 않아 이벤트 씬으로 이동하지 않습니다.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapTraversalController] EventManager 처리 중 오류: {e.Message}");
            }
            return;
        }

        if (target.nodeType == NodeType.Battle || target.nodeType == NodeType.Elite || target.nodeType == NodeType.Boss)
        {
            var battlePayload = operation.BattlePayload;
            var sceneToLoad = operation.BattleSceneToLoad ?? battleSceneName;

            if (battlePayload == null)
            {
                battlePayload = new RunStagePayloads.Battle
                {
                    act = locationPayload.act,
                    floor = locationPayload.floor,
                    nodeIndex = locationPayload.nodeIndex,
                    battleKind = (int)(operation.PreparedBattleKind ?? GameContext.BattleKind.Normal),
                    sceneName = sceneToLoad,
                    isPending = true,
                    prevAct = operation.PreviousAct,
                    prevFloor = operation.PreviousFloor,
                    prevNodeIndex = operation.PreviousNodeIndex,
                    prevBattleKind = (int)operation.PreviousBattleKind,
                    hasPrevLocation = true
                };
            }
            else
            {
                battlePayload.sceneName = sceneToLoad;
                battlePayload.isPending = true;
                battlePayload.prevAct = operation.PreviousAct;
                battlePayload.prevFloor = operation.PreviousFloor;
                battlePayload.prevNodeIndex = operation.PreviousNodeIndex;
                battlePayload.prevBattleKind = (int)operation.PreviousBattleKind;
                battlePayload.hasPrevLocation = true;
            }

            Debug.Log($"[MapTraversalController] Battle pending -> prev act={battlePayload.prevAct}, floor={battlePayload.prevFloor}, index={battlePayload.prevNodeIndex}, kind={battlePayload.prevBattleKind}, scene={sceneToLoad}");
            operation.BattlePayload = battlePayload;

            stageService?.SetStage(RunStageType.BattlePending, sceneToLoad, RunStageService.ToJson(battlePayload));
            ServiceRegistry.Get<IDatabase>()?.DeleteActiveBattleState(_run.RunId);
            RunCacheSynchronizer.Sync();
            if (TryEnterBattleViaPreload(sceneToLoad)) return;
            SceneManager.LoadScene(sceneToLoad);
            return;
        }

        if (operation.IsMoveToChild && operation.TriggerAssignedScene)
        {
            stageService?.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
            target.GoToAssignedScene();
            return;
        }

        if (!operation.IsMoveToChild)
        {
            Debug.Log("<color=orange>WARNING: No action taken. isMoveToChild was false for a non-shop/event node.</color>");
        }
    }

    private PendingStageOperation CreatePendingOperation(NodeGoScene target, bool isMoveToChild, bool isReclickSameNode)
    {
        if (_run == null)
        {
            Debug.LogError("[MapTraversalController] 런 데이터가 초기화되지 않았습니다.");
            return null;
        }

        var operation = new PendingStageOperation
        {
            Target = target,
            IsMoveToChild = isMoveToChild,
            IsReclickSameNode = isReclickSameNode
        };

        var locationPayload = new RunStagePayloads.Location
        {
            act = _run.Act,
            floor = _run.Floor,
            nodeIndex = _run.NodeIndex
        };

        if (isMoveToChild)
        {
            operation.PreviousFloor = _run.Floor;
            operation.PreviousNodeIndex = _run.NodeIndex;
            operation.PreviousAct = _run.Act;
            var currentBattleKind = GameContext.I != null
                ? GameContext.I.CurrentBattleKind
                : (GameContext.BattleKind)PlayerPrefs.GetInt("currentBattleKind", (int)GameContext.BattleKind.Normal);
            operation.PreviousBattleKind = currentBattleKind;
            _run.Floor = target.floor;
            _run.NodeIndex = target.index;
            _run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");

            locationPayload.floor = _run.Floor;
            locationPayload.nodeIndex = _run.NodeIndex;

            operation.PendingVisited = new MapNodeState
            {
                RunId = _run.RunId,
                Act = _run.Act,
                Floor = target.floor,
                NodeIndex = target.index,
                Type = (Game.Save.NodeType)target.nodeType,
                Visited = true
            };

            PrepareStageData(operation, locationPayload);
        }

        operation.LocationPayload = locationPayload;
        return operation;
    }

    private void PrepareStageData(PendingStageOperation operation, RunStagePayloads.Location locationPayload)
    {
        var target = operation.Target;
        if (target == null) return;

        if (target.nodeType == NodeType.Shop)
        {
            operation.ShopPayload = new RunStagePayloads.Shop
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex
            };
            return;
        }

        if (target.nodeType == NodeType.Event || target.nodeType == NodeType.Rest || target.nodeType == NodeType.CardRemove)
        {
            var session = PrepareEventSession(target);
            if (session != null)
            {
                operation.EventPayload = new RunStagePayloads.Event
                {
                    act = locationPayload.act,
                    floor = locationPayload.floor,
                    nodeIndex = locationPayload.nodeIndex,
                    eventId = session.eventId
                };
                operation.PreparedEventSession = session;
            }
            return;
        }

        if (target.nodeType == NodeType.Battle || target.nodeType == NodeType.Elite || target.nodeType == NodeType.Boss)
        {
            var kind = GameContext.BattleKind.Normal;
            if (target.nodeType == NodeType.Elite) kind = GameContext.BattleKind.Elite;
            else if (target.nodeType == NodeType.Boss) kind = GameContext.BattleKind.Boss;
            else if (!string.IsNullOrEmpty(target.assignedScene))
            {
                var hint = target.assignedScene.ToLowerInvariant();
                if (hint.Contains("boss")) kind = GameContext.BattleKind.Boss;
                else if (hint.Contains("elite")) kind = GameContext.BattleKind.Elite;
            }

            if (GameContext.I != null) GameContext.I.CurrentBattleKind = kind;
            try { PlayerPrefs.SetInt("currentBattleKind", (int)kind); PlayerPrefs.Save(); } catch { }

            var battleSceneToLoad = string.IsNullOrEmpty(target.assignedScene) ? battleSceneName : target.assignedScene;
            operation.PreparedBattleKind = kind;
            operation.BattleSceneToLoad = battleSceneToLoad;
            operation.BattlePayload = new RunStagePayloads.Battle
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex,
                battleKind = (int)kind,
                sceneName = battleSceneToLoad,
                isPending = true,
                prevAct = operation.PreviousAct,
                prevFloor = operation.PreviousFloor,
                prevNodeIndex = operation.PreviousNodeIndex,
                prevBattleKind = (int)operation.PreviousBattleKind,
                hasPrevLocation = true
            };
            operation.PendingVisited = null;
            operation.ShouldPersistPosition = false;
            return;
        }

        operation.TriggerAssignedScene = !string.IsNullOrEmpty(target.assignedScene);
    }

    private EventSessionDTO PrepareEventSession(NodeGoScene target)
    {
        try
        {
            var em = ServiceRegistry.GetRequired<IEventManager>();
            string eventId = !string.IsNullOrEmpty(target.eventIdOverride) ? target.eventIdOverride : defaultEventId;
            if (target.nodeType == NodeType.CardRemove && string.IsNullOrEmpty(target.eventIdOverride))
            {
                eventId = EventIds.CardRemoval;
            }
            var session = em.LoadActiveOrCreate(eventId);
            if (session == null)
            {
                Debug.LogWarning($"[MapTraversalController] 이벤트 세션 생성 실패: {eventId}");
            }
            return session;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapTraversalController] 이벤트 준비 중 오류: {e.Message}");
            return null;
        }
    }

    private bool TryRestorePreviousMapPosition(IRunStageService stageService, string serializedPayload, RunStagePayloads.Location locationPayload)
    {
        if (!RunStageService.TryParse(serializedPayload, out RunStagePayloads.Battle pendingPayload) || pendingPayload == null)
        {
            Debug.LogWarning("[MapTraversalController] Failed to parse pending battle payload; cannot restore.");
            return false;
        }

        bool payloadHasPrev = pendingPayload.hasPrevLocation ||
            pendingPayload.prevAct != 0 ||
            pendingPayload.prevFloor != 0 ||
            pendingPayload.prevNodeIndex != 0;

        if (!payloadHasPrev)
        {
            Debug.LogWarning("[MapTraversalController] Pending battle payload lacks previous location; cannot restore.");
            return false;
        }

        var prevAct = pendingPayload.prevAct;
        var prevFloor = pendingPayload.prevFloor;
        var prevNodeIndex = pendingPayload.prevNodeIndex;
        Debug.Log($"[MapTraversalController] Restoring pending battle -> prev act={prevAct}, floor={prevFloor}, index={prevNodeIndex}, kind={pendingPayload.prevBattleKind}");

        if (_run != null)
        {
            _run.Act = prevAct;
            _run.Floor = prevFloor;
            _run.NodeIndex = prevNodeIndex;
            _run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");
        }

        IDatabase db = null;
        try
        {
            db = ServiceRegistry.Get<IDatabase>();
        }
        catch { }

        if (db != null && !string.IsNullOrEmpty(_run?.RunId))
        {
            try
            {
                db.UpdateRunPosition(_run.RunId, prevAct, prevFloor, prevNodeIndex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MapTraversalController] 전투 대기 복원 중 위치 업데이트 실패: {e.Message}");
            }
        }

        if (pendingPayload.prevBattleKind >= 0)
        {
            var boundedKind = Mathf.Clamp(pendingPayload.prevBattleKind, 0, (int)GameContext.BattleKind.Boss);
            var prevKind = (GameContext.BattleKind)boundedKind;
            if (GameContext.I != null)
            {
                GameContext.I.CurrentBattleKind = prevKind;
            }
            try
            {
                PlayerPrefs.SetInt("currentBattleKind", boundedKind);
                PlayerPrefs.Save();
            }
            catch { }
        }

        locationPayload.act = prevAct;
        locationPayload.floor = prevFloor;
        locationPayload.nodeIndex = prevNodeIndex;

        stageService?.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
        Debug.Log($"[MapTraversalController] Pending battle cleared. Stage => {stageService?.Current?.Stage}");
        RunCacheSynchronizer.Sync();
        Debug.Log("[MapTraversalController] Battle transition did not complete. Restored previous map position.");
        PlaceMarker(_run.Floor, _run.NodeIndex);
        UpdateReachable(_run.Floor, _run.NodeIndex);
        return true;
    }

    private void ResetSessionsForMove()
    {
        _shopOverlay?.ResetShopSession();

        if (!string.IsNullOrEmpty(_run?.RunId))
        {
            DatabaseManager.Instance.DeleteActiveShopSession(_run.RunId);
            DatabaseManager.Instance.DeleteActiveEventSession(_run.RunId);
        }

        _shopOverlay?.ClearCachedSession();
    }

    private void CleanupAfterFailure()
    {
        ClearMarkerTween();
        _pendingOperation = null;
        _isMoving = false;
    }

    private void ClearMarkerTween()
    {
        if (_markerTween != null)
        {
            _markerTween.Kill();
            _markerTween = null;
        }
    }

    private bool ShouldAnimateMarker()
    {
        return playerMarker != null && markerMoveDuration > 0f;
    }

    private bool TryComputeAnchoredPosition(NodeGoScene node, RectTransform markerRect, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        if (node == null || markerRect == null) return false;

        var canvas = markerRect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, node.transform.position);

        var parentRect = markerRect.parent as RectTransform;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out var localPoint))
        {
            anchoredPosition = localPoint;
            return true;
        }

        return false;
    }



    /// <summary>
    /// 노드 클릭 시 유효성을 검사하고 이동 애니메이션/씬 전환을 관리합니다.
    /// </summary>
    public void OnNodeClicked(NodeGoScene target)
    {
        if (target == null)
        {
            Debug.LogWarning("[MapTraversalController] OnNodeClicked가 null 타겟으로 호출되었습니다.");
            return;
        }

        if (_isMoving)
        {
            if (allowMarkerSkip && _markerTween != null && _markerTween.IsActive())
            {
                _markerTween.Complete(true);
            }
            return;
        }

        _isMoving = true;
        var stageService = ServiceRegistry.Get<IRunStageService>();

        try
        {
            Debug.Log($"--- OnNodeClicked --- Target: ({target.floor},{target.index}), Type: {target.nodeType}");

            if (!_nodes.TryGetValue((_run.Floor, _run.NodeIndex), out var curNode))
            {
                Debug.LogWarning("[MapTraversalController] 현재 노드를 찾지 못했습니다.");
                CleanupAfterFailure();
                return;
            }

            bool isMoveToChild = curNode.children != null && curNode.children.Contains(target);
            bool isReclickSameNode = (_run.Floor == target.floor && _run.NodeIndex == target.index);
            Debug.Log($"<color=yellow>ANALYSIS >> isMoveToChild: {isMoveToChild}, isReclickSameNode: {isReclickSameNode}</color>", this);

            if (!isMoveToChild && !isReclickSameNode)
            {
                Debug.Log("<color=red>INVALID CLICK: Action ignored.</color>");
                CleanupAfterFailure();
                return;
            }

            if (isMoveToChild)
            {
                var tutorialService = ServiceRegistry.Get<ITutorialService>();
                if (tutorialService != null && tutorialService.IsTutorialRun)
                {
                    if (!tutorialService.CanMoveToNode(_run.Act, target.floor, target.index))
                    {
                        Debug.Log("[MapTraversalController] Tutorial gating prevented movement to this node.");
                        CleanupAfterFailure();
                        return;
                    }
                }
            }

            if (isMoveToChild)
            {
                Debug.Log("<color=cyan>ACTION >> Moving to a new node. Resetting shop/event session caches...</color>", this);
                ResetSessionsForMove();
            }

            _pendingOperation = CreatePendingOperation(target, isMoveToChild, isReclickSameNode);
            if (_pendingOperation == null)
            {
                Debug.LogWarning("[MapTraversalController] PendingStageOperation 생성에 실패했습니다.");
                CleanupAfterFailure();
                return;
            }

            if (isMoveToChild && ShouldAnimateMarker())
            {
                StartMarkerTween(stageService);
            }
            else
            {
                FinalizePendingOperation(stageService);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapTraversalController] 노드 이동 처리 중 오류: {e.Message}");
            CleanupAfterFailure();
        }
    }

    void PlaceMarker(int floor, int index)
    {
        // 마커나 노드 데이터가 없으면 즉시 종료
        if (playerMarker == null || _nodes == null) return;
        if (!_nodes.TryGetValue((floor, index), out var node)) return;

        var markerRect = playerMarker as RectTransform;
        var nodeTransform = node.transform;

        if (markerRect != null)
        {
            if (TryComputeAnchoredPosition(node, markerRect, out var anchoredPosition))
            {
                markerRect.anchoredPosition = anchoredPosition;
            }
            else
            {
                markerRect.position = nodeTransform.position;
            }

            markerRect.SetAsLastSibling();
        }
        else
        {
            playerMarker.position = nodeTransform.position;
        }
    }

    void UpdateReachable(int floor, int index)
    {
        if (_nodes == null) return;

        // 1. 일단 지도 위의 모든 노드를 비활성화합니다.
        foreach (var node in _nodes.Values)
        {
            node.SetReachable(false);
        }

        // 2. 현재 내가 위치한 노드를 찾습니다.
        if (_nodes.TryGetValue((floor, index), out var curNode))
        {
            // 3. 다음 층으로 갈 수 있는 모든 자식 노드들을 활성화합니다.
            if (curNode.children != null)
            {
                foreach (var child in curNode.children)
                {
                    child.SetReachable(true);
                }
            }

            // 4. [예외 규칙] 만약 현재 노드가 '상점'이라면, 자기 자신도 활성화합니다.
            // 이렇게 하면 닫았던 상점 문을 다시 열 수 있습니다.
            if (curNode.nodeType == NodeType.Shop /* && !curNode.IsCleared */)
            {
                curNode.SetReachable(true);
            }
        }
    }

    private void TriggerRewardUIIfNeeded(Game.Save.MapNodeState currentNode)
    {
        if (currentNode == null) return;
        if (string.IsNullOrEmpty(currentNode.RewardsJson)) return;

        try
        {
            var rewards = JsonUtility.FromJson<RewardContainer>(currentNode.RewardsJson);
            Debug.Log("[MapTraversal] Pending rewards found. Showing reward UI...");

            var stageService = ServiceRegistry.Get<IRunStageService>();
            var rewardPayload = new RunStagePayloads.Reward
            {
                act = currentNode.Act,
                floor = currentNode.Floor,
                nodeIndex = currentNode.NodeIndex
            };
            stageService?.SetStage(RunStageType.Reward, SceneManager.GetActiveScene().name, RunStageService.ToJson(rewardPayload));

            // 보상 UI 컨트롤러를 찾아 호출합니다. (씬에 구현체가 없으면 경고 후 종료)
            IRewardUI rewardUI = null;
            var monos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in monos)
            {
                if (m is IRewardUI ui) { rewardUI = ui; break; }
            }

            if (rewardUI == null)
            {
                Debug.LogWarning("[MapTraversal] IRewardUI 구현체를 찾지 못했습니다. 기본 보상(골드) 자동 적용 후 JSON을 정리합니다.");
                ApplyNonCardRewards(rewards);
                ClearRewardsJson(currentNode);
                stageService?.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(new RunStagePayloads.Location
                {
                    act = currentNode.Act,
                    floor = currentNode.Floor,
                    nodeIndex = currentNode.NodeIndex
                }));
                return;
            }

            rewardUI.Show(rewards, () =>
            {
                Debug.Log("[MapTraversal] Reward UI closed. Applying non-card rewards and clearing JSON.");
                ApplyNonCardRewards(rewards);
                ClearRewardsJson(currentNode);
                stageService?.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(new RunStagePayloads.Location
                {
                    act = currentNode.Act,
                    floor = currentNode.Floor,
                    nodeIndex = currentNode.NodeIndex
                }));
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MapTraversal] RewardsJson 파싱 실패: {ex.Message}");
        }
    }

    private static void ApplyNonCardRewards(RewardContainer rewards)
    {
        try
        {
            var wallet = ServiceRegistry.Get<IWalletService>();
            if (wallet == null || rewards == null || rewards.Items == null) return;
            int goldSum = 0;
            foreach (var it in rewards.Items)
            {
                if (it == null) continue;
                if (it.Type == "Gold" && it.Amount > 0) goldSum += it.Amount;
            }
            if (goldSum > 0) wallet.Add(goldSum);
        }
        catch (System.Exception applyEx)
        {
            Debug.LogWarning($"[MapTraversal] ApplyNonCardRewards 오류: {applyEx.Message}");
        }
    }

    private static void ClearRewardsJson(Game.Save.MapNodeState node)
    {
        if (node == null) return;
        node.RewardsJson = string.Empty;
        var db = ServiceRegistry.Get<IDatabase>();
        db?.UpsertNodeState(node);
    }

    /// <summary>
    /// 프리로드된 전투 씬을 즉시 활성화할 수 있다면 true를 반환합니다.
    /// </summary>
    private bool TryEnterBattleViaPreload(string sceneName)
    {
        var manager = BattlePreloadManager.Instance;
        if (manager == null) return false;
        manager.EnsurePreloadStarted(sceneName);
        var currentMapScene = SceneManager.GetActiveScene().name;
        return manager.TryActivatePreloadedScene(sceneName, currentMapScene);
    }

    private IEnumerator DeferredBattlePreload()
    {
        yield return null;
        BattlePreloadManager.Instance?.EnsurePreloadStarted(battleSceneName);
    }
}

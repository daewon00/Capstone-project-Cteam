using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Save;
using UnityEngine.SceneManagement; // SceneManager를 사용하기 위해 추가

/// <summary>
/// 맵 노드 이동과 상점/이벤트/전투 씬 전환을 관리하고 런 진행 정보를 DB와 동기화합니다.
/// </summary>
public class MapTraversalController : MonoBehaviour
{
    [Header("Marker")]
    public Transform playerMarker; // 현재 위치 마커(없으면 표시만 생략)

    Dictionary<(int floor, int index), NodeGoScene> _nodes;
    string _runId;
    CurrentRun _run;
    private bool _isMoving = false; // 노드 이동 중 재진입 방지

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

        // 씬의 모든 노드 수집
        var list = FindObjectsByType<NodeGoScene>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        _nodes = list.ToDictionary(n => (n.floor, n.index), n => n);

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
            if (currentStage == null)
            {
                stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
            }
            else
            {
                switch (currentStage.Stage)
                {
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
                    case RunStageType.Battle:
                        // 다른 씬으로 이어가기 해야 하지만 맵에 도달했다면 맵으로 복귀 처리.
                        stageService.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
                        break;
                }
            }
        }

        // 전투 돌입 시 로딩 지연을 줄이기 위해 전투 씬 프리로드를 시작합니다.
        StartCoroutine(DeferredBattlePreload());
    }



    /// <summary>
    /// 노드 클릭 시 유효성을 검사하고 이동 및 씬 전환을 실행합니다.
    /// </summary>
    public void OnNodeClicked(NodeGoScene target)
    {
        if (_isMoving) return;
        _isMoving = true;
        var stageService = ServiceRegistry.Get<IRunStageService>();

        try
        {
        // [디버그 1] 함수 시작: 어떤 노드가 클릭되었는지 기록
        Debug.Log($"--- OnNodeClicked --- Target: ({target.floor},{target.index}), Type: {target.nodeType}");

        // 1. 현재 노드 정보를 가져옵니다.
        if (!_nodes.TryGetValue((_run.Floor, _run.NodeIndex), out var curNode)) return;

        // 2. 현재 클릭이 어떤 종류인지 정의합니다.
        bool isMoveToChild = curNode.children != null && curNode.children.Contains(target);
        bool isReclickSameNode = (_run.Floor == target.floor && _run.NodeIndex == target.index);

        // [디버그 2] 클릭 종류 판단 결과 출력(이동으로 온건지 다시 누른건지)
        Debug.Log($"<color=yellow>ANALYSIS >> isMoveToChild: {isMoveToChild}, isReclickSameNode: {isReclickSameNode}</color>", this);

        // 3. 유효하지 않은 클릭은 입구에서 차단합니다
        if (!isMoveToChild && !isReclickSameNode)
        {
            Debug.Log("<color=red>INVALID CLICK: Action ignored.</color>");
            return;
        }

        // --- 여기까지 통과했다면, 클릭은 '유효'한 것으로 확정 ---

        // 4. 상태 변경: **실제로 '새로운 노드로 이동'이 발생할 때만** 실행됩니다.
        if (isMoveToChild)
        {
            Debug.Log("<color=cyan>ACTION >> Moving to a new node. Resetting shop session...</color>", this);
            
            // 상점 리클릭 아님 상태로 리셋
            _shopOverlay?.ResetShopSession();

            // 나중에 이벤트 세션도 리셋해야 할 경우를 대비한 주석이 아래에 있습니다.
            // _eventOverlay?.ResetEventSession(); 

            // db 상점 정보도 리셋합니다.
            if (!string.IsNullOrEmpty(_run?.RunId))
            {
            DatabaseManager.Instance.DeleteActiveShopSession(_run.RunId);
            DatabaseManager.Instance.DeleteActiveEventSession(_run.RunId);
            }

            _shopOverlay?.ClearCachedSession(); //진짜 상점 메모리 데이터 리셋


            // 위치 이동에 따른 모든 상태 변경(DB 저장, 마커 이동 등)을 처리합니다.
            int prevFloor = _run.Floor;
            _run.Floor = target.floor;
            _run.NodeIndex = target.index;
            _run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");

            var visited = new MapNodeState {
                RunId = _run.RunId, Act = _run.Act,
                Floor = target.floor, NodeIndex = target.index,
                Type = (Game.Save.NodeType)target.nodeType, Visited = true
            };
            
            var db = ServiceRegistry.GetRequired<IDatabase>();
            db.UpsertNodeState(visited);
            db.UpdateRunPosition(_run.RunId, _run.Act, _run.Floor, _run.NodeIndex);

            // Broadcast floor reached when floor changes
            if (prevFloor != target.floor)
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

            PlaceMarker(target.floor, target.index);
            UpdateReachable(target.floor, target.index);
        }

        // --- 최종 행동 결정 분기 시작 ---
        Debug.Log($"--- Final Action --- Deciding action for node type: {target.nodeType}");


        // 5. 최종 행동 결정: 모든 검사와 상태 변경이 끝난 후, 딱 한 번만 결정합니다.
        var locationPayload = new RunStagePayloads.Location
        {
            act = _run.Act,
            floor = _run.Floor,
            nodeIndex = _run.NodeIndex
        };

        if (target.nodeType == NodeType.Shop)
        {
            // 목표가 상점이면 (새로 이동했든, 다시 클릭했든) 상점 오버레이를 엽니다.
            Debug.Log("<color=green>ACTION: Opening Shop Overlay.</color>");
            stageService?.SetStage(RunStageType.ShopOverlay, SceneManager.GetActiveScene().name, RunStageService.ToJson(new RunStagePayloads.Shop
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex
            }));
            _shopOverlay?.OpenForNode(_run.Floor, _run.NodeIndex);
        }
        else if (target.nodeType == NodeType.Event)
        {
            Debug.Log("<color=green>ACTION: Processing Event Node.</color>");
            // '전문가 보관소'에서 EventManager를 꺼내옵니다.
            var em = ServiceRegistry.GetRequired<IEventManager>();
            if (em == null)
            {
                Debug.LogError("[MapTraversal] EventManager가 등록되지 않았습니다.");
                return;
            }

            // '같은 노드 재클릭'일 경우 (주로 '이어하기' 직후)
            if (isReclickSameNode && !isMoveToChild)
            {
                // DB에 진행 중인 이벤트가 있는지 '확인만' 합니다.
                var activeSession = em.TryLoadActive();
                if (activeSession != null)
                {
                    // 있다면, 이벤트 씬으로 보냅니다.
                    stageService?.SetStage(RunStageType.Event, eventSceneName, stageService.Current?.PayloadJson);
                    SceneManager.LoadScene(eventSceneName);
                }
                // 없다면 (이미 해결된 이벤트라면), 아무것도 하지 않습니다.
            }

            // '새로운 노드로 이동'일 경우
            else if (isMoveToChild)
            {
                // 이 노드에 지정된 특정 이벤트 ID가 있으면 그것을 사용하고, 없으면 기본 ID를 사용합니다.
                string eventId = !string.IsNullOrEmpty(target.eventIdOverride)
                                ? target.eventIdOverride
                                : defaultEventId;

                // DB에 활성 이벤트가 없으면 '새로 만들고', 있다면 불러옵니다.
                var session = em.LoadActiveOrCreate(eventId);
                if (session != null)
                {
                    var payload = new RunStagePayloads.Event
                    {
                        act = locationPayload.act,
                        floor = locationPayload.floor,
                        nodeIndex = locationPayload.nodeIndex,
                        eventId = session.eventId
                    };
                    stageService?.SetStage(RunStageType.Event, eventSceneName, RunStageService.ToJson(payload));
                    SceneManager.LoadScene(eventSceneName);
                }
            }

        }
        else if (target.nodeType == NodeType.Battle)
        {
            // 기본은 일반 전투로 태깅하되, assignedScene 이름을 힌트로 엘리트/보스를 추론합니다.
            var kind = GameContext.BattleKind.Normal;
            var hint = target.assignedScene;
            if (!string.IsNullOrEmpty(hint))
            {
                var hl = hint.ToLowerInvariant();
                if (hl.Contains("boss")) kind = GameContext.BattleKind.Boss;
                else if (hl.Contains("elite")) kind = GameContext.BattleKind.Elite;
            }
            if (GameContext.I != null) GameContext.I.CurrentBattleKind = kind;
            try { PlayerPrefs.SetInt("currentBattleKind", (int)kind); PlayerPrefs.Save(); } catch { }
            Debug.Log($"[BossFlow][Map] Battle node click → kind={kind}, nodeType={target.nodeType}, assignedScene='{target.assignedScene}'");
            var battleSceneToLoad = string.IsNullOrEmpty(target.assignedScene) ? battleSceneName : target.assignedScene;
            var battlePayload = new RunStagePayloads.Battle
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex,
                battleKind = (int)kind,
                sceneName = battleSceneToLoad
            };
            stageService?.SetStage(RunStageType.Battle, battleSceneToLoad, RunStageService.ToJson(battlePayload));
            ServiceRegistry.Get<IDatabase>()?.DeleteActiveBattleState(_run.RunId);
            if (TryEnterBattleViaPreload(battleSceneToLoad)) return;
            SceneManager.LoadScene(battleSceneToLoad);
            return;
        }
        else if (target.nodeType == NodeType.Elite)
        {
            // 엘리트 전투도 동일 씬 사용
            if (GameContext.I != null) GameContext.I.CurrentBattleKind = GameContext.BattleKind.Elite;
            try { PlayerPrefs.SetInt("currentBattleKind", (int)GameContext.BattleKind.Elite); PlayerPrefs.Save(); } catch { }
            Debug.Log($"[BossFlow][Map] Elite node click → kind=Elite, assignedScene='{target.assignedScene}'");
            var battleSceneToLoad = string.IsNullOrEmpty(target.assignedScene) ? battleSceneName : target.assignedScene;
            var battlePayload = new RunStagePayloads.Battle
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex,
                battleKind = (int)GameContext.BattleKind.Elite,
                sceneName = battleSceneToLoad
            };
            stageService?.SetStage(RunStageType.Battle, battleSceneToLoad, RunStageService.ToJson(battlePayload));
            ServiceRegistry.Get<IDatabase>()?.DeleteActiveBattleState(_run.RunId);
            if (TryEnterBattleViaPreload(battleSceneToLoad)) return;
            SceneManager.LoadScene(battleSceneToLoad);
            return;
        }
        else if (target.nodeType == NodeType.Boss)
        {
            // 보스 전투도 동일 씬 사용
            if (GameContext.I != null) GameContext.I.CurrentBattleKind = GameContext.BattleKind.Boss;
            try { PlayerPrefs.SetInt("currentBattleKind", (int)GameContext.BattleKind.Boss); PlayerPrefs.Save(); } catch { }
            Debug.Log($"[BossFlow][Map] Boss node click → kind=Boss, assignedScene='{target.assignedScene}'");
            var battleSceneToLoad = string.IsNullOrEmpty(target.assignedScene) ? battleSceneName : target.assignedScene;
            var battlePayload = new RunStagePayloads.Battle
            {
                act = locationPayload.act,
                floor = locationPayload.floor,
                nodeIndex = locationPayload.nodeIndex,
                battleKind = (int)GameContext.BattleKind.Boss,
                sceneName = battleSceneToLoad
            };
            stageService?.SetStage(RunStageType.Battle, battleSceneToLoad, RunStageService.ToJson(battlePayload));
            ServiceRegistry.Get<IDatabase>()?.DeleteActiveBattleState(_run.RunId);
            if (TryEnterBattleViaPreload(battleSceneToLoad)) return;
            SceneManager.LoadScene(battleSceneToLoad);
            return;
        }
        else if (isMoveToChild) // 상점이 아닌 다른 노드는, '이동'했을 때만 씬을 전환합니다.
        {
            Debug.Log($"<color=cyan>ACTION: Other node type. Calling GoToAssignedScene for '{target.assignedScene}'</color>");
            stageService?.SetStage(RunStageType.Map, SceneManager.GetActiveScene().name, RunStageService.ToJson(locationPayload));
            target.GoToAssignedScene();
        }
        else
        {
            // 어떤 조건에도 해당하지 않음
            Debug.Log("<color=orange>WARNING: No action taken. isMoveToChild was false for a non-shop/event node.</color>");
        }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapTraversalController] 노드 이동 처리 중 오류: {e.Message}");
        }
        finally
        {
            _isMoving = false;
        }
    }

    void PlaceMarker(int floor, int index)
    {
        // 마커나 노드 데이터가 없으면 즉시 종료
        if (playerMarker == null || _nodes == null) return;
        if (!_nodes.TryGetValue((floor, index), out var node)) return;

        var markerRect = playerMarker as RectTransform;
        var nodeTransform = node.transform;

        // 1. 마커가 UI 오브젝트일 경우 (가장 흔한 케이스)
        if (markerRect != null)
        {
            // 마커가 속한 캔버스와 렌더링용 카메라를 찾습니다.
            var canvas = markerRect.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            // 노드의 월드 좌표를 화면 좌표로 변환합니다.
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, nodeTransform.position);

            // 변환된 화면 좌표를 마커의 부모 UI 기준 로컬 좌표(anchoredPosition)로 다시 변환합니다.
            var parentRect = markerRect.parent as RectTransform;
            if (parentRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out var localPoint))
            {
                markerRect.anchoredPosition = localPoint;
            }
            else
            {
                // 변환이 실패하면 최후의 수단으로 월드 좌표라도 맞춰줍니다.
                markerRect.position = nodeTransform.position;
            }

            // (선택사항) 마커가 다른 UI에 가려지지 않도록 맨 위로 올립니다.
            markerRect.SetAsLastSibling();
        }
        // 2. 마커가 UI가 아닌 일반 3D/2D 오브젝트일 경우
        else
        {
            // 간단하게 월드 좌표를 그대로 복사합니다.
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

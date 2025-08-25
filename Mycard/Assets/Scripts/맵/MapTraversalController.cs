using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Game.Save;

public class MapTraversalController : MonoBehaviour
{
    [Header("Marker")]
    public Transform playerMarker; // 현재 위치 마커(없으면 표시만 생략)

    Dictionary<(int floor, int index), NodeGoScene> _nodes;
    string _runId;
    CurrentRun _run;
    


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
    }



    public void OnNodeClicked(NodeGoScene target)
    {
        var curKey = (_run.Floor, _run.NodeIndex);
        if (!_nodes.TryGetValue(curKey, out var curNode)) return;

        // 허용 이동인지 검사: 현재 노드의 자식이어야 함
        if (curNode.children == null || !curNode.children.Contains(target))
        {
            // TODO: 피드백(사운드/툴팁) 원하면 여기서
            return;
        }

        // 위치 갱신
        _run.Floor = target.floor;
        _run.NodeIndex = target.index;
        _run.UpdatedAtUtc = System.DateTime.UtcNow.ToString("o");

        var visited = new MapNodeState
        {
            RunId = _run.RunId,
            Act = _run.Act,
            Floor = target.floor,
            NodeIndex = target.index,
            // 👇 여기! 저장용 enum으로 명시적 캐스트
            Type = (Game.Save.NodeType) target.nodeType,
            Visited = true
        };


        DatabaseManager.Instance.SaveCurrentRun(
            _run,
            cards: null, relics: null, potions: null,
            nodes: new List<MapNodeState> { visited },
            rngStates: null
        );

        // 시각 갱신
        PlaceMarker(target.floor, target.index);
        UpdateReachable(target.floor, target.index);

        // 실제 행동(씬 진입/패널 오픈 등)
        target.GoToAssignedScene();
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

        foreach (var kv in _nodes) kv.Value.SetReachable(false);
        if (_nodes.TryGetValue((floor, index), out var cur))
        {
            if (cur.children != null)
                foreach (var child in cur.children) child.SetReachable(true);
        }
    }
}
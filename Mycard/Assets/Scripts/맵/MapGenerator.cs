using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;
using Game.Save;

/// <summary>
/// 맵 노드와 경로를 절차적으로 생성하고 배치 프리팹을 인스턴스화하는 제너레이터입니다.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    private bool _isMapBuilt = false; // 맵 생성 여부
    // 맵 완성 보고를 위한 신호

    private int BossLayerIndex => Mathf.Max(0, numberOfLayers - 1); // 보스층은 마지막 층
    private int FinalRestLayerIndex => Mathf.Max(0, numberOfLayers - 2); // 최종 휴식층은 마지막-1 층
    // 노드가 자체적으로 layerIndex를 보유하므로 별도 캐시는 사용하지 않습니다.

    [Header("맵 설정")]
    [SerializeField] private int numberOfLayers = 8; // 맵의 전체 층 수 (0~7층)
    [SerializeField] private int minNodesPerLayer = 1; // 층당 최소 노드 수 (1개)
    [SerializeField] private int maxNodesPerLayer = 3; // 층당 최대 노드 수 (3개)

    [Header("랜덤 시드")]
    // 랜덤 시드. -1이면 무작위, 특정 숫자면 고정된 맵 생성
    [SerializeField] private int mapSeed = -1;
    private System.Random random; // System.Random 사용
    private int resolvedSeed = -1; // 실제로 사용된 시드 (저장/복원용)

    [Header("노드 위치 설정")]
    [SerializeField] private float layerSpacing = 300f; // 층(세로) 간격
    [SerializeField] private float nodeSpacing = 200f;  // 노드(가로) 간격
    [SerializeField] private float positionRandomness = 50f; // 노드 랜덤 간격

    [Header("프리팹 연결")]
    // 여기에 이전에 이름 정했던 프리팹들을 연결할 겁니다.
    [SerializeField] private GameObject BattleNodePrefab;
    [SerializeField] private GameObject EliteNodePrefab;
    [SerializeField] private GameObject BossNodePrefab;
    [SerializeField] private GameObject EventNodePrefab;
    [SerializeField] private GameObject ShopNodePrefab;
    [SerializeField] private GameObject RestNodePrefab;
    [SerializeField] private GameObject CardRemoveNodePrefab;
    [SerializeField] private LineRenderer pathLinePrefab;
    [SerializeField] private Transform nodesRoot;
    [SerializeField] private Transform pathsRoot;

    [Header("스크롤 연동")]
    [SerializeField] private ScrollRect mapScrollRect;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private float contentPadding = 120f;

#if UNITY_EDITOR
    [Header("테스트 도우미")]
    [SerializeField] private bool forceTestLayerCount = false;
    [SerializeField] [Range(8, 30)] private int testLayerCount = 12;
#endif

    [Header("배치 정책")]
    [SerializeField] private int minEliteLayerPolicy = 1; // 엘리트 최소 레이어 정책(기본 1층)

    [Header("이벤트 풀")]
    [SerializeField] private EventPoolDefinition eventPoolDefinition;
    [SerializeField] private string defaultEventId = "GoldenIdolEvent";
    [SerializeField] private string restEventId = EventIds.CampfireRest;

    [Header("레이아웃 정리(교차선 감소)")]
    [SerializeField] private bool enableBarycenterOrdering = true; // 배리센터 정렬 적용 여부
    [SerializeField] private int barycenterPasses = 2; // 상하 왕복 패스 수
    [SerializeField] [Range(0f, 1f)] private float barycenterLerpAlpha = 0.5f; // 스냅 대신 Lerp 비율

    
    private List<List<MapDataNode>> mapData = new List<List<MapDataNode>>(); // 생성된 모든 맵 노드 데이터를 저장할 리스트입니다.

    private List<GameObject> nodeObjects = new List<GameObject>(); // 생성된 실제 노드 오브젝트들을 저장하여 선을 그릴 때 사용합니다.
    private readonly List<LineRenderer> pathLines = new List<LineRenderer>();
    private readonly Dictionary<MapDataNode, Transform> nodeToTransform = new Dictionary<MapDataNode, Transform>();

    // 인스펙터에서 실수로 최소 요구사항을 깨뜨리는 것을 방지하기 위한 클램프
    private void OnValidate()
    {
        if (numberOfLayers < 8)
        {
            numberOfLayers = 8;
        }
        if (minNodesPerLayer < 1)
        {
            minNodesPerLayer = 1;
        }
        if (maxNodesPerLayer < minNodesPerLayer)
        {
            maxNodesPerLayer = minNodesPerLayer;
        }
        if (minEliteLayerPolicy < 1)
        {
            minEliteLayerPolicy = 1;
        }
        if (barycenterPasses < 1)
        {
            barycenterPasses = 1;
        }
        if (barycenterLerpAlpha < 0f) barycenterLerpAlpha = 0f;
        if (barycenterLerpAlpha > 1f) barycenterLerpAlpha = 1f;
        if (contentPadding < 0f) contentPadding = 0f;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (forceTestLayerCount)
            {
                numberOfLayers = Mathf.Clamp(testLayerCount, 8, 30);
            }
            else if (numberOfLayers < 8)
            {
                numberOfLayers = 8;
            }
        }
#endif
    }

    // 부모-자식 링크를 중복 없이 추가하는 헬퍼
    private static void Link(MapDataNode parent, MapDataNode child)
    {
        if (!parent.children.Contains(child))
        {
            parent.children.Add(child);
        }
        if (!child.parents.Contains(parent))
        {
            child.parents.Add(parent);
        }
    }

    /*
    // 게임이 시작될 때 맵을 생성합니다.
    void Start()
    {
        GenerateMap();
    }
    */

    // 맵 생성의 전체 과정을 지휘하는 메인 함수입니다.
    public int ResolvedSeed => resolvedSeed;

    public void GenerateMap()
    {
        Debug.Log($"[MapGen] 맵 생성이 {Time.frameCount} 프레임에서 시작됩니다.");


        // 파라미터 유효성 검사
        if (numberOfLayers < 8)
        {
            Debug.LogError("numberOfLayers는 최소 8 이상이어야 합니다. (설계 전제)");
            return;
        }
        if (minNodesPerLayer < 1)
        {
            Debug.LogError("minNodesPerLayer는 1 이상이어야 합니다.");
            return;
        }
        if (minNodesPerLayer > maxNodesPerLayer)
        {
            Debug.LogError("minNodesPerLayer는 maxNodesPerLayer보다 클 수 없습니다.");
            return;
        }
        // 랜덤 시드 초기화: 고정 시드 제공 시 재현성 보장, 미지정 시 보안 난수 기반 시드 사용
        int seedToUse;
        if (mapSeed != -1)
        {
            seedToUse = mapSeed;
        }
        else
        {
            seedToUse = GenerateSeed();
            mapSeed = seedToUse; // 최초 생성 시 실제 시드를 기록
        }

        resolvedSeed = seedToUse;
        random = new System.Random(seedToUse);

        // 1단계: 맵의 뼈대(노드 위치) 생성
        CreateNodePositions();

        // 2단계: 경로 생성 
        CreatePaths(); 

        // 2.5단계: 교차선 감소를 위한 배리센터 정렬(옵션)
        ApplyBarycenterOrdering();

        // 3단계: 노드 타입 결정 (나중에 추가할 함수)
        SetNodeTypes();

        // 3.5단계: 이벤트 노드에 이벤트 ID 배정
        AssignEventIds();

        // 4단계: 화면에 실제 오브젝트 생성 (중앙 함수로 자동 배치/배선)
        InstantiateMapObjects();
    }

    /// <summary>
    /// 지정된 시드로 맵을 생성하고 결과 스냅샷을 반환합니다. (이미 생성된 경우 현재 레이아웃을 돌려줌)
    /// </summary>
    public MapLayoutSnapshot BuildWithSeed(int seed)
    {
        if (_isMapBuilt)
        {
            Debug.LogWarning("[MapGen] BuildWithSeed가 중복 호출되었습니다. 현재 레이아웃 스냅샷을 반환합니다.");
            return CaptureLayoutSnapshot();
        }

        _isMapBuilt = true;
        mapSeed = seed;
        resolvedSeed = seed;
        GenerateMap();
        return CaptureLayoutSnapshot();
    }

    /// <summary>
    /// 저장된 레이아웃 스냅샷을 그대로 복원합니다.
    /// </summary>
    public void BuildFromSnapshot(MapLayoutSnapshot snapshot)
    {
        if (snapshot == null)
        {
            Debug.LogError("[MapGen] BuildFromSnapshot가 null 스냅샷으로 호출되었습니다.");
            return;
        }

        resolvedSeed = snapshot.Seed;
        if (resolvedSeed > 0)
        {
            mapSeed = resolvedSeed;
        }

        _isMapBuilt = true;
        RebuildMapDataFromSnapshot(snapshot);
        AssignEventIds();
        InstantiateMapObjects();
    }

    /// <summary>
    /// 현재 메모리에 존재하는 맵 레이아웃을 JSON 직렬화용 스냅샷으로 변환합니다.
    /// </summary>
    private MapLayoutSnapshot CaptureLayoutSnapshot()
    {
        if (mapData == null || mapData.Count == 0)
        {
            return null;
        }

        var snapshot = new MapLayoutSnapshot
        {
            Seed = resolvedSeed
        };

        for (int layerIndex = 0; layerIndex < mapData.Count; layerIndex++)
        {
            var layer = mapData[layerIndex];
            for (int nodeIndex = 0; nodeIndex < layer.Count; nodeIndex++)
            {
                var node = layer[nodeIndex];
                if (node == null) continue;

                var nodeSnapshot = new MapLayoutNodeSnapshot
                {
                    Floor = node.layerIndex,
                    Index = nodeIndex,
                    NodeType = node.nodeType,
                    PositionX = node.position.x,
                    PositionY = node.position.y,
                    EventIdOverride = node.eventIdOverride
                };

                if (node.children != null)
                {
                    foreach (var child in node.children)
                    {
                        int childIndex = GetIndexInLayer(child);
                        if (childIndex < 0) continue;
                        nodeSnapshot.Children.Add(new MapLayoutEdge
                        {
                            Floor = child.layerIndex,
                            Index = childIndex
                        });
                    }
                }

                snapshot.Nodes.Add(nodeSnapshot);
            }
        }

        return snapshot;
    }

    private void AssignRestEventOverrides()
    {
        if (mapData == null || mapData.Count == 0) return;
        if (string.IsNullOrEmpty(restEventId)) return;

        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                if (node == null || node.nodeType != NodeType.Rest) continue;
                if (!string.IsNullOrEmpty(node.eventIdOverride)) continue;
                node.eventIdOverride = restEventId;
            }
        }
    }

    private void RebuildMapDataFromSnapshot(MapLayoutSnapshot snapshot)
    {
        mapData = new List<List<MapDataNode>>();
        if (snapshot.Nodes == null || snapshot.Nodes.Count == 0) return;

        var lookup = new Dictionary<(int floor, int index), MapDataNode>();

        foreach (var group in snapshot.Nodes.GroupBy(n => n.Floor).OrderBy(g => g.Key))
        {
            var ordered = group.OrderBy(n => n.Index).ToList();
            var layerList = new List<MapDataNode>(ordered.Count);

            for (int i = 0; i < ordered.Count; i++)
            {
                var nodeSnap = ordered[i];
                while (layerList.Count <= nodeSnap.Index)
                {
                    layerList.Add(null);
                }

                var node = new MapDataNode(nodeSnap.NodeType, new Vector2(nodeSnap.PositionX, nodeSnap.PositionY), nodeSnap.Floor)
                {
                    eventIdOverride = nodeSnap.EventIdOverride ?? string.Empty
                };

                layerList[nodeSnap.Index] = node;
                lookup[(node.layerIndex, nodeSnap.Index)] = node;
            }

            // null 방지: 비어 있는 슬롯이 있으면 기본 전투 노드로 채움
            for (int idx = 0; idx < layerList.Count; idx++)
            {
                if (layerList[idx] == null)
                {
                    var fallback = new MapDataNode(NodeType.Battle, Vector2.zero, ordered.First().Floor);
                    layerList[idx] = fallback;
                    lookup[(fallback.layerIndex, idx)] = fallback;
                    Debug.LogWarning($"[MapGen] 스냅샷에 비어 있는 노드가 감지되어 기본 Battle 노드로 보정했습니다. floor={ordered.First().Floor}, index={idx}");
                }
            }

            mapData.Add(layerList);
        }

        // 링크 재구성 전 부모/자식 초기화
        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                node.children.Clear();
                node.parents.Clear();
            }
        }

        foreach (var nodeSnap in snapshot.Nodes)
        {
            if (!lookup.TryGetValue((nodeSnap.Floor, nodeSnap.Index), out var parent))
            {
                continue;
            }

            if (nodeSnap.Children == null) continue;

            foreach (var childEdge in nodeSnap.Children)
            {
                if (lookup.TryGetValue((childEdge.Floor, childEdge.Index), out var child))
                {
                    Link(parent, child);
                }
            }
        }

        numberOfLayers = mapData.Count;
    }

    private int GetIndexInLayer(MapDataNode node)
    {
        if (node == null || node.layerIndex < 0 || node.layerIndex >= mapData.Count)
        {
            return -1;
        }

        return mapData[node.layerIndex].IndexOf(node);
    }

    // 주어진 위치에 가장 가까운 노드를 선형 스캔으로 찾습니다. (제곱거리 비교로 sqrt 회피)
    private static MapDataNode FindClosestNode(List<MapDataNode> candidates, Vector2 referencePosition)
    {
        if (candidates == null || candidates.Count == 0) return null;
        MapDataNode closest = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            float sqr = (c.position - referencePosition).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                closest = c;
            }
        }
        return closest;
    }

    // 보안 난수 기반 시드 생성. 플랫폼/런타임에 따라 미지원 시 TickCount로 폴백
    private static int GenerateSeed()
    {
        try
        {
            // 0 이상 int.MaxValue 미만 범위의 균등분포 정수
            return RandomNumberGenerator.GetInt32(int.MaxValue);
        }
        catch
        {
            return System.Environment.TickCount;
        }
    }

    private void AssignEventIds()
    {
        if (mapData == null || mapData.Count == 0) return;

        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        AssignRestEventOverrides();

        if (!string.IsNullOrEmpty(restEventId))
        {
            usedIds.Add(restEventId);
        }

        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                if (node == null || node.nodeType != NodeType.Event) continue;
                if (!string.IsNullOrEmpty(node.eventIdOverride))
                {
                    usedIds.Add(node.eventIdOverride);
                }
            }
        }

        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                if (node == null || node.nodeType != NodeType.Event) continue;
                if (!string.IsNullOrEmpty(node.eventIdOverride)) continue;

                string eventId = SelectEventIdForLayer(node.layerIndex, usedIds);
                if (string.IsNullOrEmpty(eventId))
                {
                    eventId = ResolveFallbackEventId();
                    Debug.LogWarning($"[MapGen] 이벤트 풀 소진 또는 조건 불일치로 기본 이벤트를 사용합니다. layer={node.layerIndex}, eventId='{eventId}'");
                }

                node.eventIdOverride = eventId;
                if (!string.IsNullOrEmpty(eventId))
                {
                    usedIds.Add(eventId);
                }
            }
        }
    }

    private string SelectEventIdForLayer(int layerIndex, HashSet<string> usedIds)
    {
        if (eventPoolDefinition == null || eventPoolDefinition.Entries == null || eventPoolDefinition.Entries.Count == 0)
        {
            return null;
        }

        if (random == null)
        {
            return null;
        }

        var candidates = new List<EventPoolDefinition.Entry>();
        foreach (var entry in eventPoolDefinition.Entries)
        {
            if (entry == null) continue;
            if (string.IsNullOrEmpty(entry.eventId)) continue;
            if (entry.weight <= 0) continue;
            if (usedIds.Contains(entry.eventId)) continue;
            if (layerIndex < entry.minLayer || layerIndex > entry.maxLayer) continue;
            candidates.Add(entry);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int totalWeight = 0;
        foreach (var candidate in candidates)
        {
            totalWeight += Mathf.Max(1, candidate.weight);
        }

        int roll = random.Next(totalWeight);
        int cumulative = 0;
        foreach (var candidate in candidates)
        {
            cumulative += Mathf.Max(1, candidate.weight);
            if (roll < cumulative)
            {
                return candidate.eventId;
            }
        }

        return candidates[candidates.Count - 1].eventId;
    }

    private string ResolveFallbackEventId()
    {
        if (eventPoolDefinition != null && !string.IsNullOrEmpty(eventPoolDefinition.FallbackEventId))
        {
            return eventPoolDefinition.FallbackEventId;
        }

        return string.IsNullOrEmpty(defaultEventId) ? "GoldenIdolEvent" : defaultEventId;
    }

    public void RegenerateWithSeed(int seed)
    {
        BuildWithSeed(seed);
    }

    #region --- 1단계: 맵 뼈대 생성 함수 ---
    void CreateNodePositions()
    {
        Debug.Log("1단계: 맵 뼈대 생성을 시작합니다.");

        mapData.Clear(); // 맵 다시 생성시 초기화

        bool hasLayerWithThreeNodes = false;
        List<int> adjustableLayers = new List<int>();

        // 0층(시작 지점)부터 마지막 층까지 반복합니다.
        for (int i = 0; i < numberOfLayers; i++)
        {
            bool isPinnedLayer = i == 0 || i == FinalRestLayerIndex || i == BossLayerIndex;
            int nodesInThisLayer;

            if (isPinnedLayer)
            {
                nodesInThisLayer = 1;
            }
            else
            {
                nodesInThisLayer = random.Next(2, 4); // 2~3개 노드 생성
                if (nodesInThisLayer >= 3)
                {
                    hasLayerWithThreeNodes = true;
                }
                adjustableLayers.Add(i);
            }

            mapData.Add(GenerateLayerNodes(i, nodesInThisLayer));
        }

        if (!hasLayerWithThreeNodes && adjustableLayers.Count > 0)
        {
            int targetLayer = adjustableLayers[random.Next(0, adjustableLayers.Count)];
            mapData[targetLayer] = GenerateLayerNodes(targetLayer, 3);
            hasLayerWithThreeNodes = true;
            Debug.Log($"[MapGen] 모든 조정 가능 층이 2개 노드여서 {targetLayer}층을 3개 노드로 재생성했습니다.");
        }

        Debug.Log("맵 뼈대 생성 완료! 총 " + mapData.Count + "개의 층이 생성되었습니다.");
    }

    private List<MapDataNode> GenerateLayerNodes(int layerIndex, int nodeCount)
    {
        var nodes = new List<MapDataNode>(nodeCount);
        float yBase = layerIndex * layerSpacing;
        float centerOffset = (nodeCount - 1) / 2f;

        for (int j = 0; j < nodeCount; j++)
        {
            float yPos = yBase;
            float xPos = (j - centerOffset) * nodeSpacing;

            if (positionRandomness > 0f)
            {
                int randomness = Mathf.RoundToInt(positionRandomness);
                if (randomness > 0)
                {
                    xPos += random.Next(-randomness, randomness + 1);
                    yPos += random.Next(-randomness, randomness + 1);
                }
            }

            nodes.Add(new MapDataNode(NodeType.Battle, new Vector2(xPos, yPos), layerIndex));
        }

        return nodes;
    }
    #endregion

    #region 2단계: 경로 생성 (선 긋기)
    void CreatePaths()
    {
        Debug.Log("2단계: 경로 생성을 시작합니다.");

        // --- 규칙 2.3 & 2.4: 보스 경로 보장 (거꾸로 연결) ---
        // 마지막 층 바로 앞(mapData.Count - 2)부터 시작해서 0층까지 거꾸로 반복합니다.
        for (int i = mapData.Count - 2; i >= 0; i--)
        {
            foreach (var node in mapData[i])
            {
                // 다음 층(자식 층)에서 가장 가까운 노드를 찾습니다.
                var childLayer = mapData[i + 1];

                // 선형 스캔으로 가장 가까운 자식 노드를 찾습니다.
                var closestChild = FindClosestNode(childLayer, node.position);

                if (closestChild != null)
                {
                    // 양방향으로 연결해줍니다.
                    Link(node, closestChild);
                }
            }
        }
        
        // --- 규칙 2.5 & 2.6: 분기/수렴 처리 (순서대로 연결) ---
        // 0층부터 마지막에서 두 번째 층까지 순서대로 반복합니다.
        for (int i = 0; i < mapData.Count - 1; i++)
        {
            // 같은 층에서 바로 이전 노드가 랜덤 경로를 만들었는지 추적하는 변수
            bool previousNodeMadeRandomPath = false;

            // 노드들을 왼쪽(x좌표가 작은)부터 순서대로 처리하기 위해 정렬합니다.
            var currentLayerSorted = mapData[i].OrderBy(n => n.position.x).ToList();

            foreach (var node in currentLayerSorted)
            {
                // 만약 바로 왼쪽 노드가 이미 랜덤 경로를 만들었다면, 이번 노드는 건너뜁니다.
                if (previousNodeMadeRandomPath)
                {
                    previousNodeMadeRandomPath = false; // 플래그 초기화
                    continue; // 다음 노드로 넘어감
                }

                var childLayer = mapData[i + 1];

                // 이웃 노드만 후보로 삼아 선 겹침을 최소화합니다.
                var neighbors = childLayer.Where(child => Mathf.Abs(child.position.x - node.position.x) < nodeSpacing * 1.5f).ToList();
                var potentialChildren = neighbors.Where(child => !node.children.Contains(child)).ToList();

                // 50% 확률로 추가 경로를 1개 더 연결합니다.
                if (random.Next(0, 100) < 50 && potentialChildren.Any())
                {
                    var randomChild = potentialChildren[random.Next(0, potentialChildren.Count)];
                    Link(node, randomChild);

                    // 내가 랜덤 경로를 만들었으니, 다음 노드는 만들지 말라고 표시합니다.
                    previousNodeMadeRandomPath = true;
                }
            }
        }

        // 1층부터 마지막 층까지 순서대로 모든 노드를 확인하여 고립 노드 확인
        for (int i = 1; i < mapData.Count; i++)
        {
            foreach (var node in mapData[i])
            {
                // 만약 이 노드로 들어오는 길이 하나도 없다면 ('고아 노드'라면)
                if (node.parents.Count == 0)
                {
                    // 이전 층(부모 층)에서 가장 가까운 노드를 찾아 강제로 연결해줍니다.
                    var parentLayer = mapData[i - 1];
                    var closestParent = FindClosestNode(parentLayer, node.position);

                    if (closestParent != null)
                    {
                        Link(closestParent, node);
                    }
                }
            }
        }

        // --- 규칙 2.7: 최종 경로 수렴 ---
        // [디자인 결정]
        // 챕터 1에서는 난이도 완화를 위해 보스 직전(최종 휴식층)으로 경로를 강제 수렴(hard convergence)합니다.
        // 아래 로직은 부모층 모든 노드의 자식 경로를 초기화하고, 오직 최종 휴식 노드로만 연결합니다.
        // [향후 확장(참고용 메모)]
        // - 챕터 추가/난이도 상향 시, 다음 모드를 고려할 수 있습니다.
        //   1) Hard: 현행 유지(완전 수렴)
        //   2) Soft: 기존 자식 연결은 유지하되, 최종 휴식으로의 경로를 최소 1개 추가(다양성 보존)
        //   3) None: 수렴 없이 경로 다양성 최대화
        //   이때 직렬화 필드(bool/enum)로 토글하여 분기 처리할 수 있습니다. (현재는 문서화만 함)
        // 휴식층(마지막-1층)의 유일한 노드로 수렴
        var finalRestNode = mapData[FinalRestLayerIndex][0];
        // 휴식 노드의 기존 부모를 정리하여 중복/잔존 링크 제거
        finalRestNode.parents.Clear();
        int parentLayerIndex = FinalRestLayerIndex - 1;
        if (parentLayerIndex >= 0 && parentLayerIndex < mapData.Count)
        {
            foreach (var node in mapData[parentLayerIndex])
            {
                // 기존 연결을 모두 지우고, 오직 최종 휴식 노드로만 연결합니다.
                node.children.Clear();
                Link(node, finalRestNode);
            }
        }

        Debug.Log("경로 생성 완료!");

    }
    #endregion

    /// <summary>
    /// 배리센터(인접 레이어의 평균 x) 휴리스틱으로 레이어 내 노드 순서를 정리하여 교차선을 줄입니다.
    /// 위에서 아래로(부모 기준) 정렬 후, 아래에서 위로(자식 기준) 정렬을 왕복하며 적용합니다.
    /// </summary>
    private void ApplyBarycenterOrdering()
    {
        if (!enableBarycenterOrdering || mapData == null || mapData.Count == 0)
        {
            return;
        }

        float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            values.Sort();
            int count = values.Count;
            int mid = count / 2;
            if ((count % 2) == 1)
            {
                return values[mid];
            }
            else
            {
                return (values[mid - 1] + values[mid]) * 0.5f;
            }
        }

        float WeightedParentX(MapDataNode node)
        {
            if (node.parents == null || node.parents.Count == 0)
            {
                return node.position.x;
            }

            if (node.parents.Count == 1)
            {
                return node.parents[0].position.x;
            }

            const float primaryWeight = 0.8f;
            const float secondaryWeight = 1f - primaryWeight;

            MapDataNode primary = node.parents[0];
            float bestDistance = Mathf.Abs(primary.position.x - node.position.x);

            for (int i = 1; i < node.parents.Count; i++)
            {
                var candidate = node.parents[i];
                float distance = Mathf.Abs(candidate.position.x - node.position.x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    primary = candidate;
                }
            }

            float secondarySum = 0f;
            int secondaryCount = 0;
            for (int i = 0; i < node.parents.Count; i++)
            {
                var candidate = node.parents[i];
                if (candidate == primary) continue;
                secondarySum += candidate.position.x;
                secondaryCount++;
            }

            float secondaryAverage = secondaryCount > 0 ? secondarySum / secondaryCount : primary.position.x;
            return primary.position.x * primaryWeight + secondaryAverage * secondaryWeight;
        }

        float MedianOfChildren(MapDataNode node)
        {
            if (node.children != null && node.children.Count > 0)
            {
                var xs = new List<float>(node.children.Count);
                for (int i = 0; i < node.children.Count; i++) xs.Add(node.children[i].position.x);
                return Median(xs);
            }
            return node.position.x;
        }

        for (int pass = 0; pass < barycenterPasses; pass++)
        {
            // Top-down: 부모 평균 x 기준으로 1층부터 마지막층까지 정렬
            for (int layerIndex = 1; layerIndex < mapData.Count; layerIndex++)
            {
                // 핀 고정: 최종 휴식/보스 레이어는 제외
                if (layerIndex == FinalRestLayerIndex || layerIndex == BossLayerIndex) continue;
                var layer = mapData[layerIndex];
                if (layer == null || layer.Count <= 1) continue;

                var bary = new Dictionary<MapDataNode, float>(layer.Count);
                foreach (var node in layer)
                {
                    bary[node] = WeightedParentX(node);
                }

                layer.Sort((a, b) =>
                {
                    int cmp = bary[a].CompareTo(bary[b]);
                    if (cmp != 0) return cmp;
                    return a.position.x.CompareTo(b.position.x);
                });

                for (int i = 0; i < layer.Count; i++)
                {
                    float targetX = (i - (layer.Count - 1) / 2f) * nodeSpacing;
                    var pos = layer[i].position;
                    float smoothedX = Mathf.Lerp(pos.x, targetX, barycenterLerpAlpha);
                    layer[i].position = new Vector2(smoothedX, pos.y);
                }
            }

            // Bottom-up: 자식 평균 x 기준으로 마지막-1층부터 0층까지 정렬
            for (int layerIndex = mapData.Count - 2; layerIndex >= 0; layerIndex--)
            {
                // 핀 고정: 시작/최종 휴식/보스 레이어는 제외
                if (layerIndex == 0 || layerIndex == FinalRestLayerIndex || layerIndex == BossLayerIndex) continue;
                var layer = mapData[layerIndex];
                if (layer == null || layer.Count <= 1) continue;

                var bary = new Dictionary<MapDataNode, float>(layer.Count);
                foreach (var node in layer)
                {
                    bary[node] = MedianOfChildren(node);
                }

                layer.Sort((a, b) =>
                {
                    int cmp = bary[a].CompareTo(bary[b]);
                    if (cmp != 0) return cmp;
                    return a.position.x.CompareTo(b.position.x);
                });

                for (int i = 0; i < layer.Count; i++)
                {
                    float targetX = (i - (layer.Count - 1) / 2f) * nodeSpacing;
                    var pos = layer[i].position;
                    float smoothedX = Mathf.Lerp(pos.x, targetX, barycenterLerpAlpha);
                    layer[i].position = new Vector2(smoothedX, pos.y);
                }
            }

            EnforceMinimumLayerSpacing();

        }
    }

    #region 3단계: 노드 타입 결정 (아이콘 정하기)
    void SetNodeTypes()
    {
        Debug.Log("3단계: 노드 타입 결정을 시작합니다.");

        // 노드가 생성 시점에 layerIndex를 확정하므로 별도 동기화 루프가 필요 없습니다.

        // 0층은 항상 시작점이므로 Battle 타입으로 설정합니다.
        // 만약 'Start' 같은 전용 타입이 있다면 그것으로 변경해도 좋습니다.
        mapData[0][0].nodeType = NodeType.Battle;

        // 배치 가능한 모든 노드를 하나의 리스트로 만듭니다. (0층, 보스층, 최종 휴식층 제외)
        // 이 리스트에서 노드를 하나씩 꺼내 타입을 지정하고 제거하는 방식으로 중복을 방지합니다.
        List<MapDataNode> placeableNodes = mapData
            .SelectMany(layer => layer)
            .Where(node => node.layerIndex != 0 && node.layerIndex != BossLayerIndex && node.layerIndex != FinalRestLayerIndex)
            .ToList();

        // --- 규칙 3.1: 고정 노드 배치 (보스, 최종 휴식) ---
        mapData[BossLayerIndex][0].nodeType = NodeType.Boss;
        mapData[FinalRestLayerIndex][0].nodeType = NodeType.Rest;
        // placeableNodes 리스트에서는 이미 제외되었습니다.

        // --- 규칙 3.5 (선행): 최소 배치 보장 ---
        // 이벤트, 상점, 카드 제거 노드를 반드시 1개씩 먼저 배치합니다.
        // 배치 가능한 층(1층 ~ 보스 전전층) 내에서 무작위로 배치합니다.
        PlaceNodeOfType(NodeType.Event, placeableNodes, 1, numberOfLayers - 3);
        PlaceNodeOfType(NodeType.Shop, placeableNodes, 1, numberOfLayers - 3);
        PlaceNodeOfType(NodeType.CardRemove, placeableNodes, 1, numberOfLayers - 3);
        
        // --- 규칙 3.2, 3.3, 3.4: 엘리트와 그에 따른 휴식, 상점 배치 ---
        PlaceElitesAndDependencies(placeableNodes);

        // --- 규칙 3.6: 남은 공간 배분 ---
        // 남은 노드들에 일반 전투 및 기타 노드들을 배분합니다.
        FillRemainingNodes(placeableNodes);

        // --- 규칙 3.7, 3.8: 배치 제약 조건 최종 확인 및 수정 ---
        // 모든 타입 할당이 끝난 후, 제약 조건에 맞지 않는 부분을 수정합니다.
        EnforceConstraints();

        Debug.Log("노드 타입 결정 완료!");
    }

    /// <summary>
    /// 특정 타입의 노드를 지정된 층 범위 내에서, 가능한 노드 목록에 1개 배치합니다.
    /// </summary>
    /// <param name="type">배치할 노드 타입</param>
    /// <param name="availableNodes">배치 가능한 노드 목록. 이 목록에서 노드가 선택되고 제거됩니다.</param>
    /// <param name="minLayer">배치 가능한 최소 층</param>
    /// <param name="maxLayer">배치 가능한 최대 층</param>
    private void PlaceNodeOfType(NodeType type, List<MapDataNode> availableNodes, int minLayer, int maxLayer)
    {
        // 지정된 층 범위 내에 있는 노드만 필터링합니다.
        var candidates = availableNodes.Where(n => n.layerIndex >= minLayer && n.layerIndex <= maxLayer).ToList();
        
        if (candidates.Count > 0)
        {
            // 후보 중에서 무작위로 하나를 선택합니다.
            var nodeToPlace = candidates[random.Next(0, candidates.Count)];
            nodeToPlace.nodeType = type;
            availableNodes.Remove(nodeToPlace); // 배치된 노드는 목록에서 제거
            Debug.Log($"{nodeToPlace.layerIndex}층에 {type} 노드 배치 완료.");
        }
        else
        {
            Debug.LogWarning($"{type} 타입을 배치할 후보 노드가 {minLayer}~{maxLayer}층 사이에 없습니다.");
        }
    }

    /// <summary>
    /// 규칙 3.2, 3.3, 3.4에 따라 엘리트 노드와 그에 종속된 휴식, 상점 노드를 배치합니다.
    /// </summary>
    private void PlaceElitesAndDependencies(List<MapDataNode> availableNodes)
    {
        // 유효한 엘리트 배치 가능 레이어: 1층 ~ 최종휴식-2 층 (엘리트 다음 휴식, 그 다음 상점 고려)
        int minEliteLayer = Mathf.Max(1, minEliteLayerPolicy);
        int maxEliteLayer = Mathf.Max(minEliteLayer, FinalRestLayerIndex - 2);

        // 타겟 레이어 헬퍼 (선호 레이어가 없으면 범위 전체에서 폴백)
        List<MapDataNode> FindEliteCandidatesInRange(int preferredMin, int preferredMax)
        {
            preferredMin = Mathf.Clamp(preferredMin, minEliteLayer, maxEliteLayer);
            preferredMax = Mathf.Clamp(preferredMax, minEliteLayer, maxEliteLayer);
            var primary = availableNodes.Where(n => {
                int li = n.layerIndex;
                return li >= preferredMin && li <= preferredMax;
            }).ToList();
            if (primary.Count > 0) return primary;
            // 폴백: 전체 유효 범위
            return availableNodes.Where(n => {
                int li = n.layerIndex;
                return li >= minEliteLayer && li <= maxEliteLayer;
            }).ToList();
        }

        // --- 첫 번째 엘리트 배치 (규칙 3.2) ---
        // 기본 선호 범위: 2~3층, 동적 범위에서 폴백
        int firstEliteLayer = -1;
        var elite1Candidates = FindEliteCandidatesInRange(2, 3);
        if (elite1Candidates.Count > 0)
        {
            var firstElite = elite1Candidates[random.Next(0, elite1Candidates.Count)];
            firstElite.nodeType = NodeType.Elite;
            availableNodes.Remove(firstElite);
            Debug.Log($"{firstElite.layerIndex}층에 첫 번째 엘리트 배치 완료.");
            firstEliteLayer = firstElite.layerIndex;

            // --- 엘리트 후 휴식 배치 (규칙 3.3) ---
            int restLayerIndex = firstElite.layerIndex + 1;
            var restCandidates = availableNodes.Where(n => n.layerIndex == restLayerIndex).ToList();
            if (restCandidates.Count > 0)
            {
                var restAfterElite = restCandidates[random.Next(0, restCandidates.Count)];
                restAfterElite.nodeType = NodeType.Rest;
                availableNodes.Remove(restAfterElite);
                Debug.Log($"{restAfterElite.layerIndex}층에 '엘리트 후 휴식' 노드 배치 완료.");

                // --- 조건부 상점 배치 (규칙 3.4) ---
                int shopLayerIndex = restAfterElite.layerIndex + 1;
                if (shopLayerIndex >= 0 && shopLayerIndex < mapData.Count)
                {
                    bool shopExistsInLayer = mapData[shopLayerIndex].Any(n => n.nodeType == NodeType.Shop);
                    if (!shopExistsInLayer)
                    {
                        var shopCandidates = availableNodes.Where(n => GetLayerIndex(n) == shopLayerIndex).ToList();
                        if (shopCandidates.Count > 0)
                        {
                            var conditionalShop = shopCandidates[random.Next(0, shopCandidates.Count)];
                            conditionalShop.nodeType = NodeType.Shop;
                            availableNodes.Remove(conditionalShop);
                            Debug.Log($"{conditionalShop.layerIndex}층에 '조건부 상점' 노드 배치 완료.");
                        }
                        else
                        {
                            Debug.LogWarning("조건부 상점을 배치할 공간이 없습니다.");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("엘리트 후 휴식 노드를 배치할 공간이 없습니다. 스킵합니다.");
            }
        }
        else
        {
            Debug.LogWarning("첫 번째 엘리트를 배치할 후보가 없습니다. 스킵합니다.");
        }

        // --- 두 번째 엘리트 배치 (규칙 3.2) ---
        // 기본 선호 범위: 4~5층, 단 첫 엘리트보다 최소 2층 뒤, 그리고 최종휴식-2 이내
        int preferredMin2 = Mathf.Max(4, minEliteLayer, (firstEliteLayer >= 0 ? firstEliteLayer + 2 : 1));
        int preferredMax2 = Mathf.Max(preferredMin2, Mathf.Min(maxEliteLayer, 5));
        var elite2Candidates = FindEliteCandidatesInRange(preferredMin2, preferredMax2);
        if (elite2Candidates.Count > 0)
        {
            var secondElite = elite2Candidates[random.Next(0, elite2Candidates.Count)];
            secondElite.nodeType = NodeType.Elite;
            availableNodes.Remove(secondElite);
            Debug.Log($"{GetLayerIndex(secondElite)}층에 두 번째 엘리트 배치 완료.");
        }
        else
        {
            Debug.LogWarning("두 번째 엘리트를 배치할 후보가 없습니다. 스킵합니다.");
        }
    }

    /// <summary>
    /// 규칙 3.6에 따라, 특별한 타입이 지정되지 않은 나머지 노드들을 채웁니다.
    /// </summary>
    private void FillRemainingNodes(List<MapDataNode> remainingNodes)
    {
        // 남은 공간의 약 50%는 일반 전투로 채웁니다.
        int battleNodeCount = Mathf.RoundToInt(remainingNodes.Count * 0.5f);

        for (int i = 0; i < battleNodeCount; i++)
        {
            if (remainingNodes.Count == 0) break;
            var nodeToFill = remainingNodes[random.Next(0, remainingNodes.Count)];
            nodeToFill.nodeType = NodeType.Battle;
            remainingNodes.Remove(nodeToFill);
        }

        // 정말 나머지 노드들은 이벤트, 상점, 카드 제거로 가중 랜덤 배분합니다.
        // 가중치: Event 0.6, Shop 0.2, CardRemove 0.2
        while (remainingNodes.Count > 0)
        {
            var nodeToFill = remainingNodes[0]; // 순서대로 채워도 무방
            float r = (float)random.NextDouble();
            if (r < 0.6f)
            {
                nodeToFill.nodeType = NodeType.Event;
            }
            else if (r < 0.8f)
            {
                nodeToFill.nodeType = NodeType.Shop;
            }
            else
            {
                nodeToFill.nodeType = NodeType.CardRemove;
            }
            remainingNodes.Remove(nodeToFill);
        }
    }

    /// <summary>
    /// 규칙 3.7과 3.8에 명시된 배치 제약 조건을 강제합니다.
    /// </summary>
    private void EnforceConstraints()
    {
        // --- 규칙 3.8: 전투 동시 배치 ---
        // 1층부터 최종 휴식층 전까지 검사
        for (int i = 1; i < FinalRestLayerIndex; i++)
        {
            var layer = mapData[i];
            bool hasSpecialNode = layer.Any(n => n.nodeType == NodeType.Shop || n.nodeType == NodeType.Elite || n.nodeType == NodeType.Rest);
            bool hasBattleNode = layer.Any(n => n.nodeType == NodeType.Battle);

            // 특별 노드가 있는데 전투 노드가 없다면
            if (hasSpecialNode && !hasBattleNode)
            {
                // 노드가 2개 이상인 레이어에서만 변경 시도 (단일 노드는 스킵)
                if (layer.Count >= 2)
                {
                    var nodeToChange = layer.FirstOrDefault(n => n.nodeType == NodeType.Event || n.nodeType == NodeType.CardRemove);
                    if (nodeToChange != null)
                    {
                        var prevType = nodeToChange.nodeType;
                        nodeToChange.nodeType = NodeType.Battle;
                        Debug.Log($"규칙 3.8 적용: {i}층에 전투 노드가 없어 {prevType}를 Battle로 변경.");
                    }
                }
            }
        }
        
        // --- 규칙 3.7: 연속 배치 제약 ---
        // 모든 노드를 순회하며 부모 노드 타입을 확인합니다.
        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                // 상점 연속 등장 방지
                if (node.nodeType == NodeType.Shop)
                {
                    if (node.parents.Any(p => p.nodeType == NodeType.Shop))
                    {
                        node.nodeType = NodeType.Event; // 현재 노드를 이벤트로 변경
                        Debug.Log($"규칙 3.7 적용: {node.layerIndex}층의 연속된 상점을 이벤트로 변경.");
                    }
                }
                
                // 일반 전투 3번 이상 연속 등장 방지 (현재노드-부모-부모의부모)
                if (node.nodeType == NodeType.Battle)
                {
                    bool hasThreeBattleChain = false;
                    foreach (var parent in node.parents)
                    {
                        if (parent.nodeType != NodeType.Battle) continue;
                        foreach (var grandParent in parent.parents)
                        {
                            if (grandParent.nodeType == NodeType.Battle)
                            {
                                hasThreeBattleChain = true;
                                break;
                            }
                        }
                        if (hasThreeBattleChain) break;
                    }
                    if (hasThreeBattleChain)
                    {
                        node.nodeType = NodeType.Event; // 3번째 전투인 현재 노드를 이벤트로 변경
                        Debug.Log($"규칙 3.7 적용: {node.layerIndex}층의 3연속 전투를 이벤트로 변경.");
                        continue; // 다음 노드로 진행
                    }
                }
            }
        }
    }

    /// <summary>
    /// 특정 노드가 몇 번째 층에 있는지 인덱스를 반환하는 헬퍼 함수입니다.
    /// </summary>
    private int GetLayerIndex(MapDataNode node)
    {
        return node != null ? node.layerIndex : -1;
    }
    #endregion

    #region 4단계: 화면에 실제 오브젝트 생성
    void InstantiateMapObjects()
    {
        Debug.Log("4단계: 실제 맵 오브젝트를 생성합니다.");

        // 이전 실행 결과가 씬에 남아있다면 정리
        foreach (var go in nodeObjects)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        nodeObjects.Clear();
        // 이전 선 렌더러들도 정리
        foreach (var lr in pathLines)
        {
            if (lr != null)
            {
                Destroy(lr.gameObject);
            }
        }
        pathLines.Clear();
        // 매핑 초기화
        nodeToTransform.Clear();

        // MapDataNode → NodeGoScene 매핑(2패스용)
        var nodeToGo = new Dictionary<MapDataNode, NodeGoScene>();

        // === 1패스: 프리팹 생성 + 타입/주소 주입 ===
        for (int layerIndex = 0; layerIndex < mapData.Count; layerIndex++)
        {
            var layer = mapData[layerIndex];
            for (int j = 0; j < layer.Count; j++)
            {
                var node = layer[j];

                GameObject prefab = GetPrefabFor(node.nodeType);
                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹이 설정되지 않은 노드 타입입니다: {node.nodeType}");
                    continue;
                }

                var nodeParent = nodesRoot != null ? nodesRoot : transform;
                GameObject go = Instantiate(prefab, nodeParent);
                go.name = $"{node.nodeType}_L{node.layerIndex}_I{j}";

                // 로컬 좌표계 기준으로 배치 (Gizmos와 동일 좌표 사용)
                go.transform.localPosition = new Vector3(node.position.x, node.position.y, 0f);

                // 매핑 저장 (선 그리기에 사용)
                nodeToTransform[node] = go.transform;

                // NodeGoScene 준비
                var nodeGo = go.GetComponent<NodeGoScene>();
                if (nodeGo == null) nodeGo = go.AddComponent<NodeGoScene>();

                // 타입/주소 주입
                nodeGo.SetNodeType(node.nodeType);
                // InitAddress 메서드를 만들어두었다면 해도 되고, 없으면 아래 두 줄처럼 직접 대입:
                nodeGo.floor = node.layerIndex;
                nodeGo.index = j;
                if (!string.IsNullOrEmpty(node.eventIdOverride))
                {
                    nodeGo.eventIdOverride = node.eventIdOverride;
                }
                else
                {
                    node.eventIdOverride = nodeGo.eventIdOverride ?? string.Empty;
                }

                // 버튼은 '총괄'에게 이동 요청하도록 연결 (직접 씬 이동 X)
                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(nodeGo.OnClicked); // ← 기존 GoToAssignedScene에서 변경
                }

                nodeToGo[node] = nodeGo;
                nodeObjects.Add(go);
            }
        }

        ApplyVerticalOffsetToRoots();

        // === 2패스: 런타임 children 링크 연결 ===
        foreach (var layer in mapData)
        {
            foreach (var parentNode in layer)
            {
                if (!nodeToGo.TryGetValue(parentNode, out var parentGo)) continue;

                parentGo.children.Clear();
                foreach (var child in parentNode.children)
                {  
                    if (child != null && nodeToGo.TryGetValue(child, out var childGo))
                    {
                        parentGo.children.Add(childGo);
                    }
                
                }
            }
        }

        // 모든 노드 생성 후 경로(선) 그리기
        DrawPaths();

        UpdateScrollContentBounds();

        if (mapScrollRect != null)
        {
            mapScrollRect.StopMovement();
            mapScrollRect.normalizedPosition = new Vector2(mapScrollRect.horizontalNormalizedPosition, 0f);
        }
    }

    private GameObject GetPrefabFor(NodeType type)
    {
        switch (type)
        {
            case NodeType.Battle:     return BattleNodePrefab;
            case NodeType.Elite:      return EliteNodePrefab;
            case NodeType.Boss:       return BossNodePrefab;
            case NodeType.Event:      return EventNodePrefab;
            case NodeType.Shop:       return ShopNodePrefab;
            case NodeType.Rest:       return RestNodePrefab;
            case NodeType.CardRemove: return CardRemoveNodePrefab;
            default: return null;
        }
    }
    
    private void DrawPaths()
    {
        // 기존 선 정리(안전)
        foreach (var lr in pathLines)
        {
            if (lr != null)
            {
                Destroy(lr.gameObject);
            }
        }
        pathLines.Clear();

        if (pathLinePrefab == null)
        {
            Debug.LogWarning("pathLinePrefab이 설정되지 않아 경로를 그릴 수 없습니다.");
            return;
        }

        var lineParent = pathsRoot != null ? pathsRoot : transform;

        // 모든 부모-자식 연결을 따라 선 생성
        foreach (var layer in mapData)
        {
            foreach (var parentNode in layer)
            {
                if (!nodeToTransform.TryGetValue(parentNode, out var parentTf) || parentTf == null)
                {
                    continue;
                }
                foreach (var child in parentNode.children)
                {
                    if (child == null) continue;
                    if (!nodeToTransform.TryGetValue(child, out var childTf) || childTf == null)
                    {
                        continue;
                    }

                    // 라인 생성 및 설정 (로컬 좌표 사용)
                    var lr = Instantiate(pathLinePrefab, lineParent);
                    lr.useWorldSpace = false;
                    lr.positionCount = 2;
                    Vector3 a = parentTf.localPosition;
                    Vector3 b = childTf.localPosition;
                    a.z = b.z = 0f;
                    lr.SetPosition(0, a);
                    lr.SetPosition(1, b);
                    pathLines.Add(lr);
                }
            }
        }
    }
    #endregion


    private void UpdateScrollContentBounds()
    {
        if (contentRect == null || viewportRect == null)
        {
            return;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        bool hasNode = false;

        foreach (var tf in nodeToTransform.Values)
        {
            var rect = tf as RectTransform;
            if (rect == null)
            {
                continue;
            }

            Vector2 pos = rect.anchoredPosition;
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
            hasNode = true;
        }

        if (!hasNode)
        {
            float baseHeight = viewportRect.rect.height;
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseHeight);
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportRect.rect.width);
            return;
        }

        float width = Mathf.Max(viewportRect.rect.width, (maxX - minX) + contentPadding * 2f);
        float height = Mathf.Max(viewportRect.rect.height, (maxY - minY) + contentPadding * 2f);

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (mapScrollRect != null)
        {
            bool enableVertical = height > viewportRect.rect.height + 0.5f;
            mapScrollRect.vertical = enableVertical;
        }
    }

    private float CalculateVerticalOffset()
    {
        if (contentRect == null)
        {
            return 0f;
        }

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                if (node == null) continue;
                minY = Mathf.Min(minY, node.position.y);
                maxY = Mathf.Max(maxY, node.position.y);
            }
        }

        if (minY == float.MaxValue)
        {
            return 0f;
        }

        float yOffset = -minY + contentPadding;

        float requiredHeight = (maxY - minY) + contentPadding * 2f;
        if (contentRect != null)
        {
            float height = Mathf.Max(contentRect.rect.height, requiredHeight);
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        return yOffset;
    }

    private void ApplyVerticalOffsetToRoots()
    {
        float yOffset = CalculateVerticalOffset();

        ApplyYOffset(nodesRoot, yOffset);
        ApplyYOffset(pathsRoot, yOffset);
    }

    private void ApplyYOffset(Transform target, float yOffset)
    {
        if (target == null)
        {
            return;
        }

        if (target is RectTransform rect)
        {
            var anchored = rect.anchoredPosition;
            anchored.y = yOffset;
            rect.anchoredPosition = anchored;
        }
        else
        {
            var local = target.localPosition;
            local.y = yOffset;
            target.localPosition = local;
        }
    }


    #region Gizmos를 이용한 맵 시각화
    // OnDrawGizmos 함수는 씬(Scene) 화면에서만 보이며, 개발 중 디버깅에 매우 유용합니다.
    private void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (mapData == null || mapData.Count == 0)
        {
            return;
        }

        float gizmoYOffset = nodesRoot != null ? 0f : CalculateVerticalOffset();
        Transform gizmoBaseTransform = nodesRoot != null ? nodesRoot : transform;

        // 모든 노드를 순회하며 Gizmos를 그립니다.
        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                // Gizmos가 캔버스 좌표계에 맞게 그려지도록 월드 좌표로 변환합니다.
                Vector3 localPos = new Vector3(node.position.x, node.position.y + gizmoYOffset, 0f);
                Vector3 worldPos = gizmoBaseTransform.TransformPoint(localPos);

                // 노드의 종류에 따라 다른 색상으로 원을 그립니다.
                switch (node.nodeType)
                {
                    case NodeType.Battle: Gizmos.color = Color.gray; break;
                    case NodeType.Elite: Gizmos.color = Color.red; break;
                    case NodeType.Boss: Gizmos.color = Color.magenta; break;
                    case NodeType.Event: Gizmos.color = Color.yellow; break;
                    case NodeType.Shop: Gizmos.color = Color.cyan; break;
                    case NodeType.Rest: Gizmos.color = Color.green; break;
                    case NodeType.CardRemove: Gizmos.color = Color.blue; break;
                }
                Gizmos.DrawSphere(worldPos, 1f); // 1f는 원의 크기

                // 레이어/타입 정보를 라벨로 표시하여 설계 검증을 돕습니다.
                // 라벨 위치가 어긋나는 문제를 방지하기 위해 전역 행렬을 고정하고, 월드 좌표에 직접 표시합니다.
                var prevMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.identity;
                Handles.Label(worldPos, $"L{node.layerIndex}:{node.nodeType}");
                Handles.matrix = prevMatrix;

                // 이 노드에서 자식 노드로 이어지는 선을 그립니다.
                Gizmos.color = Color.white;
                foreach (var child in node.children)
                {
                    // 자식 노드 위치도 월드 좌표로 변환하여 선을 정확하게 긋습니다.
                    Vector3 childLocal = new Vector3(child.position.x, child.position.y + gizmoYOffset, 0f);
                    Vector3 childWorldPos = gizmoBaseTransform.TransformPoint(childLocal);
                    Gizmos.DrawLine(worldPos, childWorldPos);
                }
            }
        }
        #endif
    }
    #endregion

    private void EnforceMinimumLayerSpacing()
    {
        float minGap = Mathf.Max(4f, nodeSpacing * 0.45f);
        float targetGap = Mathf.Max(nodeSpacing * 0.75f, minGap + 10f);
        float maxAdjust = nodeSpacing * 0.45f;

        for (int layerIndex = 0; layerIndex < mapData.Count; layerIndex++)
        {
            var layer = mapData[layerIndex];
            if (layer == null || layer.Count <= 1) continue;

            for (int i = 0; i < layer.Count - 1; i++)
            {
                var left = layer[i];
                var right = layer[i + 1];
                float delta = right.position.x - left.position.x;
                if (delta >= targetGap) continue;

                float needed = Mathf.Clamp((targetGap - delta) * 0.5f, 0f, maxAdjust);
                left.position = new Vector2(left.position.x - needed, left.position.y);
                right.position = new Vector2(right.position.x + needed, right.position.y);

                if (delta < minGap)
                {
                    float penalty = Mathf.Clamp((minGap - delta) * 0.25f, 0f, maxAdjust * 0.5f);
                    left.position = new Vector2(left.position.x - penalty, left.position.y);
                    right.position = new Vector2(right.position.x + penalty, right.position.y);
                }
            }
        }
    }
}

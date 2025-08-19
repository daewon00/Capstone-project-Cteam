using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;

public class MapGenerator : MonoBehaviour
{
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

    [Header("배치 정책")]
    [SerializeField] private int minEliteLayerPolicy = 1; // 엘리트 최소 레이어 정책(기본 1층)

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

    // 게임이 시작될 때 맵을 생성합니다.
    void Start()
    {
        GenerateMap();
    }

    // 맵 생성의 전체 과정을 지휘하는 메인 함수입니다.
    void GenerateMap()
    {
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
        if (mapSeed != -1)
        {
            random = new System.Random(mapSeed);
        }
        else
        {
            random = new System.Random(GenerateSeed());
        }

        // 1단계: 맵의 뼈대(노드 위치) 생성
        CreateNodePositions();

        // 2단계: 경로 생성 
        CreatePaths(); 

        // 2.5단계: 교차선 감소를 위한 배리센터 정렬(옵션)
        ApplyBarycenterOrdering();

        // 3단계: 노드 타입 결정 (나중에 추가할 함수)
        SetNodeTypes();

        // 4단계: 화면에 실제 오브젝트 생성 (중앙 함수로 자동 배치/배선)
        InstantiateMapObjects();
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

    public void RegenerateWithSeed(int seed)
    {
        this.mapSeed = seed;
        GenerateMap();
    }

    #region --- 1단계: 맵 뼈대 생성 함수 ---
    void CreateNodePositions()
    {
        Debug.Log("1단계: 맵 뼈대 생성을 시작합니다.");

        mapData.Clear(); // 맵 다시 생성시 초기화

        // 0층(시작 지점)부터 마지막 층까지 반복합니다.
        for (int i = 0; i < numberOfLayers; i++)
        {
            // 현재 층에 해당하는 노드 리스트를 새로 만듭니다.
            List<MapDataNode> currentLayerNodes = new List<MapDataNode>();

            // 이 층에 몇 개의 노드를 만들지 무작위로 결정합니다.
            int nodesInThisLayer = random.Next(minNodesPerLayer, maxNodesPerLayer + 1);

            // 6층과 7층은 규칙에 따라 노드가 1개만 있도록 강제합니다.
            if (i == 0 || i == FinalRestLayerIndex || i == BossLayerIndex)
            {
                nodesInThisLayer = 1;
            }

            // 결정된 개수만큼 노드를 생성합니다.
            for (int j = 0; j < nodesInThisLayer; j++)
            {
                // 노드의 x, y 좌표를 계산합니다.
                float yPos = i * layerSpacing;
                float xPos = (j - (nodesInThisLayer - 1) / 2f) * nodeSpacing;

                // 약간의 무작위성을 더해 맵이 너무 반듯하지 않게 만듭니다.
                xPos += random.Next((int)-positionRandomness, (int)positionRandomness + 1);
                yPos += random.Next((int)-positionRandomness, (int)positionRandomness + 1);

                // 계산된 위치에 '빈 노드' 데이터를 생성합니다. (아직 타입은 미정)
                MapDataNode newNode = new MapDataNode(NodeType.Battle, new Vector2(xPos, yPos), i); // 타입은 일단 기본값으로
                currentLayerNodes.Add(newNode);
            }

            // 완성된 층을 전체 맵 데이터에 추가합니다.
            mapData.Add(currentLayerNodes);
        }

        Debug.Log("맵 뼈대 생성 완료! 총 " + mapData.Count + "개의 층이 생성되었습니다.");
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

        float MedianOfParents(MapDataNode node)
        {
            if (node.parents != null && node.parents.Count > 0)
            {
                var xs = new List<float>(node.parents.Count);
                for (int i = 0; i < node.parents.Count; i++) xs.Add(node.parents[i].position.x);
                return Median(xs);
            }
            return node.position.x;
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
                    bary[node] = MedianOfParents(node);
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

        // 모든 노드를 순회하며 타입에 맞는 프리팹을 생성하고, 좌표를 배치합니다.
        for (int layerIndex = 0; layerIndex < mapData.Count; layerIndex++)
        {
            foreach (var node in mapData[layerIndex])
            {
                GameObject prefab = GetPrefabFor(node.nodeType);
                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹이 설정되지 않은 노드 타입입니다: {node.nodeType}");
                    continue;
                }

                var nodeParent = nodesRoot != null ? nodesRoot : transform;
                GameObject go = Instantiate(prefab, nodeParent);
                go.name = $"{node.nodeType}_L{node.layerIndex}";

                // 로컬 좌표계 기준으로 배치 (Gizmos와 동일 좌표 사용)
                go.transform.localPosition = new Vector3(node.position.x, node.position.y, 0f);
                // 매핑 저장 (선 그리기에 사용)
                nodeToTransform[node] = go.transform;

                // 런타임 자동 배선: NodeGoScene에 타입 주입 + 버튼 클릭 연결
                var nodeGo = go.GetComponent<NodeGoScene>();
                if (nodeGo == null)
                {
                    nodeGo = go.AddComponent<NodeGoScene>();
                }

                nodeGo.SetNodeType(node.nodeType);

                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    // 중복 연결 방지 후 리스너 등록
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(nodeGo.GoToAssignedScene);
                }

                nodeObjects.Add(go);
            }
        }

        // 모든 노드 생성 후 경로(선) 그리기
        DrawPaths();
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

                    // 라인 생성 및 설정 (월드 좌표 사용)
                    var lr = Instantiate(pathLinePrefab, lineParent);
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                    Vector3 a = parentTf.position;
                    Vector3 b = childTf.position;
                    a.z = b.z = 0f;
                    lr.SetPosition(0, a);
                    lr.SetPosition(1, b);
                    pathLines.Add(lr);
                }
            }
        }
    }
    #endregion


    #region Gizmos를 이용한 맵 시각화
    // OnDrawGizmos 함수는 씬(Scene) 화면에서만 보이며, 개발 중 디버깅에 매우 유용합니다.
    private void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (mapData == null || mapData.Count == 0)
        {
            return;
        }

        // 모든 노드를 순회하며 Gizmos를 그립니다.
        foreach (var layer in mapData)
        {
            foreach (var node in layer)
            {
                // Gizmos가 캔버스 좌표계에 맞게 그려지도록 월드 좌표로 변환합니다.
                Vector3 worldPos = transform.TransformPoint(node.position);

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
                    Vector3 childWorldPos = transform.TransformPoint(child.position);
                    Gizmos.DrawLine(worldPos, childWorldPos);
                }
            }
        }
        #endif
    }
    #endregion
}

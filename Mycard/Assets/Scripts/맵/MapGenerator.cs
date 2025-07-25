using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    private const int BOSS_LAYER = 7;   // 보스층은 7층
    private const int FINAL_REST_LAYER = 6; // 최종 휴식층은 6층

    [Header("맵 설정")]
    public int numberOfLayers = 8; // 맵의 전체 층 수 (0~7층)
    public int minNodesPerLayer = 1; // 층당 최소 노드 수 (1개)
    public int maxNodesPerLayer = 3; // 층당 최대 노드 수 (3개)

    [Header("랜덤 시드")]
    // 랜덤 시드. -1이면 무작위, 특정 숫자면 고정된 맵 생성
    public int mapSeed = -1;
    private System.Random random; // System.Random 사용

    [Header("노드 위치 설정")]
    public float layerSpacing = 300f; // 층(세로) 간격
    public float nodeSpacing = 200f;  // 노드(가로) 간격
    public float positionRandomness = 50f; // 노드 랜덤 간격

    [Header("프리팹 연결")]
    // 여기에 이전에 이름 정했던 프리팹들을 연결할 겁니다.
    public GameObject BattleNodePrefab;
    public GameObject EliteNodePrefab;
    public GameObject BossNodePrefab;
    public GameObject EventNodePrefab;
    public GameObject ShopNodePrefab;
    public GameObject RestNodePrefab;
    public GameObject CardRemoveNodePrefab;
    public LineRenderer pathLinePrefab;

    
    private List<List<MapDataNode>> mapData = new List<List<MapDataNode>>(); // 생성된 모든 맵 노드 데이터를 저장할 리스트입니다.

    private List<GameObject> nodeObjects = new List<GameObject>(); // 생성된 실제 노드 오브젝트들을 저장하여 선을 그릴 때 사용합니다.

    // 게임이 시작될 때 맵을 생성합니다.
    void Start()
    {
        GenerateMap();
    }

    // 맵 생성의 전체 과정을 지휘하는 메인 함수입니다.
    void GenerateMap()
    {
        // 랜덤 시드 초기화
        if (mapSeed != -1)
        {
            random = new System.Random(mapSeed);
        }
        else
        {
            random = new System.Random((int)System.DateTime.Now.Ticks);
        }

        // 1단계: 맵의 뼈대(노드 위치) 생성
        CreateNodePositions();

        // 2단계: 경로 생성 
        CreatePaths(); 

        // 3단계: 노드 타입 결정 (나중에 추가할 함수)
        // SetNodeTypes();

        // 4단계: 화면에 실제 오브젝트 생성 (나중에 추가할 함수)
        // InstantiateMapObjects();
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
            int nodesInThisLayer = Random.Range(minNodesPerLayer, maxNodesPerLayer + 1);

            // 6층과 7층은 규칙에 따라 노드가 1개만 있도록 강제합니다.
            if (i == FINAL_REST_LAYER || i == BOSS_LAYER)
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
                MapDataNode newNode = new MapDataNode(NodeType.Battle, new Vector2(xPos, yPos)); // 타입은 일단 기본값으로
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

                // Linq를 사용해 거리가 가장 가까운 자식 노드를 찾습니다.
                var closestChild = childLayer.OrderBy(child => Vector2.Distance(child.position, node.position)).FirstOrDefault();

                if (closestChild != null)
                {
                    // 양방향으로 연결해줍니다.
                    node.children.Add(closestChild);
                    closestChild.parents.Add(node);
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
                    node.children.Add(randomChild);
                    randomChild.parents.Add(node);

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
                    var closestParent = parentLayer.OrderBy(p => Vector2.Distance(p.position, node.position)).FirstOrDefault();

                    if (closestParent != null)
                    {
                        closestParent.children.Add(node);
                        node.parents.Add(closestParent);
                    }
                }
            }
        }

        // --- 규칙 2.7: 최종 경로 수렴 ---
        // 5층(인덱스 4)의 모든 노드와 6층(인덱스 5)의 휴식 노드를 연결합니다.
        var finalRestNode = mapData[FINAL_REST_LAYER][0]; // 6층의 유일한 휴식 노드
        foreach (var node in mapData[FINAL_REST_LAYER - 1]) // 5층의 모든 노드
        {
            // 기존 연결을 모두 지우고, 오직 최종 휴식 노드로만 연결합니다.
            node.children.Clear();
            node.children.Add(finalRestNode);
            finalRestNode.parents.Add(node);
        }

        Debug.Log("경로 생성 완료!");

    }
    #endregion

    #region 3단계: 노드 타입 결정 (아이콘 정하기)
        void SetNodeTypes()
    {
        // 이 함수는 다음 단계에서 채워나갈 부분입니다.
        Debug.Log("3단계: 노드 타입 결정을 시작합니다.");
    }
    #endregion

    #region 4단계: 화면에 실제 오브젝트 생성
    void InstantiateMapObjects()
    {
        // 이 함수는 마지막에 채워나갈 부분입니다.
        Debug.Log("4단계: 실제 맵 오브젝트를 생성합니다.");
    }
    #endregion


    #region Gizmos를 이용한 맵 시각화
    // OnDrawGizmos 함수는 씬(Scene) 화면에서만 보이며, 개발 중 디버깅에 매우 유용합니다.
    private void OnDrawGizmos()
    {
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
    }
    #endregion
}

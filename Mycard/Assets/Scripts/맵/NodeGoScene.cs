using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Save; // NodeType 충돌 방지 


/// <summary>
/// 맵 노드 버튼을 나타내며 이동 가능 상태와 할당된 씬 정보를 관리합니다.
/// </summary>
public class NodeGoScene : MonoBehaviour
{
    // --- 버튼이 로드할 씬 이름들 (씬이 아직 없어도 있다고 가정하고 필드만 만들어 둡니다) ---
    [SerializeField] private string battleSceneName = "Battle_android";          // 일반 전투
    [SerializeField] private string eliteSceneName = "Elite";            // 엘리트 전투
    [SerializeField] private string bossSceneName = "Boss";              // 보스 전투
    [SerializeField] private string eventSceneName = "EventScene";       // 이벤트
    [SerializeField] private string shopSceneName = "ShopScene";         // 상점
    [SerializeField] private string restSceneName = "Rest";              // 휴식
    [SerializeField] private string cardRemoveSceneName = "Event";  // 카드 제거
    [SerializeField] private string mapSceneName = "Map Scene";          // 맵으로 돌아가기

    // (옵션) 이동 가능 하이라이트 오브젝트
    [SerializeField] private GameObject reachableHighlight;

    // MapGenerator가 주입할 노드 타입 (수동 설정 제거)
    private NodeType assignedNodeType = NodeType.Battle;

    [HideInInspector] public int floor;                 // 층
    [HideInInspector] public int index;                 // 층 내 인덱스
    [HideInInspector] public List<NodeGoScene> children = new(); // 이 노드의 자식들

    Button _button;
    MapTraversalController _traversal;

    // MapTraversalController가 읽을 수 있도록 public 프로퍼티 제공
    public NodeType nodeType { get { return assignedNodeType; } }
    public string assignedScene; //이 노드가 이동할 씬의 이름
    public string eventIdOverride; // 특정 이벤트를 강제할 때 사용할 ID 변수

    void Awake()
    {
        _button = GetComponent<Button>();
        _traversal = FindFirstObjectByType<MapTraversalController>(FindObjectsInactive.Exclude);

        // 버튼 클릭은 '총괄'에게 먼저 허락 요청 → 허용되면 실제 씬 이동
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RequestMove);
        }
    }
    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClicked()
    {
        // 총괄 컨트롤러에게 먼저 이동 요청 (허용 경로 체크)
        var traversal = FindFirstObjectByType<MapTraversalController>(FindObjectsInactive.Exclude);
        if (traversal != null)
            traversal.OnNodeClicked(this);
        else
            GoToAssignedScene(); // 폴백: 컨트롤러 없으면 바로 이동
    }

    // MapGenerator가 노드 타입을 주입할 때 호출
    public void SetNodeType(NodeType type)
    {
        assignedNodeType = type;
    }

    // MapGenerator가 주소까지 세팅하려면 사용
    public void InitAddress(int floorIndex, int columnIndex)
    {
        floor = floorIndex;
        index = columnIndex;
    }

    // 이동 가능/불가 토글 (버튼/하이라이트)
    public void SetReachable(bool isReachable)
    {
        if (_button != null) _button.interactable = isReachable;
        if (reachableHighlight != null) reachableHighlight.SetActive(isReachable);
    }

    // 버튼에서 이 함수가 호출됨: 먼저 이동 요청
    private void RequestMove()
    {
        if (_traversal != null)
        {
            _traversal.OnNodeClicked(this);
        }
        else
        {
            // 폴백: 컨트롤러가 없으면 기존처럼 바로 이동
            GoToAssignedScene();
        }
    }

    // 버튼에서 이 함수 하나만 연결하면 됨
    public void GoToAssignedScene()
    {
        // 이벤트/휴식 노드는 총괄 컨트롤러가 처리하므로 직접 로드하지 않습니다.
        if (nodeType == NodeType.Event || nodeType == NodeType.Rest)
            return;

        // 이벤트가 아닐 경우에만, 기존 로직대로 씬을 찾아 이동합니다.
        if (!string.IsNullOrEmpty(assignedScene))
            UnityEngine.SceneManagement.SceneManager.LoadScene(assignedScene);
    }

    private string ResolveSceneName(NodeType type)
    {
        switch (type)
        {
            case NodeType.Battle:      return battleSceneName;
            //엘리트 배틀씬이 완성이 되어있지 않다면 일반 배틀씬으로 이동
            case NodeType.Elite:       return string.IsNullOrWhiteSpace(eliteSceneName) ? battleSceneName : eliteSceneName;
            case NodeType.Boss:        return bossSceneName;
            case NodeType.Event:       return eventSceneName;
            case NodeType.Shop:        return shopSceneName;
            case NodeType.Rest:        return restSceneName;
            case NodeType.CardRemove:  return cardRemoveSceneName;
            default:                   return mapSceneName;
        }
    }

    private void LoadSceneWithCommonHooks(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            GameLog.Warn("로드할 씬 이름이 비어있습니다. 인스펙터에서 씬 이름을 설정하세요.");
            return;
        }
        SafePlayClickSfx();
        SceneManager.LoadScene(sceneName);
    }

    // 공통 클릭 사운드 (AudioManager 없거나 미설정인 씬에서도 안전하게 호출)
    private void SafePlayClickSfx()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(0);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    // 노드 종류를 정의
    public enum NodeType
    {
        Battle, Elite, Boss, Event, Shop, Rest, CardRemove
    }

    // 이 클래스는 MonoBehaviour를 상속받지 않습니다.
    // 데이터만 담는 설계도
    public class MapDataNode
    {
        public NodeType nodeType;  // 이 노드의 종류 (전투, 상점 등)
        public Vector2 position;  // 맵 상의 좌표 (화면에 표시될 위치)
        public int layerIndex = -1; // 이 노드가 속한 층 인덱스 (생성 시 설정)

        // 이 노드에서 갈 수 있는 다음 층(자식) 노드들의 목록
        public List<MapDataNode> children = new List<MapDataNode>();
        // 이 노드로 들어오는 이전 층(부모) 노드들의 목록
        public List<MapDataNode> parents = new List<MapDataNode>();

        public bool isVisited = false;

        // 생성자: 노드를 처음 만들 때 타입/위치/층 인덱스를 지정
        public MapDataNode(NodeType type, Vector2 pos, int layerIdx)
        {
            nodeType = type;
            position = pos;
            layerIndex = layerIdx;
        }

        // (선택적 호환) 레거시 생성자: 층 인덱스 미지정 시 -1로 둠
        // 향후 사용 금지 권장
        public MapDataNode(NodeType type, Vector2 pos)
        {
            nodeType = type;
            position = pos;
            layerIndex = -1;
        }
    }

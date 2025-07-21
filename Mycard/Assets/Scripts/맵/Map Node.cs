using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapNode : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    // 전투 노드 버튼이 호출할 함수
    public void GoToBattleScene()
    {
        SceneManager.LoadScene("Battle");
    }

    // 상점 노드 버튼이 호출할 함수 아직 씬 완성 안됨
    public void GoToShopScene()
    {
        SceneManager.LoadScene("ShopScene");
    }

    // 이벤트 노드 버튼이 호출할 함수 아직 씬 완성 안됨
    public void GoToEventScene()
    {
        SceneManager.LoadScene("EventScene");
    }
}

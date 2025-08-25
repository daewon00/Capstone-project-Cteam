using System.Linq;
using System.Reflection;
using UnityEngine;
using Game.Save;

public class MapBootstrap : MonoBehaviour
{
    [Header("씬(Scene)에 있는 오브젝트 연결")]
    public DeckManager deck;
    public MapGenerator mapGenerator;

    void Start()
    {
        // 씬에 있는 오브젝트를 자동으로 찾아 연결 (인스펙터에서 연결하는 것을 잊었을 때를 대비한 안전장치)
        if (!deck) deck = FindObjectOfType<DeckManager>();
        if (!mapGenerator) mapGenerator = FindObjectOfType<MapGenerator>();

        DatabaseManager.Instance.Connect();

        // PlayerPrefs에서 현재 진행 중인 런(Run)의 ID를 가져옵니다.
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId))
        {
            Debug.LogError("[MapBootstrap] runId를 찾을 수 없습니다! 메인 메뉴에서 게임을 시작해야 합니다.");
            return;
        }

        // DB에서 해당 런 ID의 데이터를 불러옵니다.
        var data = DatabaseManager.Instance.LoadCurrentRun(runId);
        if (data == null)
        {
            Debug.LogError($"[MapBootstrap] runId({runId})에 해당하는 저장된 런 데이터를 찾을 수 없습니다.");
            return;
        }

        // 덱을 복원합니다.
        if (deck != null)
        {
            deck.InitForRun(runId, data.Cards);
        }

        // 맵을 재현합니다.
        if (mapGenerator != null)
        {
            // runId를 사용해 고유한 숫자 시드를 만들고, 맵을 다시 생성합니다.
            mapGenerator.RegenerateWithSeed(runId.GetHashCode());
        }

        Debug.Log($"[MapBootstrap] 런({runId}) 로드 완료. 카드: {data.Cards.Count}장");
    }
}
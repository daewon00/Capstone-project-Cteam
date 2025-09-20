using System.Linq;
using System.Reflection;
using UnityEngine;
using Game.Save;
using Game.Utils;

/// <summary>
/// 맵 씬 진입 시 현재 런 데이터를 복원하고 덱과 맵 생성을 초기화합니다.
/// </summary>
public class MapBootstrap : MonoBehaviour
{
    [Header("씬(Scene)에 있는 오브젝트 연결")]
    public DeckManager deck;
    public MapGenerator mapGenerator;
    

    /// <summary>
    /// 런 ID를 확인하고 덱과 맵, 유물 상태를 복구합니다.
    /// </summary>
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
            var db = DatabaseManager.Instance;
            var storedLayout = db.LoadMapLayout(runId, data.Run.Act);

            if (storedLayout != null && !string.IsNullOrEmpty(storedLayout.Json))
            {
                try
                {
                    var snapshot = JsonUtility.FromJson<MapLayoutSnapshot>(storedLayout.Json);
                    if (snapshot != null && snapshot.Nodes != null && snapshot.Nodes.Count > 0)
                    {
                        mapGenerator.BuildFromSnapshot(snapshot);
                        Debug.Log($"[MapBootstrap] 저장된 맵 레이아웃 복원 완료 (runId={runId}, act={data.Run.Act}, seed={snapshot.Seed}).");
                    }
                    else
                    {
                        Debug.LogWarning("[MapBootstrap] 저장된 맵 레이아웃이 비어 있어 재생성을 시도합니다.");
                        BuildAndPersistNewLayout(db, runId, data.Run.Act);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[MapBootstrap] 저장된 맵 레이아웃 파싱 실패: {ex.Message}. 재생성합니다.");
                    BuildAndPersistNewLayout(db, runId, data.Run.Act);
                }
            }
            else
            {
                BuildAndPersistNewLayout(db, runId, data.Run.Act);
            }
        }
        //
        RelicSystem.Instance?.LoadRelicsFromDb(runId, clearBeforeLoad: true);

        Debug.Log($"[MapBootstrap] 런({runId}) 로드 완료. 카드: {data.Cards.Count}장");

        
    }

    private void BuildAndPersistNewLayout(DatabaseManager db, string runId, int act)
    {
        if (mapGenerator == null) return;

        int seed = DeterministicHashUtility.HashToSeed(runId, $"map-act-{act}");
        var snapshot = mapGenerator.BuildWithSeed(seed);
        if (snapshot == null)
        {
            Debug.LogError("[MapBootstrap] 맵 스냅샷 생성에 실패했습니다.");
            return;
        }

        var json = JsonUtility.ToJson(snapshot);
        db.UpsertMapLayout(runId, act, json);
        Debug.Log($"[MapBootstrap] 맵 레이아웃 생성 및 저장 완료 (runId={runId}, act={act}, seed={snapshot.Seed}).");
    }
}

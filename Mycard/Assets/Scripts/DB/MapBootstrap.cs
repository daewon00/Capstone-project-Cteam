// - 덱/동료 스타팅 로드
// - RunId 해시를 MapGenerator의 mapSeed에 주입하고 GenerateMap() 다시 호출(리플렉션)
// - MapGenerator에 BuildFromSeed/SpawnVisuals가 없어도 동작

using System.Linq;
using System.Reflection;
using UnityEngine;
using Game.Save;

public class MapBootstrap : MonoBehaviour
{
    [Header("Scene Refs")]
    public DeckManager deck;            // 맵 씬에 존재하는 DeckManager 지정
    public MapGenerator mapGenerator;   // 맵 씬의 MapGenerator 지정

    void Start()
    {
        // 1) DB 연결
        DatabaseManager.Instance.Connect();

        // 2) RunId 가져오기
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId))
        {
            Debug.LogWarning("[MapBootstrap] runId 없음 (메뉴 → 동료선택 → 시작 버튼에서 runId 저장 필요)");
            return;
        }

        // 3) 저장된 런 로드
        var data = DatabaseManager.Instance.LoadCurrentRun(runId);
        if (data == null)
        {
            Debug.LogWarning("[MapBootstrap] 저장된 런이 없습니다.");
            return;
        }

        // 4) 덱이 비어 있으면: 동료 정보로 스타팅 덱/유물 구성 후 저장
        if (data.Cards.Count == 0)
        {
            // 동료 ID는 동료 선택 씬에서 PlayerPrefs("selectedCompanionId")로 저장했다고 가정
            var compId = PlayerPrefs.GetString("selectedCompanionId", "");
            var comp = Resources.LoadAll<CompanionDefinition>("Companions")
                                .FirstOrDefault(c => c.CompanionId == compId);

            // 덱 매니저 초기화(비어 있는 상태에서 시작)
            if (deck != null) deck.InitForRun(runId, null);

            // 기본 카드
            if (deck != null)
            {
                deck.CreateNewCardInstance("CARD_STRIKE", false);
                deck.CreateNewCardInstance("CARD_STRIKE", false);
                deck.CreateNewCardInstance("CARD_DEFEND", false);
            }

            // 동료 카드/유물/포션
            var relics = new System.Collections.Generic.List<RelicInPossession>();
            var potions = new System.Collections.Generic.List<PotionInPossession>();

            if (comp != null)
            {
                foreach (var cid in comp.StartingCardIds)
                    deck.CreateNewCardInstance(cid, false);

                foreach (var rid in comp.StartingRelicIds)
                    relics.Add(new RelicInPossession { RunId = runId, RelicId = rid, Stacks = 1, UsesLeft = -1 });

                // 동료 표식 유물(런 전체에서 동료 파워 체크용)
                relics.Add(new RelicInPossession { RunId = runId, RelicId = "COMP_" + comp.CompanionId, Stacks = 1, UsesLeft = -1 });

                foreach (var pid in comp.StartingPotionIds)
                    potions.Add(new PotionInPossession { RunId = runId, PotionId = pid, Charges = 1 });
            }

            // 저장
            DatabaseManager.Instance.SaveCurrentRun(
                data.Run,
                deck != null ? deck.ToCardRowsForSave() : new System.Collections.Generic.List<CardInDeck>(),
                relics,
                potions,
                nodes: data.Nodes ?? new System.Collections.Generic.List<MapNodeState>(),
                rngStates: data.RngStates ?? new System.Collections.Generic.List<RngState>()
            );

            // 다시 로드해서 동기화
            data = DatabaseManager.Instance.LoadCurrentRun(runId);
        }

        // 5) 덱 복원
        if (deck != null)
            deck.InitForRun(runId, data.Cards);

        // 6) 맵 생성: 새로 만든 '시동 버튼'을 눌러 맵을 다시 생성합니다.
        mapGenerator.RegenerateWithSeed(runId.GetHashCode());

        Debug.Log("[MapBootstrap] 초기화 완료");
    }
}
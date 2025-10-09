using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 전투에 등장할 적 덱/AI/스탯 구성을 ScriptableObject 형태로 정의합니다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyEncounter", menuName = "Battle/Enemy Encounter")]
public class EnemyEncounterConfig : ScriptableObject
{
    [Serializable]
    public class AiPatternSelection
    {
        [Tooltip("덱에서 바로 전열에 배치하는 패턴")]
        public bool 덱에서바로배치;

        [Tooltip("손패에서 무작위 위치에 배치하는 패턴")]
        public bool 손패무작위배치;

        [Tooltip("플레이어 카드를 우선 막는 수비형 패턴")]
        public bool 수비형배치;

        [Tooltip("빈 라인을 우선으로 채우는 공격형 패턴")]
        public bool 공격형배치 = true;

        public List<EnemyController.AITpye> BuildList()
        {
            var list = new List<EnemyController.AITpye>(4);
            if (덱에서바로배치) list.Add(EnemyController.AITpye.placeFromDeck);
            if (손패무작위배치) list.Add(EnemyController.AITpye.handRandomPlace);
            if (수비형배치) list.Add(EnemyController.AITpye.handDefensive);
            if (공격형배치) list.Add(EnemyController.AITpye.handAttacking);
            return list;
        }

        public void SetFromList(IEnumerable<EnemyController.AITpye> source)
        {
            덱에서바로배치 = false;
            손패무작위배치 = false;
            수비형배치 = false;
            공격형배치 = false;

            if (source == null) return;
            foreach (var entry in source)
            {
                switch (entry)
                {
                    case EnemyController.AITpye.placeFromDeck:
                        덱에서바로배치 = true;
                        break;
                    case EnemyController.AITpye.handRandomPlace:
                        손패무작위배치 = true;
                        break;
                    case EnemyController.AITpye.handDefensive:
                        수비형배치 = true;
                        break;
                    case EnemyController.AITpye.handAttacking:
                        공격형배치 = true;
                        break;
                }
            }

            if (!덱에서바로배치 && !손패무작위배치 && !수비형배치 && !공격형배치)
            {
                공격형배치 = true;
            }
        }
    }

    private static readonly EnemyController.AITpye[] 기본AI = { EnemyController.AITpye.handAttacking };

    [FormerlySerializedAs("encounterId")]
    [SerializeField, Tooltip("저장/재개 시 식별에 사용할 고유 ID입니다.")]
    private string 전투아이디 = "ENCOUNTER_DEFAULT";

    [FormerlySerializedAs("aiType")]
    [SerializeField, Tooltip("사용할 적 AI 패턴을 체크하세요. 여러 개 선택 시 동일 확률로 무작위 선택됩니다.")]
    private AiPatternSelection 사용AI패턴 = new AiPatternSelection();

    [FormerlySerializedAs("deckCards")]
    [SerializeField, Tooltip("전투 시작 시 적 덱에 들어가는 카드 목록입니다.")]
    private List<CardScriptableObject> 덱카드목록 = new List<CardScriptableObject>();

    [FormerlySerializedAs("startHandSize")]
    [SerializeField, Tooltip("전투 시작 시 적 손패에 넣을 카드 수입니다.")]
    private int 적시작손패수 = 3;

    [FormerlySerializedAs("drawPerTurn")]
    [SerializeField, Tooltip("적 턴마다 드로우할 카드 수입니다.")]
    private int 적턴마다드로우수 = 2;

    [FormerlySerializedAs("enemyBaseHealth")]
    [SerializeField, Tooltip("전투 시작 시 적 리더의 체력입니다.")]
    private int 적기본체력 = 20;

    [FormerlySerializedAs("enemyMaxMana")]
    [SerializeField, Tooltip("적 리더의 최대 마나(에너지)입니다.")]
    private int 적최대마나 = 3;

    [FormerlySerializedAs("enemyStartingMana")]
    [SerializeField, Tooltip("적 리더의 시작 마나(에너지)입니다.")]
    private int 적시작마나 = 3;

    public string EncounterId => string.IsNullOrEmpty(전투아이디) ? "ENCOUNTER_DEFAULT" : 전투아이디;
    public IReadOnlyList<CardScriptableObject> DeckCards => 덱카드목록;
    public int StartHandSize => 적시작손패수;
    public int DrawPerTurn => 적턴마다드로우수;
    public int EnemyBaseHealth => 적기본체력;
    public int EnemyMaxMana => 적최대마나;
    public int EnemyStartingMana => 적시작마나;

    public EnemyController.AITpye PickRandomAiType()
    {
        var list = 사용AI패턴 != null ? 사용AI패턴.BuildList() : null;
        if (list == null || list.Count == 0)
            return 기본AI[0];
        int index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    public void SetEncounterId(string value)
    {
        전투아이디 = value;
    }

    public void SetDeck(IEnumerable<CardScriptableObject> cards)
    {
        덱카드목록.Clear();
        if (cards == null) return;
        foreach (var card in cards)
        {
            if (card != null)
                덱카드목록.Add(card);
        }
    }

    public void SetStartHandSize(int value)
    {
        적시작손패수 = value;
    }

    public void SetDrawPerTurn(int value)
    {
        적턴마다드로우수 = value;
    }

    public void SetEnemyStats(int baseHealth, int maxMana, int startMana)
    {
        적기본체력 = baseHealth;
        적최대마나 = maxMana;
        적시작마나 = startMana;
    }

    public void SetAiOptions(IEnumerable<EnemyController.AITpye> options)
    {
        if (사용AI패턴 == null)
            사용AI패턴 = new AiPatternSelection();
        사용AI패턴.SetFromList(options);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

#region Save DTOs
[Serializable]
public class RelicSaveEntry
{
    public string id;
    public int stacks;
}
[Serializable]
public class RelicSaveData
{
    public List<RelicSaveEntry> entries = new();
}
#endregion

public class RelicSystem : MonoBehaviour
{
    public static RelicSystem Instance { get; private set; }

    [SerializeField] private RelicsUI relicsUI;   // 옵션(UI 표시)

    [Header("Relic DB (Id → SO 매핑용)")]
    [Tooltip("여기에 사용 가능한 모든 RelicData 에셋을 등록하세요.")]
    public List<RelicData> relicDatabase = new List<RelicData>();

    // 빠른 조회용: id -> SO
    private readonly Dictionary<string, RelicData> dbById = new();

    // 런타임 보유 유물(동일 id는 스택으로 합쳐진다)
    private readonly List<Relic> relics = new();

    // (선택) 유물 변화 알림: 카드 표시 갱신 등에 사용
    public event Action RelicsChanged; //추가+++
    private void FireRelicsChanged() => RelicsChanged?.Invoke(); //추가+++

    private const string PlayerPrefsKey = "relics_1";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildIndex(); // DB 인덱싱
    }

    private void Start()
    {
        //LoadRelics();//씬 시작 시 자동으로 불러오기용
        //RelicSystem.Instance.LoadRelics(); //수동으로 불러올 거면 아무 곳에서나 호출.
    }

    public void AttachUI(RelicsUI ui)
    {
        relicsUI = ui;
        relicsUI.Refresh(relics); // 이미 가진 유물들로 UI를 즉시 맞춰줌
    }

    private void OnEnable()
    {
        // 이벤트 구독
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnBattleEnd += HandleBattleEnd;
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
        GameEvents.OnCardDrawn += HandleCardDrawn;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnDamageDealt += HandleDamageDealt;

        // 체인형 수정자 연결(누가 먼저 연결되든 null 안전)
        GameEvents.ModifyPlayerAttack += ChainModifyPlayerAttack;
        GameEvents.ModifyPlayerMana += ChainModifyPlayerMana;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= HandleBattleStart;
        GameEvents.OnBattleEnd -= HandleBattleEnd;
        GameEvents.OnTurnStart -= HandleTurnStart;
        GameEvents.OnTurnEnd -= HandleTurnEnd;
        GameEvents.OnCardDrawn -= HandleCardDrawn;
        GameEvents.OnCardPlayed -= HandleCardPlayed;
        GameEvents.OnDamageDealt -= HandleDamageDealt;

        GameEvents.ModifyPlayerAttack -= ChainModifyPlayerAttack;
        GameEvents.ModifyPlayerMana -= ChainModifyPlayerMana;
    }

    #region DB & Factory
    private void BuildIndex()
    {
        dbById.Clear();
        foreach (var so in relicDatabase)
        {
            if (so == null || string.IsNullOrEmpty(so.relicId)) continue;
            dbById[so.relicId] = so; // 마지막 등록 우선
        }
    }



    // id에 따라 올바른 파생 유물 클래스를 new 해서 돌려줌
    private Relic CreateRelicFromId(string relicId, RelicData data)
    {
        // 필요할 때 아래 switch를 확장하세요.
        switch (relicId)
        {
            case "WarBanner": return new WarBannerRelic(data);//워배너
            case "ManaGem": return new ManaGem(data);//ManaGem이 있어야함
            case "HappyFlower": return new HappyFlowerRelic(data);//
            case "ExtraDraw": return new ExtraDrawRelic(data);//
            case "SheildBanner": return new ShieldBannerRelic(data);//
            case "ManaDiscount": return new ManaDiscountRelic(data);//
            case "EnemyManaLeech": return new EnemyManaLeechRelic(data);//
            case "EnemyFirstCardWeakener": return new EnemyFirstCardWeakenerRelic(data);//
            // TODO: 새 유물을 추가할 때 case 추가
            default:
                Debug.LogWarning($"[RelicSystem] 알 수 없는 relicId: {relicId}");
                return null;
        }
    }

    #endregion

    #region Public: 편한 ID 기반 추가/삭제

    public bool AddRelicById(string relicId, int stacks = 1, bool save = true)
    {
        if (string.IsNullOrEmpty(relicId)) return false;
        if (!dbById.TryGetValue(relicId, out var data))
        {
            Debug.LogWarning($"[RelicSystem] DB에 없는 relicId: {relicId}");
            return false;
        }

        // 동일 id 유물 있으면 스택만 늘림
        var existing = relics.Find(r => r.Data.relicId == relicId);
        if (existing != null)
        {
            for (int k = 0; k < Mathf.Max(0, stacks); k++)
                existing.AddStack();

            relicsUI?.UpdateStacks(existing);
            FireRelicsChanged();
            if (save) SaveRelics();
            return true;
        }

        // 새로 생성
        var relic = CreateRelicFromId(relicId, data);
        if (relic == null) return false;

        relics.Add(relic);
        relic.OnAdd();
        // stacks가 1보다 크면 추가 스택 반영
        for (int k = 1; k < Mathf.Max(1, stacks); k++)
            relic.AddStack();

        relicsUI?.AddOrStack(relic);
        FireRelicsChanged();
        if (save) SaveRelics();
        return true;
    }
    public void RemoveRelic(string relicId, bool save = true)
    {
        int idx = relics.FindIndex(r => r.Data.relicId == relicId);
        if (idx < 0) return;

        relics[idx].OnRemove();
        relics.RemoveAt(idx);
        relicsUI?.Remove(relicId);
        FireRelicsChanged();
        if (save) SaveRelics();
    }

    public void ClearRelics(bool save = true)
    {
        foreach (var r in relics) r.OnRemove();
        relics.Clear();
        relicsUI?.Refresh(relics);
        FireRelicsChanged();
        if (save) SaveRelics();
    }

    public int CountStacks(string relicId)
    {
        var r = relics.Find(x => x.Data.relicId == relicId);
        return r != null ? r.Stacks : 0;
    }


    #endregion

    public void NotifyStackChanged(Relic r)
    {
        relicsUI?.UpdateStacks(r);
        FireRelicsChanged();
    }


    #region Public API
    public void AddRelic(Relic newRelic)
    {
        // 동일 ID 스택 처리
        var existing = relics.Find(r => r.Data.relicId == newRelic.Data.relicId);
        if (existing != null && existing.Data.stackable)
        {
            existing.AddStack();
            relicsUI?.UpdateStacks(existing); // 스택 텍스트만 갱신
            FireRelicsChanged();              // <- 추가
            return;
        }

        relics.Add(newRelic);
        newRelic.OnAdd();
        relicsUI?.AddOrStack(newRelic);
        FireRelicsChanged();              // <- 추가
    }
    /*
    public void RemoveRelic(string relicId)
    {
        var idx = relics.FindIndex(r => r.Data.relicId == relicId);
        if (idx >= 0)
        {
            relics[idx].OnRemove();
            relics.RemoveAt(idx);
            //relicsUI?.Refresh(relics);
            relicsUI?.Remove(relicId);
            FireRelicsChanged();              // <- 추가
        }
    }*/
    #endregion

    #region Save / Load (PlayerPrefs + JSON)
    public void SaveRelics()
    {
        var data = new RelicSaveData();
        foreach (var r in relics)
        {
            if (r?.Data == null || string.IsNullOrEmpty(r.Data.relicId)) continue;
            data.entries.Add(new RelicSaveEntry { id = r.Data.relicId, stacks = Mathf.Max(1, r.Stacks) });
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
        // DeckController와 같은 방식: PlayerPrefs + JsonUtility【turn2file9†DeckController.cs†L32-L51】
    }

    public bool LoadRelics(bool clearBeforeLoad = true)
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return false;

        string json = PlayerPrefs.GetString(PlayerPrefsKey);
        var data = JsonUtility.FromJson<RelicSaveData>(json);
        if (data == null || data.entries == null) return false;

        if (clearBeforeLoad) ClearRelics(false);

        foreach (var e in data.entries)
        {
            // DB에 없으면 스킵(경고)
            if (!dbById.ContainsKey(e.id))
            {
                Debug.LogWarning($"[RelicSystem] 로드 실패: 알 수 없는 relicId {e.id}");
                continue;
            }
            AddRelicById(e.id, Mathf.Max(1, e.stacks), save: false);
        }

        relicsUI?.Refresh(relics);
        FireRelicsChanged();
        return true;
    }
    #endregion

    #region Event Handlers
    private void HandleBattleStart()
    {
        foreach (var r in relics) r.OnBattleStart();
    }
    private void HandleBattleEnd()
    {
        foreach (var r in relics) r.OnBattleEnd();
    }
    private void HandleTurnStart(bool isPlayer)
    {
        foreach (var r in relics) r.OnTurnStart(isPlayer);
    }
    private void HandleTurnEnd(bool isPlayer)
    {
        foreach (var r in relics) r.OnTurnEnd(isPlayer);
    }
    private void HandleCardDrawn(Card c)
    {
        foreach (var r in relics) r.OnCardDrawn(c);
    }
    private void HandleCardPlayed(Card c)
    {
        foreach (var r in relics) r.OnCardPlayed(c);
    }
    private void HandleDamageDealt(int dmg, bool fromPlayer)
    {
        foreach (var r in relics) r.OnDamageDealt(dmg, fromPlayer);
    }

    private int ChainModifyPlayerAttack(int baseAttack)
    {
        int v = baseAttack;
        foreach (var r in relics) v = r.ModifyPlayerAttack(v);
        return v;
    }
    private int ChainModifyPlayerMana(int curMana)
    {
        int v = curMana;
        foreach (var r in relics) v = r.ModifyPlayerMana(v);
        return v;
    }


    #endregion
    /*
     

    // 지급
    RelicSystem.Instance.AddRelicById("war_banner", stacks: 1);   // 스택 여러 개면 2,3...
    // 삭제
    RelicSystem.Instance.RemoveRelic("war_banner");
    // 저장/로드
    RelicSystem.Instance.SaveRelics();
    RelicSystem.Instance.LoadRelics();
     
     
     */
}


/*
 기존코드
using System.Collections;
using System.Collections.Generic;
using UnityEngine; 



 public class RelicSystem : MonoBehaviour
{
    public static RelicSystem Instance { get; private set; }

    [SerializeField] private RelicsUI relicsUI;   // 옵션(UI 표시)

    private readonly List<Relic> relics = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // 이벤트 구독
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnBattleEnd += HandleBattleEnd;
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
        GameEvents.OnCardDrawn += HandleCardDrawn;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnDamageDealt += HandleDamageDealt;

        // 체인형 수정자 연결(누가 먼저 연결되든 null 안전)
        GameEvents.ModifyPlayerAttack += ChainModifyPlayerAttack;
        GameEvents.ModifyPlayerMana += ChainModifyPlayerMana;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= HandleBattleStart;
        GameEvents.OnBattleEnd -= HandleBattleEnd;
        GameEvents.OnTurnStart -= HandleTurnStart;
        GameEvents.OnTurnEnd -= HandleTurnEnd;
        GameEvents.OnCardDrawn -= HandleCardDrawn;
        GameEvents.OnCardPlayed -= HandleCardPlayed;
        GameEvents.OnDamageDealt -= HandleDamageDealt;

        GameEvents.ModifyPlayerAttack -= ChainModifyPlayerAttack;
        GameEvents.ModifyPlayerMana -= ChainModifyPlayerMana;
    }

    public void NotifyStackChanged(Relic r)
    {
        relicsUI?.UpdateStacks(r);
        FireRelicsChanged();
    }

    public event System.Action RelicsChanged; //추가+++
    private void FireRelicsChanged() => RelicsChanged?.Invoke(); //추가+++

    #region Public API
    public void AddRelic(Relic newRelic)
    {
        // 동일 ID 스택 처리
        var existing = relics.Find(r => r.Data.relicId == newRelic.Data.relicId);
        if (existing != null && existing.Data.stackable)
        {
            existing.AddStack();
            relicsUI?.UpdateStacks(existing); // 스택 텍스트만 갱신
            FireRelicsChanged();              // <- 추가
            return;
        }

        relics.Add(newRelic);
        newRelic.OnAdd();
        relicsUI?.AddOrStack(newRelic);
        FireRelicsChanged();              // <- 추가
    }

    public void RemoveRelic(string relicId)
    {
        var idx = relics.FindIndex(r => r.Data.relicId == relicId);
        if (idx >= 0)
        {
            relics[idx].OnRemove();
            relics.RemoveAt(idx);
            //relicsUI?.Refresh(relics);
            relicsUI?.Remove(relicId);
            FireRelicsChanged();              // <- 추가
        }
    }
    #endregion

    #region Event Handlers
    private void HandleBattleStart()
    {
        foreach (var r in relics) r.OnBattleStart();
    }
    private void HandleBattleEnd()
    {
        foreach (var r in relics) r.OnBattleEnd();
    }
    private void HandleTurnStart(bool isPlayer)
    {
        foreach (var r in relics) r.OnTurnStart(isPlayer);
    }
    private void HandleTurnEnd(bool isPlayer)
    {
        foreach (var r in relics) r.OnTurnEnd(isPlayer);
    }
    private void HandleCardDrawn(Card c)
    {
        foreach (var r in relics) r.OnCardDrawn(c);
    }
    private void HandleCardPlayed(Card c)
    {
        foreach (var r in relics) r.OnCardPlayed(c);
    }
    private void HandleDamageDealt(int dmg, bool fromPlayer)
    {
        foreach (var r in relics) r.OnDamageDealt(dmg, fromPlayer);
    }

    private int ChainModifyPlayerAttack(int baseAttack)
    {
        int v = baseAttack;
        foreach (var r in relics) v = r.ModifyPlayerAttack(v);
        return v;
    }
    private int ChainModifyPlayerMana(int curMana)
    {
        int v = curMana;
        foreach (var r in relics) v = r.ModifyPlayerMana(v);
        return v;
    }
    #endregion
}

 
 
 */
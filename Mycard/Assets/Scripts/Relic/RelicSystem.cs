using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Game.Save;
using UnityEngine.SceneManagement;

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

    [SerializeField] private RelicsUI relicsUI;   // Relic UI연결 (옵션)

    [Header("Relic DB (Id -> SO)")]
    [Tooltip("So를 RelicDatabase에 저장.")]
    public List<RelicData> relicDatabase = new List<RelicData>();

    // id -> SO
    private readonly Dictionary<string, RelicData> dbById = new();

    // 
    private readonly List<Relic> relics = new();

    // 
    public event Action RelicsChanged;
    private void FireRelicsChanged() => RelicsChanged?.Invoke(); 

    private const string PlayerPrefsKey = "relics_1";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildIndex(); // DB �ε���
        
    }

    private void Start()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        //LoadRelics();//로컬 저장식으로 불러올때 사용
        LoadRelicsFromDb(runId, clearBeforeLoad: true); //DB에 연결됨
    }

    public void AttachUI(RelicsUI ui)
    {
        relicsUI = ui;
        relicsUI.Refresh(relics); // UI 있을시 자동으로 부착 새로고침
    }

    private void OnEnable()
    {
        // 이벤트가 있으면 불러옴
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnBattleEnd += HandleBattleEnd;
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
        GameEvents.OnCardDrawn += HandleCardDrawn;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnDamageDealt += HandleDamageDealt;

        
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
            dbById[so.relicId] = so; // Id로 so 인식
        }
    }



    private Relic CreateRelicFromId(string relicId, RelicData data)
    {
        if (data != null && data.HasEffectDefinitions) // ScriptableObject에 효과 정의가 있으면 데이터 기반 유물을 생성
            return new EffectDrivenRelic(data);

        // switch문으로 relic(커스텀)추가시마다 늘려줘야함
        switch (relicId)
        {
            case "WarBanner": return new WarBannerRelic(data);//
            case "ManaGem": return new ManaGem(data);//
            case "HappyFlower": return new HappyFlowerRelic(data);//
            case "ExtraDraw": return new ExtraDrawRelic(data);//
            case "SheildBanner": return new ShieldBannerRelic(data);//
            case "ManaDiscount": return new ManaDiscountRelic(data);//
            case "EnemyManaLeech": return new EnemyManaLeechRelic(data);//
            case "EnemyFirstCardWeakener": return new EnemyFirstCardWeakenerRelic(data);//
            case "COMP_COMP_Knight": return new COMP_COMP_KnightRelic(data);//
            // TODO: relic이 추가될때마다 넣어주기(커스텀relic일 경우만)
            default:
                Debug.LogWarning($"[RelicSystem] 저장되지 않는 relic입니다 relicId: {relicId}");
                return null;
        }
    }

    #endregion

    #region Public: Relic ID로 추가

    public bool AddRelicById(string relicId, int stacks = 1, bool save = true)
    {
        if (string.IsNullOrEmpty(relicId)) return false;
        if (!dbById.TryGetValue(relicId, out var data))
        {
            Debug.LogWarning($"[RelicSystem] DB에 없는 relicId: {relicId}");
            return false;
        }

        
        var existing = relics.Find(r => r.Data.relicId == relicId);
        if (existing != null)
        {
            for (int k = 0; k < Mathf.Max(0, stacks); k++)
                existing.AddStack();

            relicsUI?.UpdateStacks(existing);
            FireRelicsChanged();
            if (save) TryPersistToDbOrPrefs();
            return true;
        }

        // 
        var relic = CreateRelicFromId(relicId, data);
        if (relic == null) return false;

        relics.Add(relic);
        relic.OnAdd();
        // stacks이 증가될때마다
        for (int k = 1; k < Mathf.Max(1, stacks); k++)
            relic.AddStack();

        relicsUI?.AddOrStack(relic);
        FireRelicsChanged();
        if (save) TryPersistToDbOrPrefs();
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
        if (save) TryPersistToDbOrPrefs();
    }

    public void ClearRelics(bool save = true)
    {
        foreach (var r in relics) r.OnRemove();
        relics.Clear();
        relicsUI?.Refresh(relics);
        FireRelicsChanged();
        if (save) TryPersistToDbOrPrefs();
    }

    public int CountStacks(string relicId)
    {
        var r = relics.Find(x => x.Data.relicId == relicId);
        return r != null ? r.Stacks : 0;
    }


    #endregion

    #region Save/Load

    public void SaveRelics()
    {
        var data = new RelicSaveData();
        foreach (var r in relics)
        {
            if (r == null || r.Data == null || string.IsNullOrEmpty(r.Data.relicId)) continue;
            data.entries.Add(new RelicSaveEntry { id = r.Data.relicId, stacks = r.Stacks });
        }
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public bool LoadRelics(bool clearBeforeLoad = true)
    {
        // TODO: runId를 DB에서 불러옵니다

        var runId = PlayerPrefs.GetString("lastRunId", "");

        if (!string.IsNullOrEmpty(runId))
            return LoadRelicsFromDb(runId, clearBeforeLoad);

        // 
        if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return false;
        string json = PlayerPrefs.GetString(PlayerPrefsKey);
        var data = JsonUtility.FromJson<RelicSaveData>(json);
        if (data == null || data.entries == null) return false;

        if (clearBeforeLoad) ClearRelics(false);
        foreach (var e in data.entries)
            AddRelicById(e.id, Mathf.Max(1, e.stacks), save: false);

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
     

    // 추가시
    RelicSystem.Instance.AddRelicById("war_banner", stacks: 1);   // ���� ���� ���� 2,3...
    // 삭제시
    RelicSystem.Instance.RemoveRelic("war_banner");
    // 저장/불러오기
    RelicSystem.Instance.SaveRelics();
    RelicSystem.Instance.LoadRelics();
     
     
     */
    private void TryPersistToDbOrPrefs()
    {
        // GameInitializer/Map 시작시 id생성후 불러와짐
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId))
        {
            // 
            SaveRelics();
            return;
        }
        SaveRelicsToDb(runId);
    }
    private void SaveRelicsToDb(string runId)
    {
        var rows = new List<RelicInPossession>(relics.Count);
        foreach (var r in relics)
        {
            if (r?.Data == null || string.IsNullOrEmpty(r.Data.relicId)) continue;
            rows.Add(new RelicInPossession
            {
                RunId = runId,
                RelicId = r.Data.relicId,
                Stacks = Mathf.Max(1, r.Stacks),
                Cooldown = 0,        // 
                UsesLeft = -1,       //
                StateJson = string.Empty
            });
        }
        DatabaseManager.Instance.ReplaceRelics(runId, rows);
    }
    public bool LoadRelicsFromDb(string runId, bool clearBeforeLoad = true)
    {
        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
        var rows = loaded?.Relics;
        if (rows == null) return false;

        if (clearBeforeLoad) ClearRelics(save: false);

        foreach (var row in rows)
        {
            
            AddRelicById(row.RelicId, Mathf.Max(1, row.Stacks), save: false);
        }

        relicsUI?.Refresh(relics);
        FireRelicsChanged();
        return true;
    }


}

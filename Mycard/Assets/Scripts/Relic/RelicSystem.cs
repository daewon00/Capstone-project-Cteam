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

    [SerializeField] private RelicsUI relicsUI;   // Relic UI���� (�ɼ�)

    [Header("Relic DB (Id -> SO)")]
    [Tooltip("So�� RelicDatabase�� ����.")]
    public List<RelicData> relicDatabase = new List<RelicData>();

    // id -> SO
    private readonly Dictionary<string, RelicData> dbById = new();

    // 
    private readonly List<Relic> relics = new();

    // 
    public event Action RelicsChanged;
    private void FireRelicsChanged()
    {
        RelicsChanged?.Invoke();
        GameEvents.RaiseCardManaCostModifiersChanged();
    }
    public void NotifyRelicStateChanged() => FireRelicsChanged();

    private const string PlayerPrefsKey = "relics_1";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildIndex(); // DB ?��???
        
    }

    private void Start()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        //LoadRelics();//���� ���������� �ҷ��ö� ����
        LoadRelicsFromDb(runId, clearBeforeLoad: true); //DB�� ������
        if(RelicsUI.Instance != null)
        {
            relicsUI?.Refresh(relics);
        }
        
    }

    public void AttachUI(RelicsUI ui)
    {
        relicsUI = ui;
        relicsUI.Refresh(relics); // UI ������ �ڵ����� ���� ���ΰ�ħ
    }

    private void OnEnable()
    {
        // �̺�Ʈ�� ������ �ҷ���
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnBattleEnd += HandleBattleEnd;
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
        GameEvents.OnCardDrawn += HandleCardDrawn;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnDamageDealt += HandleDamageDealt;


        GameEvents.ModifyPlayerAttack += ChainModifyPlayerAttack;
        GameEvents.ModifyPlayerMana += ChainModifyPlayerMana;
        GameEvents.ModifyCardManaCost += ChainModifyCardManaCost;
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
        GameEvents.ModifyCardManaCost -= ChainModifyCardManaCost;
    }

    #region DB & Factory
    private void BuildIndex()
    {
        dbById.Clear();
        foreach (var so in relicDatabase)
        {
            if (so == null || string.IsNullOrEmpty(so.relicId)) continue;
            dbById[so.relicId] = so; // Id�� so �ν�
        }
    }



    private Relic CreateRelicFromId(string relicId, RelicData data)
    {
        if (data != null && data.HasEffectDefinitions) // ScriptableObject�� ȿ�� ���ǰ� ������ ������ ���� ������ ����
            return new EffectDrivenRelic(data);

        // switch������ relic(Ŀ����)�߰��ø��� �÷�������
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
            // TODO: relic�� �߰��ɶ����� �־��ֱ�(Ŀ����relic�� ���츸)
            default:
                Debug.LogWarning($"[RelicSystem] �������� �ʴ� relic�Դϴ� relicId: {relicId}");
                return null;
        }
    }

    #endregion

    #region Public: Relic ID�� �߰�

    public bool AddRelicById(string relicId, int stacks = 1, bool save = true)
    {
        if (string.IsNullOrEmpty(relicId)) return false;
        if (!dbById.TryGetValue(relicId, out var data))
        {
            Debug.LogWarning($"[RelicSystem] DB�� ���� relicId: {relicId}");
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
        // stacks�� �����ɶ�����
        for (int k = 1; k < Mathf.Max(1, stacks); k++)
            relic.AddStack();

        relicsUI?.Refresh(relics);
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
        relicsUI?.Refresh(relics);
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
        // TODO: runId�� DB���� �ҷ��ɴϴ�

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
    private int ChainModifyCardManaCost(Card card, int baseCost)
    {
        int value = baseCost;
        foreach (var relic in relics) value = relic.ModifyCardManaCost(card, value);
        return Mathf.Max(0, value);
    }


    #endregion
    /*
     

    // �߰���
    RelicSystem.Instance.AddRelicById("war_banner", stacks: 1);   // ???? ???? ???? 2,3...
    // ������
    RelicSystem.Instance.RemoveRelic("war_banner");
    // ����/�ҷ�����
    RelicSystem.Instance.SaveRelics();
    RelicSystem.Instance.LoadRelics();
     
     
     */
    private void TryPersistToDbOrPrefs()
    {
        // GameInitializer/Map ���۽� id������ �ҷ�����
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

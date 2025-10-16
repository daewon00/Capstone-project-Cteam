using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class RelicManager : MonoBehaviour
{
   /* public static RelicManager instance;
    [SerializeField] private List<ItemScriptObject> RelicCatalog = new();

    private readonly Dictionary<string, ItemScriptObject> _index = new(); // relicId -> SO
    private readonly List<Relic> _owned = new();

    public event Action OnChanged;

    private const string SAVE_KEY = "Relic_SAVE_V1";

    [Serializable] class SaveEntry { public string id; public int stacks; }
    [Serializable] class SaveBlob { public List<SaveEntry> list = new(); }

    void Awake()
    {
        
        
         
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildIndex();

        if (!Load(true))   // 저장본 없으면 기본 세팅
        {
            //SetupDefaultRelics();
            //NotifyChanged();
        }


    }

    public void BuildIndex()
    {
        _index.Clear();
        foreach (var so in RelicCatalog)
        {
            if (so == null || string.IsNullOrEmpty(so.relicId)) continue;
            if (!_index.ContainsKey(so.relicId))
                _index.Add(so.relicId, so);
            else
                Debug.LogWarning($"[RelicManager] 중복 RelicId: {so.relicId}");
        }
    }

    private void NotifyChanged() => OnChanged?.Invoke();

    private void SetupDefaultRelics()
    {
        // 기본 지급하고 싶은 퍼크가 있으면 여기서 Add
        // 예) AddById("war_banner", 1);
    }

    public bool Load(bool rebuildOwned = true)
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return false;

        if (rebuildOwned) _owned.Clear();

        var json = PlayerPrefs.GetString(SAVE_KEY);
        var blob = JsonUtility.FromJson<SaveBlob>(json);
        if (blob?.list == null) return false;

        foreach (var e in blob.list)
        {
            if (_index.TryGetValue(e.id, out var so))
                _owned.Add(new Relic(so, e.stacks));
            else
                Debug.LogWarning($"[RelicManaher] 카탈로그에 없는 relicId: {e.id}");
        }
        return true;
    }

    public void Save()
    {
        var blob = new SaveBlob
        {
            list = _owned.Select(p => new SaveEntry { id = p.ItemSo.relicId, stacks = p.Stacks }).ToList()
        };
        PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(blob));
        PlayerPrefs.Save();
    }

    public IEnumerable<Relic> Owned() => _owned;
    public bool Has(string relicId) => _owned.Any(p => p.ItemSo.relicId == relicId);
    public int GetStacks(string relicId) => _owned.FirstOrDefault(p => p.ItemSo.relicId == relicId)?.Stacks ?? 0;

    public void AddById(string relicId, int stacks = 1)
    {
        if (!_index.TryGetValue(relicId, out var so))
        {
            Debug.LogWarning($"[RelicManager] Unknown relicId : {relicId}");
            return;
        }
        AddBySO(so, stacks);
    }

    public void AddBySO(ItemScriptObject so, int stacks = 1)
    {
        var exist = _owned.FirstOrDefault(p => p.ItemSo == so || p.ItemSo.relicId == so.relicId);
        if (exist == null) _owned.Add(new Relic(so, stacks));
        else exist.AddStack(stacks);
        Save();
        NotifyChanged();
    }
    public void RemoveAll(string relicId)
    {
        _owned.RemoveAll(p => p.ItemSo.relicId == relicId);
        Save();
        NotifyChanged();
    }

    public int ApplyModifyCardAttack(int baseAttack)
        => _owned.Aggregate(baseAttack, (val, p) => p.ModifyCardAttack(val));

    public int ApplyManaGainAtTurnStart(int baseGain)
        => _owned.Aggregate(baseGain, (val, p) => p.ModifyManaAtTurnStart(val));

    public int ApplyExtraDrawAtTurnStart(int baseDraw)
        => _owned.Aggregate(baseDraw, (val, p) => p.ModifyExtraDrawAtTurnStart(val));
   */


}

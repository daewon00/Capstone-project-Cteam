using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RelicsUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent;     // GridLayoutGroup
    [SerializeField] private GameObject iconPrefab;    // Image 
    public static RelicsUI Instance;

    private readonly Dictionary<string, RelicIconUI> map = new();

    void Awake()
    {
        // 
        if (RelicSystem.Instance != null)
            RelicSystem.Instance.AttachUI(this);
    }

    // 
    public void AddOrStack(Relic relic)
    {
        if (relic == null || relic.Data == null) return;
        var id = relic.Data.relicId;
        if (string.IsNullOrEmpty(id)) return;

        if (map.TryGetValue(id, out var ui) && ui)  // ui
        {
            ui.SetStacks(relic.Stacks);
            return;
        }

        // 
        map.Remove(id);
        if (!iconPrefab || !gridParent) return;

        var go = Instantiate(iconPrefab, gridParent);
        var icon = go.GetComponent<RelicIconUI>();
        if (icon)
        {
            icon.Setup(relic.Data.icon, relic.Stacks);
            map[id] = icon;
        }
    }

    // 
    public void UpdateStacks(Relic relic)
    {
        if (relic == null || relic.Data == null) return;
        var id = relic.Data.relicId;

        if (map.TryGetValue(id, out var ui) && ui)
        {
            ui.SetStacks(relic.Stacks);
        }
        else
        {
            // UI
            map.Remove(id);
            AddOrStack(relic);
        }
    }

    // 
    public void Remove(string relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return;
        if (map.TryGetValue(relicId, out var ui))
        {
            if (ui) Destroy(ui.gameObject);  //  Destroy
            map.Remove(relicId);
        }
    }

    // 
    public void Refresh(IReadOnlyList<Relic> relics)
    {
        if (map.Count > 0)
        {
            // 
            var keys = new List<string>(map.Keys);
            foreach (var key in keys)
            {
                var ui = map[key];
                if (ui) Destroy(ui.gameObject);  // 
                map.Remove(key);
            }
        }

        if (relics == null) return;
        for (int i = 0; i < relics.Count; i++)
            AddOrStack(relics[i]);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RelicsUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent;     // GridLayoutGroup
    [SerializeField] private GameObject iconPrefab;    // Image 들어있는 프리팹
    public static RelicsUI Instance;

    private readonly Dictionary<string, RelicIconUI> map = new();

    void Awake()
    {
        // 씬에 UI가 생기면 RelicSystem에 자신을 붙임
        if (RelicSystem.Instance != null)
            RelicSystem.Instance.AttachUI(this);
    }

    // 이미 있으면 스택만 갱신, 없거나 파괴되어 있으면 새로 만듭니다.
    public void AddOrStack(Relic relic)
    {
        if (relic == null || relic.Data == null) return;
        var id = relic.Data.relicId;
        if (string.IsNullOrEmpty(id)) return;

        if (map.TryGetValue(id, out var ui) && ui)  // ui가 살아있으면
        {
            ui.SetStacks(relic.Stacks);
            return;
        }

        // 파괴되었거나 없으면 깨끗이 정리 후 새로 생성
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

    // 이미 있는 아이콘의 스택만 갱신(파괴돼 있으면 재생성)
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
            // UI가 사라졌다면 다시 만들어 준다
            map.Remove(id);
            AddOrStack(relic);
        }
    }

    // 제거 (파괴 체크 필수)
    public void Remove(string relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return;
        if (map.TryGetValue(relicId, out var ui))
        {
            if (ui) Destroy(ui.gameObject);  // 살아있을 때만 Destroy
            map.Remove(relicId);
        }
    }

    // 풀 리프레시(안전 파괴 + 재구성)
    public void Refresh(IReadOnlyList<Relic> relics)
    {
        if (map.Count > 0)
        {
            // 키 목록을 복사해 안전하게 순회
            var keys = new List<string>(map.Keys);
            foreach (var key in keys)
            {
                var ui = map[key];
                if (ui) Destroy(ui.gameObject);  // 파괴된 경우엔 접근 X
                map.Remove(key);
            }
        }

        if (relics == null) return;
        for (int i = 0; i < relics.Count; i++)
            AddOrStack(relics[i]);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManaLeechRelic : Relic
{
    // 지금까지 적용한 총 감소량(스택 변화/제거 대응)
    private int applied = 0;

    public EnemyManaLeechRelic(RelicData data) : base(data) { }

    public override void OnAdd() => ApplyOrAdjust();
    protected override void OnStacksChanged() => ApplyOrAdjust();

    public override void OnRemove()
    {
        var bc = BattleController.instance;
        if (bc == null) { applied = 0; return; }

        if (applied != 0)
        {
            // 원복
            bc.currentEnemyMaxMana = Mathf.Max(0, bc.enemyMana + applied);
            bc.enemymaxMana = Mathf.Max(0, bc.enemymaxMana + applied);
            bc.startingEnemeyMana = Mathf.Max(0, bc.startingEnemeyMana + applied);
            applied = 0;
            Debug.Log($"[Relic] {Data.displayName} 제거 → enemymaxMana/startingEnemeyMana 복구");
        }
    }

    private void ApplyOrAdjust()
    {
        var bc = BattleController.instance;
        if (bc == null) return;

        // 목표 감소량 = 스택 수(스택당 -1)
        int target = Mathf.Max(0, Stacks);
        int delta = target - applied;   // (+면 더 낮춤, -면 일부 복구)
        if (delta == 0) return;

        // 두 수치를 동일한 양만큼 조정
        bc.enemymaxMana = Mathf.Max(0, bc.enemymaxMana - delta);
        bc.startingEnemeyMana = Mathf.Max(0, bc.startingEnemeyMana - delta);
        bc.currentEnemyMaxMana = Mathf.Max(0, bc.enemyMana - delta);

        applied = target;

        Debug.Log($"[Relic] {Data.displayName} 적용: enemymaxMana={bc.enemymaxMana}, startingEnemeyMana={bc.startingEnemeyMana} (스택={Stacks})");
    }
}
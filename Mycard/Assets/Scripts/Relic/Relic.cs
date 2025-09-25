using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Relic
{
    public RelicData Data { get; private set; }
    public int Stacks { get; private set; } = 1;

    protected Relic(RelicData data)
    {
        Data = data;
    }

    public void AddStack(int n = 1)
    {
        if (!Data.stackable) return;
        Stacks = Mathf.Clamp(Stacks + n, 1, Data.maxStacks);
        //RelicSystem.Instance?.NotifyStackChanged(this);
        OnStacksChanged();
    }


    #region 생명주기(획득/제거)
    public virtual void OnAdd() { }
    public virtual void OnRemove() { }
    protected virtual void OnStacksChanged() { }
    #endregion

    #region 전투/턴/행동 훅
    public virtual void OnBattleStart() { }
    public virtual void OnBattleEnd() { }
    public virtual void OnTurnStart(bool isPlayerTurn) { }
    public virtual void OnTurnEnd(bool isPlayerTurn) { }
    public virtual void OnCardDrawn(Card card) { }
    public virtual void OnCardPlayed(Card card) { }
    public virtual void OnDamageDealt(int damage, bool isFromPlayer) { }

    // 필요시 스탯 수정 훅(체인 연결용)
    public virtual int ModifyPlayerAttack(int baseAttack) => baseAttack;
    public virtual int ModifyPlayerMana(int currentMana) => currentMana;
    public virtual int ModifyCardManaCost(Card card, int currentCost) => currentCost;
    public virtual int ModifyCardHealth(Card card, int currentHealth) => currentHealth;
    #endregion

}

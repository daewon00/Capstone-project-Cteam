using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RelicPlayModeTests : PlayModeTestBase
{
    [UnityTest]
    public IEnumerator extradraw_턴_시작_추가_드로우를_발동한다()
    {
        yield return null;
        int beforeHand = DeckService.GetHandSnapshot().Count;

        Assert.IsTrue(GrantRelic("extradraw"));

        bool received = false;
        DrawResult captured = null;
        void OnDraw(DrawResult result)
        {
            if (result != null && result.Reason == DrawReason.Relic)
            {
                received = true;
                captured = result;
            }
        }

        DeckService.OnCardsDrawn += OnDraw;
        GameEvents.RaiseTurnStart(true);
        yield return new WaitUntilWithTimeout(() => received, 5f);
        DeckService.OnCardsDrawn -= OnDraw;

        Assert.IsTrue(received, "Relic draw event not received.");
        Assert.IsNotNull(captured);
        Assert.AreEqual(DrawReason.Relic, captured.Reason);
        Assert.Greater(DeckService.GetHandSnapshot().Count, beforeHand);
    }

    [UnityTest]
    public IEnumerator HPup_유물은_최대체력을_증가시킨다()
    {
        int beforeMax = BattleController.playerMaxHealth;
        int beforeCurrent = BattleController.playerHealth;

        Assert.IsTrue(GrantRelic("HPup"));
        yield return null;

        Assert.AreEqual(beforeMax + 15, BattleController.playerMaxHealth);
        Assert.AreEqual(beforeCurrent + 15, BattleController.playerHealth);
    }

    [UnityTest]
    public IEnumerator MANAup_유물은_플레이어_마나용량을_증가시킨다()
    {
        int beforeCap = BattleController.playermaxMana;
        int beforeTurnCap = BattleController.currentPlayerMaxMana;

        Assert.IsTrue(GrantRelic("MANAup"));
        yield return null;

        Assert.AreEqual(beforeCap + 1, BattleController.playermaxMana);
        Assert.AreEqual(beforeTurnCap + 1, BattleController.currentPlayerMaxMana);
    }

    [UnityTest]
    public IEnumerator cardhp_유물은_3턴마다_카드_체력을_증가시킨다()
    {
        yield return null;
        var card = EnsureSinglePlayerCardInHand();
        int before = card.currentHealth;

        Assert.IsTrue(GrantRelic("cardhp"));
        yield return RunPlayerTurns(3);

        Assert.GreaterOrEqual(card.currentHealth, before + 2);
    }

    [UnityTest]
    public IEnumerator manadis_유물은_3턴마다_카드_코스트를_감소시킨다()
    {
        yield return null;
        var card = EnsureSinglePlayerCardInHand();
        card.manaCost = Mathf.Max(2, card.manaCost);
        card.UpdateCardDisplay();
        int before = card.manaCost;

        Assert.IsTrue(GrantRelic("manadis"));
        yield return RunPlayerTurns(3);

        Assert.AreEqual(before - 1, card.manaCost);
    }

    [UnityTest]
    public IEnumerator cardattackup_유물은_3턴마다_카드_공격력을_증가시킨다()
    {
        yield return null;
        var card = EnsureSinglePlayerCardInHand();
        card.attackPower = Mathf.Max(3, card.attackPower);
        card.UpdateCardDisplay();
        int before = card.attackPower;

        Assert.IsTrue(GrantRelic("cardattackup"));
        yield return RunPlayerTurns(3);

        Assert.AreEqual(before + 2, card.attackPower);
    }

    [UnityTest]
    public IEnumerator finalattack_유물은_HP_10이하에서_버프가_유지됐다가_만료된다()
    {
        yield return null;
        var card = EnsureSinglePlayerCardInHand();
        card.attackPower = Mathf.Max(5, card.attackPower);
        card.UpdateCardDisplay();
        int baseAttack = card.attackPower;

        BattleController.playerHealth = Mathf.Min(BattleController.playerHealth, 9);
        Assert.IsTrue(GrantRelic("finalattack"));

        yield return RunPlayerTurns(1);
        Assert.AreEqual(baseAttack + 5, card.attackPower, "Attack buff not applied.");

        yield return RunPlayerTurns(2);
        Assert.AreEqual(baseAttack, card.attackPower, "Attack buff did not expire.");
    }

    [UnityTest]
    public IEnumerator Gold_유물은_전투종료시_HP_10이하이면_골드를_지급한다()
    {
        BattleController.playerHealth = Mathf.Min(BattleController.playerHealth, 10);
        int beforeGold = WalletService.Gold;

        Assert.IsTrue(GrantRelic("Gold"));
        GameEvents.RaiseBattleEnd();
        yield return null;

        Assert.AreEqual(beforeGold + 500, WalletService.Gold);
    }

    [UnityTest]
    public IEnumerator drawManaDiscount_유물은_10턴마다_코스트를_감소시킨다()
    {
        yield return null;
        var card = EnsureSinglePlayerCardInHand();
        card.manaCost = Mathf.Max(3, card.manaCost);
        card.UpdateCardDisplay();
        int before = card.manaCost;

        Assert.IsTrue(GrantRelic("drawManaDiscount"));
        yield return RunPlayerTurns(10);

        Assert.AreEqual(before - 1, card.manaCost);
    }
}

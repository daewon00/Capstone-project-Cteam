using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class BattleFlowTests : PlayModeTestBase
{
    [UnityTest]
    public IEnumerator 전투_시작_시_초기_핸드가_정상적으로_드로우된다()
    {
        // 초기 드로우가 반영되도록 한 프레임 추가 유예
        yield return null;

        var hand = DeckService.GetHandSnapshot();
        Assert.IsNotNull(hand);
        int expected = GetConfiguredInitialHandCount();
        Assert.Greater(expected, 0, "초기 핸드 설정값을 가져오지 못했습니다.");
        Assert.AreEqual(expected, hand.Count, $"초기 핸드 카드 수가 {expected}가 아닙니다.");
    }

    [UnityTest]
    public IEnumerator 턴_종료_시_다음_턴_시작_드로우가_정상적으로_발생한다()
    {
        bool received = false;
        void OnDraw(DrawResult r)
        {
            if (r != null && r.Reason == DrawReason.TurnStart) received = true;
        }
        DeckService.OnCardsDrawn += OnDraw;
        try
        {
            BattleController.AdvanceTurn();
            yield return new WaitUntilWithTimeout(() => received, 5f);
            Assert.IsTrue(received, "OnCardsDrawn(TurnStart) 미수신");
        }
        finally
        {
            DeckService.OnCardsDrawn -= OnDraw;
        }
    }
}

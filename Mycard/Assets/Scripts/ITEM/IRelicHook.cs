using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRelicHook
{
    void OnBattleStart() { }
    void OnPlayerTurnStart() { }
    void OnPlayerCardDrawn(Card card) { }
    void OnPlayerCardPlayed(Card card) { }

    // 스탯 수정 계층(질의형)
    // 카드가 화면/전투에 표시되거나 계산될 때 호출해서 가산치 부여
    int ModifyCardAttack(int baseAttack) { return baseAttack; }
    int ModifyManaAtTurnStart(int baseGain) { return baseGain; }
    int ModifyExtraDrawAtTurnStart(int baseDraw) { return baseDraw; }


}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameEvents
{   
    
    // 전투 흐름
    public static Action OnBattleStart;                // 전투 시작
    public static Action OnBattleEnd;                  // 전투 종료

    // 턴 흐름
    public static Action<bool> OnTurnStart;            // (isPlayerTurn)
    public static Action<bool> OnTurnEnd;              // (isPlayerTurn)

    // 카드/피해 등
    public static Action<Card> OnCardDrawn;            // 카드 드로우
    public static Action<Card> OnCardPlayed;           // 카드 사용
    public static Action<int, bool> OnDamageDealt;     // (damage, isFromPlayer)

    // 스탯 조정(가변 훅, 필요하면 호출)
    public static Func<int, int> ModifyPlayerAttack;   // 누적 체인(예: 최종 공격력 = 체인 통과 결과)
    public static Func<int, int> ModifyEnemyAttack;    // 제아에게 공격 제어
    public static Func<int, int> ModifyPlayerMana;     // 플레이어 마나 수정


}

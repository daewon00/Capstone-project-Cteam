// 전투 장면에서 카드의 런타임 수치를 덱 서비스와 동기화하고 스냅샷 복원 시 역직렬화를 지원합니다.
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleDeckRuntimeSync
{
    /// <summary>
    /// 현재 필드/손패의 카드 상태를 캡처하여 덱 서비스에 갱신합니다.
    /// </summary>
    public static void UpdateCardState(Card card)
    {
        if (card == null) return;
        // 플레이어 카드만 런타임 덱 상태에 등록합니다.
        // 적 카드/소환물(ex: SummonAdjacentTokens)까지 등록하면 전투 종료 시 덱이 기하급수적으로 늘어납니다.
        // 만약 향후 카드 소유권이 적→플레이어로 바뀌는 효과를 추가한다면, 그 시점에 isPlayer 값을 true로 설정한 뒤
        // 이 메서드를 다시 호출하도록 해야 합니다. 반대로 플레이어 카드가 적에게 넘어가는 기능을 만들면 덱 서비스에서
        // 해당 카드를 제외하는 별도 처리도 함께 필요합니다.
        if (!card.isPlayer) return;
        var deckService = ServiceRegistry.Get<IDeckService>();
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        if (deckService == null) return;

        var battleState = new BattleCardState
        {
            instanceId = card.GetBattleInstanceId(),
            cardId = card.cardSO != null ? card.cardSO.CardId : string.Empty,
            currentHp = card.currentHealth,
            attack = card.attackPower,
            slotIndex = ResolveSlotIndex(card),
            isPlayer = card.isPlayer,
            rotX = card.transform.eulerAngles.x,
            rotY = card.transform.eulerAngles.y,
            rotZ = card.transform.eulerAngles.z,
            effectState = effectService?.CaptureCardState(card)
        };

        var location = ResolveLocation(card);
        deckService.UpdateBattleCardState(battleState, location);
    }

    /// <summary>
    /// 저장된 카드 속성 JSON을 BattleCardState DTO로 변환합니다.
    /// </summary>
    public static BattleCardState ParseModifiers(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<BattleCardState>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 카드의 현재 위치(손패/필드/버려진 더미)를 계산합니다.
    /// </summary>
    private static CardLocation ResolveLocation(Card card)
    {
        if (card.inHand)
            return CardLocation.Hand;

        if (card.assignedPlace != null)
            return card.isPlayer ? CardLocation.PlayerField : CardLocation.EnemyField;

        return CardLocation.DiscardPile;
    }

    /// <summary>
    /// 카드가 놓인 슬롯 인덱스를 찾습니다. 슬롯이 없으면 -1을 반환합니다.
    /// </summary>
    private static int ResolveSlotIndex(Card card)
    {
        if (card.assignedPlace == null) return -1;
        var board = CardPointsController.instance;
        if (board == null) return -1;

        var arr = card.isPlayer ? board.playerCardPoints : board.enemyCardPoints;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == card.assignedPlace) return i;
        }
        var bench = board.enemyStayPoints;
        if (!card.isPlayer && bench != null)
        {
            for (int i = 0; i < bench.Length; i++)
            {
                if (bench[i] == card.assignedPlace) return i;
            }
        }
        return -1;
    }
}

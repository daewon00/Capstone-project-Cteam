// BattleSnapshotDTO 데이터를 활용해 전투 장면을 원상 복원하는 책임을 맡습니다.
using System;
using System.Collections.Generic;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleSnapshotRestorer
{
    /// <summary>
    /// 저장된 스냅샷과 컨텍스트를 바탕으로 전투 장면 전체를 복원합니다.
    /// </summary>
    public static void Apply(BattleSnapshotDTO snapshot, BattleSceneContext context)
    {
        if (snapshot == null || context == null)
        {
            GameLog.Warn("[BattleSnapshotRestorer] Missing snapshot or context");
            return;
        }

        RestoreTurn(snapshot.turn, context.Battle);
        RestorePlayerCombat(snapshot.player, context);
        RestoreEnemyCombat(snapshot.enemy, snapshot.enemyField, snapshot.enemyBench, context);
        RestorePlayerField(snapshot.playerField, context);
        RestoreRng(snapshot.rngStates, context.RngService);
        context.Battle.MarkRestored();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BattleSnapshotRestorer] Applied snapshot turn={snapshot.turn?.turnNumber} handCountSnapshot={snapshot.player?.handInstanceIds?.Count ?? 0} playerField={snapshot.playerField?.Count ?? 0} enemyField={snapshot.enemyField?.Count ?? 0} actualHand={(context.Hand!=null ? context.Hand.heldCards.Count : -1)}");
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (context.Hand != null)
        {
            var actual = string.Join(",", context.Hand.heldCards.ConvertAll(c => c != null ? c.GetBattleInstanceId() : "<null>"));
            GameLog.Info($"[BattleSnapshotRestorer] Actual hand instances: {actual}");
        }
#endif
    }

    /// <summary>
    /// 턴 정보와 리더 실드 상태를 회복합니다.
    /// </summary>
    private static void RestoreTurn(TurnState turn, BattleController battle)
    {
        if (turn == null || battle == null) return;
        battle.SetTurnStateFromSnapshot(turn.turnNumber, (BattleController.TurnOrder)turn.phase, turn.playerMana, turn.playerMaxMana, turn.enemyMana, turn.enemyMaxMana, turn.battleEnded);
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        effectService?.RestoreLeaderShield(true, turn.playerShield);
        effectService?.RestoreLeaderShield(false, turn.enemyShield);
    }

    /// <summary>
    /// 플레이어 체력, 손패, 덱 서비스 상태를 복원합니다.
    /// </summary>
    private static void RestorePlayerCombat(PlayerCombatState player, BattleSceneContext context)
    {
        if (player == null) return;

        var battle = context.Battle;
        battle.playerHealth = player.hp;
        UIController.instance?.setPlayerHealthText(player.hp);
        context.ClearHand();

        var deckService = context.DeckService;
        if (deckService != null)
        {
            if (player.handInstanceIds != null && player.handInstanceIds.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info($"[BattleSnapshotRestorer] Restoring hand from snapshot list: {string.Join(",", player.handInstanceIds)}");
#endif
                foreach (var instanceId in player.handInstanceIds)
                {
                    var runtime = deckService.GetCardByInstanceId(instanceId);
                    if (runtime == null)
                    {
                        GameLog.Warn($"[BattleSnapshotRestorer] Missing runtime for hand card {instanceId}");
                        continue;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    GameLog.Info($"[BattleSnapshotRestorer] Spawning hand card instance={runtime.InstanceId} cardId={runtime.CardId}");
#endif
                    context.SpawnCardInHand(runtime);
                }
            }
            else
            {
                foreach (var card in deckService.GetCardsInLocation(CardLocation.Hand))
                {
                    context.SpawnCardInHand(card);
                }
            }
        }
    }

    /// <summary>
    /// 플레이어 필드 슬롯에 스냅샷된 카드를 다시 배치합니다.
    /// </summary>
    private static void RestorePlayerField(List<PlayerBoardSlotState> board, BattleSceneContext context)
    {
        var cardPoints = CardPointsController.instance;
        if (cardPoints == null || board == null) return;

        // Clear existing board
        context.ClearPlayerField();

        var deckService = context.DeckService;
        foreach (var slot in board)
        {
            if (slot == null) continue;
            var runtime = deckService?.GetCardByInstanceId(slot.instanceId);
            if (runtime == null)
            {
                GameLog.Warn($"[BattleSnapshotRestorer] Player field restore missing runtime for {slot.instanceId}");
                continue;
            }
            context.SpawnPlayerFieldCard(slot, runtime);
        }
    }

    /// <summary>
    /// 적 체력과 벤치/전열 카드를 EnemyController로 전달해 복구합니다.
    /// </summary>
    private static void RestoreEnemyCombat(EnemyCombatState enemy, List<EnemyBoardSlotState> frontline, List<EnemyBoardSlotState> bench, BattleSceneContext context)
    {
        var enemyController = EnemyController.instance;
        if (enemyController == null) return;

        context.Battle.enemyHealth = enemy?.hp ?? context.Battle.enemyHealth;
        UIController.instance?.setEnemyHealthText(context.Battle.enemyHealth);

        enemyController.RestoreStateFromSnapshot(enemy, frontline, bench, context);
    }

    /// <summary>
    /// RNG 역직렬화 포인트를 남겨 향후 구현을 용이하게 합니다.
    /// </summary>
    private static void RestoreRng(List<RngDomainState> rngStates, IRngService rngService)
    {
        // IRngService currently does not expose state injection; skip until supported.
    }
}

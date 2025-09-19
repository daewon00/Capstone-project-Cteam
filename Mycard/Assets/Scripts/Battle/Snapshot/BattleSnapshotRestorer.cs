using System;
using System.Collections.Generic;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleSnapshotRestorer
{
    public static void Apply(BattleSnapshotDTO snapshot, BattleSceneContext context)
    {
        if (snapshot == null || context == null)
        {
            Debug.LogWarning("[BattleSnapshotRestorer] Missing snapshot or context");
            return;
        }

        RestoreTurn(snapshot.turn, context.Battle);
        RestorePlayerCombat(snapshot.player, context);
        RestoreEnemyCombat(snapshot.enemy, snapshot.enemyField, snapshot.enemyBench, context);
        RestorePlayerField(snapshot.playerField, context);
        RestoreRng(snapshot.rngStates, context.RngService);
        context.Battle.MarkRestored();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleSnapshotRestorer] Applied snapshot turn={snapshot.turn?.turnNumber} handCountSnapshot={snapshot.player?.handInstanceIds?.Count ?? 0} playerField={snapshot.playerField?.Count ?? 0} enemyField={snapshot.enemyField?.Count ?? 0} actualHand={(context.Hand!=null ? context.Hand.heldCards.Count : -1)}");
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (context.Hand != null)
        {
            var actual = string.Join(",", context.Hand.heldCards.ConvertAll(c => c != null ? c.GetBattleInstanceId() : "<null>"));
            Debug.Log($"[BattleSnapshotRestorer] Actual hand instances: {actual}");
        }
#endif
    }

    private static void RestoreTurn(TurnState turn, BattleController battle)
    {
        if (turn == null || battle == null) return;
        battle.SetTurnStateFromSnapshot(turn.turnNumber, (BattleController.TurnOrder)turn.phase, turn.playerMana, turn.playerMaxMana, turn.enemyMana, turn.enemyMaxMana, turn.battleEnded);
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        effectService?.RestoreLeaderShield(true, turn.playerShield);
        effectService?.RestoreLeaderShield(false, turn.enemyShield);
    }

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
                Debug.Log($"[BattleSnapshotRestorer] Restoring hand from snapshot list: {string.Join(",", player.handInstanceIds)}");
#endif
                foreach (var instanceId in player.handInstanceIds)
                {
                    var runtime = deckService.GetCardByInstanceId(instanceId);
                    if (runtime == null)
                    {
                        Debug.LogWarning($"[BattleSnapshotRestorer] Missing runtime for hand card {instanceId}");
                        continue;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[BattleSnapshotRestorer] Spawning hand card instance={runtime.InstanceId} cardId={runtime.CardId}");
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
                Debug.LogWarning($"[BattleSnapshotRestorer] Player field restore missing runtime for {slot.instanceId}");
                continue;
            }
            context.SpawnPlayerFieldCard(slot.slotIndex, runtime);
        }
    }

    private static void RestoreEnemyCombat(EnemyCombatState enemy, List<EnemyBoardSlotState> frontline, List<EnemyBoardSlotState> bench, BattleSceneContext context)
    {
        var enemyController = EnemyController.instance;
        if (enemyController == null) return;

        context.Battle.enemyHealth = enemy?.hp ?? context.Battle.enemyHealth;
        UIController.instance?.setEnemyHealthText(context.Battle.enemyHealth);

        enemyController.RestoreStateFromSnapshot(enemy, frontline, bench, context);
    }

    private static void RestoreRng(List<RngDomainState> rngStates, IRngService rngService)
    {
        // IRngService currently does not expose state injection; skip until supported.
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleSnapshotBuilder
{
    public static BattleSnapshotDTO Capture(string reason = "")
    {
        var battle = BattleController.instance;
        var hand = HandController.instance;
        var board = CardPointsController.instance;
        var enemy = EnemyController.instance;
        var deckService = ServiceRegistry.Get<IDeckService>();
        var rngService = ServiceRegistry.Get<IRngService>();
        var effectService = ServiceRegistry.Get<ICardEffectService>();

        if (battle == null || hand == null || board == null)
        {
            Debug.LogWarning("[BattleSnapshotBuilder] Missing core controller, snapshot skipped.");
            return null;
        }

        var dto = new BattleSnapshotDTO
        {
            turn = new TurnState
            {
                turnNumber = battle.CurrentTurnNumber,
                phase = (int)battle.CurrentPhase,
                playerTurn = battle.CurrentPhase == BattleController.TurnOrder.playerActive || battle.CurrentPhase == BattleController.TurnOrder.playerCardAttacks,
                playerMana = battle.playerMana,
                playerMaxMana = battle.currentPlayerMaxMana,
                enemyMana = battle.enemyMana,
                enemyMaxMana = battle.currentEnemyMaxMana,
                battleEnded = battle.battleEnded
            },
            player = new PlayerCombatState
            {
                hp = battle.playerHealth,
                maxHp = battle.currentPlayerMaxMana, // fallback; actual max hp not tracked
                baseHp = battle.playerHealth,
                handInstanceIds = CollectHandInstanceIds(hand)
            },
            enemy = CaptureEnemyState(enemy),
            playerField = CapturePlayerField(board),
            enemyField = CaptureEnemyField(board.enemyCardPoints, true, effectService),
            enemyBench = CaptureEnemyField(board.enemyStayPoints, false, effectService),
            rngStates = CaptureRng(rngService),
            reason = reason,
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };

        if (effectService != null && dto.turn != null)
        {
            dto.turn.playerShield = effectService.GetLeaderShield(true);
            dto.turn.enemyShield = effectService.GetLeaderShield(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleSnapshotBuilder] Capture reason='{reason}' turn={dto.turn.turnNumber} phase={dto.turn.phase} handCount={dto.player.handInstanceIds?.Count ?? 0} playerField={dto.playerField?.Count ?? 0} enemyField={dto.enemyField?.Count ?? 0} playerMana={dto.turn.playerMana}/{dto.turn.playerMaxMana} enemyMana={dto.turn.enemyMana}/{dto.turn.enemyMaxMana}");
        if (dto.player.handInstanceIds != null)
        {
            Debug.Log("[BattleSnapshotBuilder] Hand instances: " + string.Join(",", dto.player.handInstanceIds));
        }
#endif

        return dto;
    }

    private static List<string> CollectHandInstanceIds(HandController hand)
    {
        var list = new List<string>(hand.heldCards.Count);
        foreach (var card in hand.heldCards)
        {
            if (card == null) continue;
            list.Add(card.GetBattleInstanceId());
        }
        return list;
    }

    private static EnemyCombatState CaptureEnemyState(EnemyController enemy)
    {
        var state = new EnemyCombatState
        {
            hp = BattleController.instance.enemyHealth,
            maxHp = BattleController.instance.currentEnemyMaxMana, // fallback: enemy max hp not tracked separately
            aiType = enemy != null ? enemy.enemyAIType.ToString() : string.Empty,
            deckCardIds = new List<string>(),
            handCardIds = new List<string>(),
            stagedCards = new List<EnemyCardState>()
        };

        if (enemy != null)
        {
            if (enemy.ActiveDeck != null)
                state.deckCardIds.AddRange(enemy.ActiveDeck.Select(c => c != null ? c.CardId : string.Empty));
            if (enemy.CurrentHand != null)
                state.handCardIds.AddRange(enemy.CurrentHand.Select(c => c != null ? c.CardId : string.Empty));
            if (enemy.StagedCards != null)
            {
                foreach (var card in enemy.StagedCards)
                {
                    if (card == null) continue;
                    state.stagedCards.Add(new EnemyCardState
                    {
                        cardId = card.cardSO != null ? card.cardSO.CardId : string.Empty,
                        currentHp = card.currentHealth,
                        attack = card.attackPower,
                        instanceId = card.GetBattleInstanceId()
                    });
                }
            }
        }

        return state;
    }

    private static List<PlayerBoardSlotState> CapturePlayerField(CardPointsController board)
    {
        var list = new List<PlayerBoardSlotState>();
        for (int i = 0; i < board.playerCardPoints.Length; i++)
        {
            var slot = board.playerCardPoints[i];
            if (slot == null) continue;
            var card = slot.activeCard;
            if (card == null) continue;
            list.Add(new PlayerBoardSlotState
            {
                instanceId = card.GetBattleInstanceId(),
                slotIndex = i
            });
        }
        return list;
    }

    private static List<EnemyBoardSlotState> CaptureEnemyField(CardPlacePoint[] points, bool isFrontline, ICardEffectService effectService)
    {
        var list = new List<EnemyBoardSlotState>();
        if (points == null) return list;
        for (int i = 0; i < points.Length; i++)
        {
            var slot = points[i];
            if (slot == null || slot.activeCard == null) continue;
            var card = slot.activeCard;
            list.Add(new EnemyBoardSlotState
            {
                instanceId = card.GetBattleInstanceId(),
                cardId = card.cardSO != null ? card.cardSO.CardId : string.Empty,
                slotIndex = i,
                currentHp = card.currentHealth,
                attack = card.attackPower,
                effectState = effectService?.CaptureCardState(card)
            });
        }
        return list;
    }

    private static List<RngDomainState> CaptureRng(IRngService rng)
    {
        var list = new List<RngDomainState>();
        if (rng == null) return list;
        try
        {
            var states = rng.GetStatesForSave();
            if (states != null)
            {
                foreach (var s in states)
                {
                    list.Add(new RngDomainState
                    {
                        domain = s.Domain,
                        seed = s.Seed,
                        stateA = s.StateA,
                        stateB = s.StateB,
                        step = s.Step
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BattleSnapshotBuilder] CaptureRng failed: {e.Message}");
        }
        return list;
    }
}

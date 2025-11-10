// 현재 전투 장면의 주요 상태를 수집해 BattleSnapshotDTO로 직렬화하는 유틸리티입니다.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public static class BattleSnapshotBuilder
{
    /// <summary>
    /// 전투 진행 상황을 캡처하여 저장 가능한 DTO를 생성합니다.
    /// </summary>
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
            GameLog.Warn("[BattleSnapshotBuilder] Missing core controller, snapshot skipped.");
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
        GameLog.Info($"[BattleSnapshotBuilder] Capture reason='{reason}' turn={dto.turn.turnNumber} phase={dto.turn.phase} handCount={dto.player.handInstanceIds?.Count ?? 0} playerField={dto.playerField?.Count ?? 0} enemyField={dto.enemyField?.Count ?? 0} playerMana={dto.turn.playerMana}/{dto.turn.playerMaxMana} enemyMana={dto.turn.enemyMana}/{dto.turn.enemyMaxMana}");
        if (dto.player.handInstanceIds != null)
        {
            GameLog.Info("[BattleSnapshotBuilder] Hand instances: " + string.Join(",", dto.player.handInstanceIds));
        }
#endif

        return dto;
    }

    /// <summary>
    /// 현재 손패 카드들의 인스턴스 ID 목록을 수집합니다.
    /// </summary>
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

    /// <summary>
    /// 적 컨트롤러 상태를 기반으로 적 전투 정보를 기록합니다.
    /// </summary>
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

    /// <summary>
    /// 플레이어 필드 슬롯에 있는 카드 인스턴스를 기록합니다.
    /// </summary>
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
                slotIndex = i,
                rotX = card.transform.eulerAngles.x,
                rotY = card.transform.eulerAngles.y,
                rotZ = card.transform.eulerAngles.z
            });
        }
        return list;
    }

    /// <summary>
    /// 적 필드 또는 벤치 슬롯을 순회하여 카드 상태와 효과 정보를 수집합니다.
    /// </summary>
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

    /// <summary>
    /// RNG 서비스에 등록된 도메인 상태를 추출합니다.
    /// </summary>
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
            GameLog.Warn($"[BattleSnapshotBuilder] CaptureRng failed: {e.Message}");
        }
        return list;
    }
}

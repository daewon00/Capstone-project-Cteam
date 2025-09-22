// BattleSnapshotBuilder와 Restorer가 공유하는 전투 상태 DTO 정의를 모아둡니다.
using System;
using System.Collections.Generic;

namespace BattleSnapshot
{
    /// <summary>
    /// 전투 전반의 진행 정보를 담는 루트 DTO입니다.
    /// </summary>
    [Serializable]
    public class BattleSnapshotDTO
    {
        public TurnState turn;
        public PlayerCombatState player;
        public EnemyCombatState enemy;
        public List<PlayerBoardSlotState> playerField;
        public List<EnemyBoardSlotState> enemyField;
        public List<EnemyBoardSlotState> enemyBench;
        public List<RngDomainState> rngStates;
        public string reason;
        public string savedAtUtc;
    }

    /// <summary>
    /// 턴 순서와 마나 등 턴 관련 데이터를 담습니다.
    /// </summary>
    [Serializable]
    public class TurnState
    {
        public int turnNumber;
        public int phase;
        public bool playerTurn;
        public int playerMana;
        public int playerMaxMana;
        public int enemyMana;
        public int enemyMaxMana;
        public bool battleEnded;
        public int playerShield;
        public int enemyShield;
    }

    /// <summary>
    /// 플레이어의 체력과 손패 정보 등 전투 상태를 나타냅니다.
    /// </summary>
    [Serializable]
    public class PlayerCombatState
    {
        public int hp;
        public int maxHp;
        public int baseHp;
        public List<string> handInstanceIds;
    }

    /// <summary>
    /// 적 AI 유형, 덱 구성 등 전투 데이터 스냅샷입니다.
    /// </summary>
    [Serializable]
    public class EnemyCombatState
    {
        public int hp;
        public int maxHp;
        public string aiType;
        public List<string> deckCardIds;
        public List<string> handCardIds;
        public List<EnemyCardState> stagedCards;
    }

    /// <summary>
    /// 적이 준비 중인 개별 카드 정보를 담습니다.
    /// </summary>
    [Serializable]
    public class EnemyCardState
    {
        public string cardId;
        public int currentHp;
        public int attack;
        public string instanceId;
    }

    /// <summary>
    /// 플레이어 필드 슬롯에서 카드의 배치 정보를 제공합니다.
    /// </summary>
    [Serializable]
    public class PlayerBoardSlotState
    {
        public string instanceId;
        public int slotIndex;
        public float rotX;
        public float rotY;
        public float rotZ;
    }

    /// <summary>
    /// 적 필드 또는 벤치 슬롯의 카드 상태를 기록합니다.
    /// </summary>
    [Serializable]
    public class EnemyBoardSlotState
    {
        public string instanceId;
        public string cardId;
        public int slotIndex;
        public int currentHp;
        public int attack;
        public CardEffectRuntimeSnapshot effectState;
    }

    /// <summary>
    /// RNG 서비스의 각 도메인 상태를 보존합니다.
    /// </summary>
    [Serializable]
    public class RngDomainState
    {
        public string domain;
        public ulong seed;
        public ulong stateA;
        public ulong stateB;
        public long step;
    }
}

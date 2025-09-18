using System;
using System.Collections.Generic;

namespace BattleSnapshot
{
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
    }

    [Serializable]
    public class PlayerCombatState
    {
        public int hp;
        public int maxHp;
        public int baseHp;
        public List<string> handInstanceIds;
    }

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

    [Serializable]
    public class EnemyCardState
    {
        public string cardId;
        public int currentHp;
        public int attack;
        public string instanceId;
    }

    [Serializable]
    public class PlayerBoardSlotState
    {
        public string instanceId;
        public int slotIndex;
    }

    [Serializable]
    public class EnemyBoardSlotState
    {
        public string instanceId;
        public string cardId;
        public int slotIndex;
        public int currentHp;
        public int attack;
    }

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

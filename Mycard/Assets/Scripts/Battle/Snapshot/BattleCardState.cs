using System;

namespace BattleSnapshot
{
    [Serializable]
    public class BattleCardState
    {
        public string instanceId;
        public string cardId;
        public int currentHp;
        public int attack;
        public int slotIndex;
        public bool isPlayer;
        public CardEffectRuntimeSnapshot effectState;
    }
}

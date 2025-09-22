// 전투 스냅샷 저장 시 개별 카드의 직렬화 가능한 상태 데이터를 보관합니다.
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
        public float rotX;
        public float rotY;
        public float rotZ;
        public CardEffectRuntimeSnapshot effectState;
    }
}

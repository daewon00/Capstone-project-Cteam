using UnityEngine;
using BattleSnapshot;

namespace Game.Save
{
    /// <summary>
    /// 카드 런타임 상태의 부가 정보를 직렬화/역직렬화하기 위한 도우미입니다.
    /// </summary>
    [System.Serializable]
    public class CardRuntimeMetadata
    {
        public bool upgraded;
        public BattleCardState lastKnownState;

        public static CardRuntimeMetadata FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new CardRuntimeMetadata();

            if (json.Contains("\"lastKnownState\""))
            {
                try
                {
                    var meta = JsonUtility.FromJson<CardRuntimeMetadata>(json);
                    return meta ?? new CardRuntimeMetadata();
                }
                catch { return new CardRuntimeMetadata(); }
            }

            try
            {
                var legacy = JsonUtility.FromJson<BattleCardState>(json);
                if (legacy != null && !string.IsNullOrEmpty(legacy.instanceId))
                {
                    return new CardRuntimeMetadata
                    {
                        lastKnownState = legacy,
                        upgraded = legacy.isUpgraded
                    };
                }
            }
            catch { }

            return new CardRuntimeMetadata();
        }

        public string ToJson() => JsonUtility.ToJson(this);
    }

    public static class CardRuntimeStateExtensions
    {
        public static CardRuntimeMetadata GetMetadata(this CardRuntimeState state)
        {
            if (state == null) return new CardRuntimeMetadata();
            return CardRuntimeMetadata.FromJson(state.ModifiersJson);
        }

        public static bool IsUpgraded(this CardRuntimeState state)
        {
            return state?.GetMetadata().upgraded ?? false;
        }

        public static void SetUpgraded(this CardRuntimeState state, bool upgraded)
        {
            if (state == null) return;
            var meta = state.GetMetadata() ?? new CardRuntimeMetadata();
            meta.upgraded = upgraded;
            state.ModifiersJson = meta.ToJson();
        }

        public static void SetSnapshot(this CardRuntimeState state, BattleCardState snapshot)
        {
            if (state == null) return;
            var meta = state.GetMetadata() ?? new CardRuntimeMetadata();
            meta.lastKnownState = snapshot;
            if (snapshot != null)
            {
                meta.upgraded = snapshot.isUpgraded || meta.upgraded;
            }
            state.ModifiersJson = meta.ToJson();
        }
    }
}

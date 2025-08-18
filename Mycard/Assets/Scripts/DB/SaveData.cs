using System;
using System.Collections.Generic;
using SQLite;

namespace Game.Save
{
    // ==== 게임 전체에서 공통으로 사용하는 데이터 종류들 ====
    //카드 위치
    public enum CardLocation { Master = 0, DrawPile = 1, DiscardPile = 2, ExhaustPile = 3, Hand = 4 }
    //노드 종류
    public enum NodeType { Battle, Elite, Boss, Event, Shop, Rest, CardRemove }


    // ==== 프로필(영구 저장 데이터) ====

    // 플레이어 계정의 가장 기본적인 정보. 게임 전체에 단 하나만 존재합니다.
    [Table("PlayerProfile")]
    public class PlayerProfile
    {
        [PrimaryKey]
        public string ProfileId { get; set; }           // 플레이어 프로필의 고유 식별자 (UUID).
        public int SchemaVersion { get; set; }          // 세이브 파일의 버전. 게임 업데이트 후 데이터 구조가 바뀌었을 때 안전하게 변환하기 위해 필수적입니다.
        //public string DisplayName { get; set; }         // 플레이어 이름 (프로필 여러개 만들거라면 필요)
        public string CreatedAtUtc { get; set; }        // 프로필 생성 시각 (UTC 기준).
        public string UpdatedAtUtc { get; set; }        // 마지막으로 저장한 시각 (UTC 기준).

        public string AppVersion { get; set; }          //앱 버젼
        public int UnspentPerkPoints { get; set; }      // 사용하지 않고 보유 중인 특전 포인트.
    }

    /// <summary>
    /// 플레이어가 투자한 특전(Perk)의 종류와 레벨을 저장합니다.
    /// </summary>
    [Table("PerkAllocation")]
    public class PerkAllocation
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; } // 데이터베이스 내부 관리용 숫자 ID.
        [Indexed] public string ProfileId { get; set; }         // 어떤 프로필에 속한 특전인지 연결.
        [Indexed] public string PerkId { get; set; }            // 어떤 종류의 특전인지 식별 (예: "PERK_HP_UP").
        public int Level { get; set; }                          // 해당 특전의 현재 레벨.
    }

    /// <summary>
    /// 플레이어가 해금한 카드 목록을 저장합니다.
    /// </summary>
    [Table("UnlockedCard")]
    public class UnlockedCard
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string ProfileId { get; set; }
        [Indexed] public string CardId { get; set; } // 해금된 카드의 고유 ID.
    }

    /// <summary>
    /// 플레이어가 해금한 유물 목록을 저장합니다.
    /// </summary>
    [Table("UnlockedRelic")]
    public class UnlockedRelic
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string ProfileId { get; set; }
        [Indexed] public string RelicId { get; set; } // 해금된 유물의 고유 ID.
    }

    /// <summary>
    /// 플레이어가 해금한 동료 목록을 저장합니다.
    /// </summary>
    [Table("UnlockedCompanion")]
    public class UnlockedCompanion
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string ProfileId { get; set; }
        [Indexed] public string CompanionId { get; set; } // 해금된 동료의 고유 ID.
    }

    /// <summary>
    /// 플레이어가 달성한 업적 목록을 저장합니다.
    /// </summary>
    [Table("AchievementUnlocked")]
    public class AchievementUnlocked
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string ProfileId { get; set; }
        [Indexed] public string AchievementId { get; set; } // 달성한 업적의 고유 ID.
    }

    /// <summary>
    /// 플레이어가 완료한 게임(Run)의 요약 정보를 저장합니다. (클리어 기록, 리더보드용)
    /// </summary>
    [Table("RunSummary")]
    public class RunSummary
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string ProfileId { get; set; }
        [Indexed] public string RunId { get; set; } // 해당 판의 고유 식별자.
        public string CompanionId { get; set; }             // 동료 ID.
        public int Score { get; set; }                      // 최종 점수.
        public int Ascension { get; set; }                  // 승천(난이도) 레벨.
        public bool Cleared { get; set; }                   // 클리어 여부.
        public int DurationSeconds { get; set; }            // 플레이 시간 (초).
        public string Seed { get; set; }                    // 해당 판의 맵 시드.
        public string EndedAtUtc { get; set; }              // 게임이 끝난 시각.
    }

    //=========================================================================================
    // ==== 💾 일시 저장 데이터 (Current Run) ====
    // "이어하기" 기능을 위한 데이터입니다. 한 판이 끝나면 모두 삭제됩니다.
    //=========================================================================================

    /// <summary>
    /// 현재 진행 중인 게임 한 판(Run)의 전반적인 상태를 저장합니다.
    /// </summary>
    [Table("CurrentRun")]
    public class CurrentRun
    {
        [PrimaryKey] public string RunId { get; set; } // 현재 판의 고유 식별자.
        [Indexed] public string ProfileId { get; set; }
        public int Act { get; set; }                            // 현재 챕터.
        public int Floor { get; set; }                          // 현재 층.
        public int NodeIndex { get; set; }                      // 현재 몇 번째 노드에 있는지 (예: 0, 1, 2).
        public int Gold { get; set; }                           // 현재 골드.

        public int CurrentHp { get; set; }                      // 현재 체력.
        public int MaxHpBase { get; set; }                      // 기본 최대 체력.
        public int MaxHpFromPerks { get; set; }                 // 특전으로 증가한 최대 체력.
        public int MaxHpFromRelics { get; set; }                // 유물로 증가한 최대 체력.

        public int EnergyMax { get; set; }                      // 최대 에너지.
        public int Keys { get; set; }                           // 보유한 열쇠 개수.

        public string CreatedAtUtc { get; set; }                // 이번 판을 시작한 시각.
        public string UpdatedAtUtc { get; set; }                // 마지막으로 저장한 시각.
        public string ContentCatalogVersion { get; set; }        // 어떤 버전의 콘텐츠로 플레이했는지 기록.
        public string AppVersion { get; set; }
    }

    /// <summary>
    /// 현재 덱에 있는 카드 한 장 한 장의 상태를 개별적으로 저장합니다.
    /// </summary>
    [Table("CardInDeck")]
    public class CardInDeck
    {
        [PrimaryKey] public string InstanceId { get; set; }  // 이번 판에서만 사용되는 카드의 고유 시리얼 번호.
        [Indexed] public string RunId { get; set; }
        [Indexed] public string CardId { get; set; }                 // 카드의 종류 (예: "CARD_STRIKE").
        public bool IsUpgraded { get; set; }                         // 강화 여부.

    }

    /// <summary>
    /// 현재 보유 중인 유물과 그 상태를 저장합니다.
    /// </summary>
    [Table("RelicInPossession")]
    public class RelicInPossession
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string RunId { get; set; }
        [Indexed] public string RelicId { get; set; }
        public int Stacks { get; set; }         // 중첩 횟수.
        public int Cooldown { get; set; }       // 남은 쿨타임.
        public int UsesLeft { get; set; }       // 남은 사용 횟수 (-1은 무한).
        public string StateJson { get; set; }   // 기타 복잡한 상태를 JSON으로 저장.
    }

    /// <summary>
    /// 현재 보유 중인 포션과 그 상태를 저장합니다.
    /// </summary>
    [Table("PotionInPossession")]
    public class PotionInPossession
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string RunId { get; set; }
        [Indexed] public string PotionId { get; set; }
        public int Charges { get; set; }        // 남은 사용 횟수.
    }

    /// <summary>
    /// 맵에 있는 각 노드의 상태를 개별적으로 저장합니다. (세이브 스컴 방지용)
    /// </summary>
    [Table("MapNodeState")]
    public class MapNodeState
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string RunId { get; set; }
        public int Act { get; set; }
        public int Floor { get; set; }
        public int NodeIndex { get; set; }        // 층 내에서의 순서 (예: 왼쪽에서 2번째).
        public NodeType Type { get; set; }
        public bool Visited { get; set; }         // 이미 방문했는지 여부.
        public bool Cleared { get; set; }         // 전투에서 승리했는지 여부.

        // 각 노드의 고유한 내용을 JSON 텍스트로 저장하여, 재접속 시 결과가 바뀌는 것을 방지합니다.
        public string ShopInventoryJson { get; set; }   // 상점의 상품 목록.
        public string EventResolutionJson { get; set; } // 이벤트에서 내린 선택과 그 결과.
        public string RewardsJson { get; set; }         // 전투 후 제시된 보상 목록.
    }

    /// <summary>
    /// 모든 무작위 결과의 '운명'을 미리 저장하여 세이브 스컴을 방지합니다.
    /// </summary>
    [Table("RngState")]
    public class RngState
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        [Indexed] public string RunId { get; set; }
        [Indexed] public string Domain { get; set; } // 어떤 종류의 랜덤인지 구분 (예: "map", "reward", "shop").
        public ulong Seed { get; set; }              // 이 랜덤의 시작 시드값.
        public ulong StateA { get; set; }            // 랜덤 생성기의 현재 상태값 A.
        public ulong StateB { get; set; }            // 랜덤 생성기의 현재 상태값 B.
        public long Step { get; set; }               // 몇 번째 랜덤 숫자를 생성할 차례인지.
    }

}
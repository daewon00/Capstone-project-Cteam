// 요구: Unity Test Framework (NUnit), DatabaseManager / SaveData / DeckManager / CardInstance

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Game.Save;

public class DbCoreMoreTests
{
    // 1) 모든 엔터티(카드/유물/포션/노드/RNG) 저장→로드 검증
    [Test]
    public void SaveLoad_AllEntities_Roundtrip()
    {
        DatabaseManager.Instance.Connect();

        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId = "P1",
            Act = 1, Floor = 1, NodeIndex = 0,
            Gold = 123, CurrentHp = 40, MaxHpBase = 50,
            MaxHpFromPerks = 2, MaxHpFromRelics = 3,
            EnergyMax = 3, Keys = 1,
            CreatedAtUtc = DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion = "42", AppVersion = Application.version
        };

        var cards = new List<CardInDeck> {
            new CardInDeck { InstanceId = $"{runId}-00000001", RunId = runId, CardId = "CARD_STRIKE", IsUpgraded = false },
            new CardInDeck { InstanceId = $"{runId}-00000002", RunId = runId, CardId = "CARD_DEFEND",  IsUpgraded = true  },
        };

        var relics = new List<RelicInPossession> {
            new RelicInPossession { RunId = runId, RelicId = "RELIC_PEN", Stacks=1, Cooldown=0, UsesLeft=-1, StateJson=null }
        };

        var pots = new List<PotionInPossession> {
            new PotionInPossession { RunId = runId, PotionId = "POTION_HEAL", Charges=1 }
        };

        var nodes = new List<MapNodeState> {
            new MapNodeState {
                RunId=runId, Act=1, Floor=1, NodeIndex=0,
                Type=Game.Save.NodeType.Event, Visited=true, Cleared=false,
                EventResolutionJson="{}", ShopInventoryJson=null, RewardsJson=null
            }
        };

        var rngs = new List<RngState> {
            new RngState { RunId=runId, Domain="reward", Seed=1234, StateA=1, StateB=2, Step=3 },
            new RngState { RunId=runId, Domain="shop",   Seed=5678, StateA=4, StateB=5, Step=6 }
        };

        DatabaseManager.Instance.SaveCurrentRun(run, cards, relics, pots, nodes, rngs);

        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
        Assert.NotNull(loaded);
        Assert.AreEqual(2, loaded.Cards.Count);
        Assert.AreEqual(1, loaded.Relics.Count);
        Assert.AreEqual(1, loaded.Potions.Count);
        Assert.AreEqual(1, loaded.Nodes.Count);
        Assert.AreEqual(2, loaded.RngStates.Count);
        Assert.AreEqual(123, loaded.Run.Gold);
        Assert.AreEqual("CARD_DEFEND", loaded.Cards[1].CardId);
        Assert.IsTrue(loaded.Cards[1].IsUpgraded);
    }

    // 2) 같은 RunId로 여러 번 저장하면 깔끔히 덮어써지는지(Upsert)
    [Test]
    public void Upsert_SameRunId_Overwrites()
    {
        DatabaseManager.Instance.Connect();

        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId = "P1", Act=1, Floor=0, NodeIndex=0,
            Gold=10, CurrentHp=20, MaxHpBase=30,
            CreatedAtUtc = DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion="42", AppVersion=Application.version
        };

        var cards = new List<CardInDeck> {
            new CardInDeck { InstanceId=$"{runId}-00000001", RunId=runId, CardId="CARD_A", IsUpgraded=false }
        };

        // 1차 저장
        DatabaseManager.Instance.SaveCurrentRun(run, cards,
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>());

        // 2차 저장(값 변경 + 카드 추가)
        run.Gold = 77;
        cards.Add(new CardInDeck { InstanceId=$"{runId}-00000002", RunId=runId, CardId="CARD_B", IsUpgraded=true });

        DatabaseManager.Instance.SaveCurrentRun(run, cards,
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>());

        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
        Assert.NotNull(loaded);
        Assert.AreEqual(77, loaded.Run.Gold);
        Assert.AreEqual(2, loaded.Cards.Count);
        Assert.IsTrue(loaded.Cards.Exists(c => c.CardId == "CARD_A"));
        Assert.IsTrue(loaded.Cards.Exists(c => c.CardId == "CARD_B" && c.IsUpgraded));
    }

    // 3) 삭제 API가 완전히 지우는지 (EndRunAndSummarize 없는 버전)
    [Test]
    public void DeleteCurrentRun_RemovesEverything()
    {
        DatabaseManager.Instance.Connect();

        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId = "P1", Act=1, Floor=2, NodeIndex=0,
            Gold=10, CurrentHp=42, MaxHpBase=50,
            CreatedAtUtc=DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc=DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion="42", AppVersion=Application.version
        };

        DatabaseManager.Instance.SaveCurrentRun(run,
            cards: new List<CardInDeck>(),
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>());

        DatabaseManager.Instance.DeleteCurrentRun(runId);

        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
        Assert.IsNull(loaded);
    }

    // 4) 백업 파일이 생성되고 내용이 있는지
    [Test]
    public void Backup_File_Exists_And_NonEmpty()
    {
        DatabaseManager.Instance.Connect();

        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId="P1", Act=1, Floor=0, NodeIndex=0,
            Gold=1, CurrentHp=1, MaxHpBase=1,
            CreatedAtUtc=DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc=DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion="42", AppVersion=Application.version
        };

        DatabaseManager.Instance.SaveCurrentRun(run,
            cards: new List<CardInDeck>(),
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>());

        var dbPath  = Path.Combine(Application.persistentDataPath, "game_save.db");
        var bakPath = dbPath + ".bak";

        Assert.IsTrue(File.Exists(bakPath), "Backup .bak should exist after save");
        var fi = new FileInfo(bakPath);
        Assert.Greater(fi.Length, 0, ".bak should be non-empty");
    }

    // 5) DeckManager: InstanceId 카운터 복원 검증
    [Test]
    public void DeckManager_InstanceId_RestoresCounter()
    {
        DatabaseManager.Instance.Connect();

        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId="P1", Act=1, Floor=0, NodeIndex=0,
            Gold=0, CurrentHp=1, MaxHpBase=1,
            CreatedAtUtc=DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc=DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion="42", AppVersion=Application.version
        };

        var cards = new List<CardInDeck> {
            new CardInDeck { InstanceId=$"{runId}-00000001", RunId=runId, CardId="CARD_A", IsUpgraded=false },
            new CardInDeck { InstanceId=$"{runId}-0000000A", RunId=runId, CardId="CARD_B", IsUpgraded=false }, // HEX 10
        };

        DatabaseManager.Instance.SaveCurrentRun(run, cards,
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>());

        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
        Assert.NotNull(loaded);

        var go = new GameObject("DeckManagerTest");
        try
        {
            var deck = go.AddComponent<DeckManager>();
            deck.InitForRun(runId, loaded.Cards);

            var newCard = deck.CreateNewCardInstance("CARD_NEW", false);
            var parts = newCard.InstanceId.Split('-');
            var last = parts[parts.Length - 1];
            Assert.IsTrue(long.TryParse(last, System.Globalization.NumberStyles.HexNumber, null, out var parsed));
            Assert.GreaterOrEqual(parsed, 0x0000000B); // 10 다음인 11 이상이어야 함
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
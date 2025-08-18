using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Save;

public class DbCoreTests
{
    [Test]
    public void SaveLoad_Run_Roundtrip_Minimal()
    {
        // 1) DB 연결 (현재 DatabaseManager에 맞춰 인자 없이)
        DatabaseManager.Instance.Connect();

        // 2) 새 런 데이터 구성
        var runId = Guid.NewGuid().ToString("N");
        var run = new CurrentRun {
            RunId = runId, ProfileId = "P1",
            Act = 1, Floor = 0, NodeIndex = 0,
            Gold = 99, CurrentHp = 70, MaxHpBase = 80,
            MaxHpFromPerks = 2, MaxHpFromRelics = 3,
            EnergyMax = 3, Keys = 0,
            CreatedAtUtc = DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            ContentCatalogVersion = "42",
            AppVersion = Application.version
        };

        var cards = new List<CardInDeck> {
            new CardInDeck { InstanceId = $"{runId}-00000001", RunId = runId, CardId = "CARD_STRIKE", IsUpgraded = false },
            new CardInDeck { InstanceId = $"{runId}-00000002", RunId = runId, CardId = "CARD_DEFEND", IsUpgraded = true  },
        };

        // 3) 저장
        DatabaseManager.Instance.SaveCurrentRun(
            run,
            cards,
            relics: new List<RelicInPossession>(),
            potions: new List<PotionInPossession>(),
            nodes: new List<MapNodeState>(),
            rngStates: new List<RngState>()
        );

        // 4) 로드
        var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);

        // 5) 검증
        Assert.NotNull(loaded, "Loaded run should not be null");
        Assert.AreEqual(2, loaded.Cards.Count, "Card count mismatch");
        Assert.AreEqual(99, loaded.Run.Gold, "Gold mismatch");
        Assert.AreEqual("CARD_DEFEND", loaded.Cards[1].CardId, "Second card should be DEFEND");
        Assert.IsTrue(loaded.Cards[1].IsUpgraded, "DEFEND should be upgraded");
    }
}
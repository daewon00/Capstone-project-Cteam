using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Shared harness for PlayMode tests. Seeds a lightweight run, bootstraps the
/// usual game services via <see cref="GameInitializer"/>, and exposes helpers
/// to drive turns or manipulate the player hand deterministically.
/// </summary>
public abstract class PlayModeTestBase
{
    protected IDeckService DeckService { get; private set; }
    protected BattleController BattleController { get; private set; }
    protected RelicSystem RelicSystem { get; private set; }
    protected IWalletService WalletService { get; private set; }
    protected string ActiveRunId { get; private set; }

    protected virtual int DefaultDeckSize => 12;
    protected virtual string BattleSceneName => "Battle_android";
    private GameInitializer _initializer;

    [UnitySetUp]
    public virtual IEnumerator BaseUnitySetUp()
    {
        SeedRunIfNeeded();
        EnsureTestGameContext();
        EnsureTestAudioManager();
        _initializer = new GameObject("TestGameInitializer").AddComponent<GameInitializer>();
        yield return null; // allow Awake/Start

        ConfigureRunScopedServices();

        SceneManager.LoadScene(BattleSceneName);
        yield return null;

        DeckService = ServiceRegistry.GetRequired<IDeckService>();
        BattleController = UnityEngine.Object.FindObjectOfType<BattleController>();
        RelicSystem = UnityEngine.Object.FindObjectOfType<RelicSystem>();
        WalletService = ServiceRegistry.Get<IWalletService>();

        Assert.IsNotNull(DeckService, "IDeckService not found.");
        Assert.IsNotNull(BattleController, "BattleController not found.");
        Assert.IsNotNull(RelicSystem, "RelicSystem not found in scene.");
        Assert.IsNotNull(WalletService, "IWalletService not registered.");

        if (!string.IsNullOrEmpty(ActiveRunId))
        {
            DeckService.LoadAndPrepareDeck(ActiveRunId);
        }

        BattleController.currentPhase = BattleController.TurnOrder.playerActive;
        yield return null; // let first-frame draws/UI settle
    }

    [TearDown]
    public virtual void BaseTearDown()
    {
        PlayerPrefs.DeleteKey("lastRunId");
        ActiveRunId = null;
        if (_initializer != null)
            UnityEngine.Object.DestroyImmediate(_initializer.gameObject);
    }

    protected List<string> BuildTestDeck(int cardCount)
    {
        var allCards = Resources.LoadAll<CardScriptableObject>("Cards");
        Assert.IsNotEmpty(allCards, "Resources/Cards is empty.");
        var validCardIds = allCards.Select(c => c.CardId).Where(id => !string.IsNullOrEmpty(id)).ToList();
        Assert.IsNotEmpty(validCardIds, "No CardScriptableObject has a valid CardId.");

        var deck = new List<string>(cardCount);
        for (int i = 0; i < cardCount; i++)
            deck.Add(validCardIds[i % validCardIds.Count]);
        return deck;
    }

    protected string PrepareRunAndDatabase(List<string> cardIds, int startingHp = 50, int maxHp = 50, int startingGold = 100, int startingEnergy = 3)
    {
        if (cardIds == null || cardIds.Count == 0)
            Assert.Fail("cardIds must not be empty.");

        var runId = Guid.NewGuid().ToString("N");
        DatabaseManager.Instance.Connect();

        var cardsInDeck = new List<CardInDeck>();
        foreach (var cardId in cardIds)
        {
            cardsInDeck.Add(new CardInDeck
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                RunId = runId,
                CardId = cardId,
                IsUpgraded = false
            });
        }

        var run = new CurrentRun
        {
            RunId = runId,
            ProfileId = "TestProfile",
            Act = 1,
            Floor = 0,
            NodeIndex = 0,
            Gold = startingGold,
            CurrentHp = startingHp,
            MaxHpBase = maxHp,
            EnergyMax = startingEnergy,
            CreatedAtUtc = DateTime.UtcNow.ToString("o"),
            UpdatedAtUtc = DateTime.UtcNow.ToString("o")
        };

        var db = new DatabaseFacade();
        db.UpsertCurrentRun(run);
        db.ReplaceCardsInDeck(runId, cardsInDeck);

        PlayerPrefs.SetString("lastRunId", runId);
        PlayerPrefs.Save();
        return runId;
    }

    protected IEnumerator RunPlayerTurns(int turnCount, bool includeEnemyTurns = false)
    {
        for (int i = 0; i < turnCount; i++)
        {
            GameEvents.RaiseTurnStart(true);
            yield return null;
            GameEvents.RaiseTurnEnd(true);
            yield return null;

            if (includeEnemyTurns)
            {
                GameEvents.RaiseTurnStart(false);
                yield return null;
                GameEvents.RaiseTurnEnd(false);
                yield return null;
            }
        }
    }

    protected bool GrantRelic(string relicId, int stacks = 1)
    {
        Assert.IsNotNull(RelicSystem, "RelicSystem is not available.");
        bool granted = RelicSystem.AddRelicById(relicId, stacks, save: false);
        Assert.IsTrue(granted, $"Failed to grant relic '{relicId}'.");
        return granted;
    }

    protected Card EnsureSinglePlayerCardInHand()
    {
        var hand = HandController.instance;
        Assert.IsNotNull(hand, "HandController.instance is null.");
        if (hand.heldCards == null)
            hand.heldCards = new List<Card>();

        var primary = hand.heldCards.FirstOrDefault(c => c != null && c.isPlayer);
        Assert.IsNotNull(primary, "No player-owned card found in hand.");

        for (int i = hand.heldCards.Count - 1; i >= 0; i--)
        {
            var card = hand.heldCards[i];
            if (card == null || card == primary)
                continue;
            hand.heldCards.RemoveAt(i);
        }

        hand.SetCardPositionsInHand();
        return primary;
    }

    protected int GetConfiguredInitialHandCount()
    {
        if (BattleController == null)
            return 0;

        var field = typeof(BattleController).GetField("_initialHandCount", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            return 0;

        if (field.GetValue(BattleController) is int value)
            return value;

        return 0;
    }

    private void ConfigureRunScopedServices()
    {
        if (string.IsNullOrEmpty(ActiveRunId))
            return;

        ServiceRegistry.Get<IDeckService>()?.LoadAndPrepareDeck(ActiveRunId);
        ServiceRegistry.Get<IWalletService>()?.RebindRun(ActiveRunId);
        ServiceRegistry.Get<IRunService>()?.RebindRun(ActiveRunId);
        ServiceRegistry.Get<IRunStageService>()?.RebindRun(ActiveRunId);
    }

    private void SeedRunIfNeeded()
    {
        if (!string.IsNullOrEmpty(ActiveRunId))
            return;

        ActiveRunId = PrepareRunAndDatabase(BuildTestDeck(DefaultDeckSize));
    }

    private void EnsureTestGameContext()
    {
        if (GameContext.I != null)
            return;

        var go = new GameObject("TestGameContext");
        var context = go.AddComponent<GameContext>();
        context.ProfileId = "TestProfile";
    }

    private void EnsureTestAudioManager()
    {
        if (AudioManager.instance != null)
            return;

        var go = new GameObject("TestAudioManager");
        var manager = go.AddComponent<AudioManager>();

        manager.menuMusic = go.AddComponent<AudioSource>();
        manager.battleSelectMusic = go.AddComponent<AudioSource>();
        manager.MapMusic = go.AddComponent<AudioSource>();

        manager.bgm = new AudioSource[1];
        manager.bgm[0] = go.AddComponent<AudioSource>();

        var sfxList = new List<AudioSource>();
        const int sfxCount = 8;
        for (int i = 0; i < sfxCount; i++)
        {
            sfxList.Add(go.AddComponent<AudioSource>());
        }
        manager.sfx = sfxList.ToArray();
    }
}

/// <summary>Custom yield instruction with a timeout to avoid hanging UnityTests.</summary>
public sealed class WaitUntilWithTimeout : CustomYieldInstruction
{
    private readonly Func<bool> _predicate;
    private readonly float _timeout;
    private readonly float _start;

    public override bool keepWaiting
    {
        get
        {
            if (Time.time - _start > _timeout)
            {
                Assert.Fail("Test timed out.");
                return false;
            }
            return !_predicate();
        }
    }

    public WaitUntilWithTimeout(Func<bool> predicate, float timeoutSeconds)
    {
        _predicate = predicate;
        _timeout = timeoutSeconds;
        _start = Time.time;
    }
}

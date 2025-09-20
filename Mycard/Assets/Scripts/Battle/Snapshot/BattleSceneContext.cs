using System.Collections.Generic;
using BattleSnapshot;
using Game.Save;
using UnityEngine;

public class BattleSceneContext
{
    public BattleController Battle { get; }
    public HandController Hand { get; }
    public CardPointsController Board { get; }
    public EnemyController Enemy { get; }
    public IDeckService DeckService { get; }
    public ICardCatalog CardCatalog { get; }
    public Card CardPrefab { get; }
    public IRngService RngService { get; }
    private readonly EffectIconDatabase _iconDatabase;
    private readonly ICardEffectService _effectService;
    public HandServiceBinder HandBinder { get; }

    public BattleSceneContext(BattleController battle, HandController hand, CardPointsController board, EnemyController enemy,
        IDeckService deckService, ICardCatalog catalog, Card cardPrefab, IRngService rng, EffectIconDatabase iconDatabase = null)
    {
        Battle = battle;
        Hand = hand;
        Board = board;
        Enemy = enemy;
        DeckService = deckService;
        CardCatalog = catalog;
        CardPrefab = cardPrefab;
        RngService = rng;
        HandBinder = hand != null ? hand.GetComponent<HandServiceBinder>() : null;
        _iconDatabase = iconDatabase != null ? iconDatabase : ServiceRegistry.Get<EffectIconDatabase>();
        if (_iconDatabase == null)
        {
            _iconDatabase = Resources.Load<EffectIconDatabase>("Cards/EffectIconDatabase");
        }
        _effectService = ServiceRegistry.Get<ICardEffectService>();
    }

    public void ClearHand()
    {
        Debug.Log($"[BattleSceneContext] ClearHand (before) count={(Hand != null ? Hand.heldCards.Count : -1)}");
        if (Hand == null) return;
        foreach (var card in Hand.heldCards)
        {
            if (card != null)
                Object.Destroy(card.gameObject);
        }
        Hand.heldCards.Clear();
        Hand.SetCardPositionsInHand();
        HandBinder?.ResetViewCache();
        Debug.Log("[BattleSceneContext] ClearHand completed");
    }

    public void SpawnCardInHand(CardRuntimeState state)
    {
        if (Hand == null || state == null || CardPrefab == null || CardCatalog == null || DeckService == null) return;
        var so = CardCatalog.GetCardData(state.CardId);
        if (so == null)
        {
            Debug.LogWarning($"[BattleSceneContext] Missing SO for hand card {state.CardId}");
            return;
        }
        var card = Object.Instantiate(CardPrefab, Hand.transform.position, CardPrefab.transform.rotation);
        card.gameObject.SetActive(true);
        card.Initialize(state.InstanceId, so, DeckService, _iconDatabase);
        card.isPlayer = true;
        card.inHand = true;
        card.transform.SetParent(Hand.transform, true);
        Hand.heldCards.Add(card);
        Hand.SetCardPositionsInHand();
        HandBinder?.RegisterExistingCard(card);
        BattleDeckRuntimeSync.UpdateCardState(card);
        Debug.Log($"[BattleSceneContext] SpawnCardInHand -> heldCards={Hand.heldCards.Count}");
    }

    public void ClearPlayerField()
    {
        if (Board == null) return;
        foreach (var slot in Board.playerCardPoints)
        {
            if (slot == null) continue;
            if (slot.activeCard != null)
            {
                _effectService?.UnregisterBoardCard(slot.activeCard);
                Object.Destroy(slot.activeCard.gameObject);
                slot.activeCard = null;
            }
        }
    }

    public void SpawnPlayerFieldCard(PlayerBoardSlotState slotState, CardRuntimeState runtime)
    {
        if (Board == null || runtime == null || CardPrefab == null || CardCatalog == null || DeckService == null) return;
        if (slotState == null) return;

        int slotIndex = slotState.slotIndex;
        if (slotIndex < 0 || slotIndex >= Board.playerCardPoints.Length)
        {
            Debug.LogWarning($"[BattleSceneContext] Invalid slot index {slotIndex}");
            return;
        }
        var slot = Board.playerCardPoints[slotIndex];
        if (slot == null)
        {
            Debug.LogWarning($"[BattleSceneContext] Slot {slotIndex} missing");
            return;
        }
        if (slot.activeCard != null)
        {
            Object.Destroy(slot.activeCard.gameObject);
            slot.activeCard = null;
        }

        var so = CardCatalog.GetCardData(runtime.CardId);
        if (so == null)
        {
            Debug.LogWarning($"[BattleSceneContext] Missing SO for field card {runtime.CardId}");
            return;
        }
        var card = Object.Instantiate(CardPrefab);
        card.gameObject.SetActive(true);
        card.Initialize(runtime.InstanceId, so, DeckService, _iconDatabase);
        card.isPlayer = true;
        card.inHand = false;
        card.assignedPlace = slot;
        card.SetInteractable(false);
        card.transform.SetParent(slot.transform, true);
        card.transform.position = slot.transform.position;

        var modifiers = BattleDeckRuntimeSync.ParseModifiers(runtime.ModifiersJson);
        CardEffectRuntimeSnapshot effectSnapshot = modifiers?.effectState;
        var boardRotation = HandController.instance != null
            ? HandController.instance.minpos.rotation
            : CardPrefab.transform.rotation;
        if (modifiers != null)
        {
            boardRotation = Quaternion.Euler(modifiers.rotX, modifiers.rotY, modifiers.rotZ);
        }
        else if (slotState != null)
        {
            boardRotation = Quaternion.Euler(slotState.rotX, slotState.rotY, slotState.rotZ);
        }
        card.transform.rotation = boardRotation;
        card.MoveToPoint(slot.transform.position, boardRotation);
        if (HandController.instance != null)
            card.SetCardScale(HandController.instance.GetBoardScale());
        slot.activeCard = card;

        if (modifiers != null)
        {
            card.currentHealth = modifiers.currentHp;
            card.attackPower = modifiers.attack;
            card.UpdateCardDisplay();
        }

        _effectService?.RegisterBoardCard(card, true, effectSnapshot);
        BattleDeckRuntimeSync.UpdateCardState(card);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleSceneContext] SpawnPlayerFieldCard index={slotIndex} pos={card.transform.position} rot={card.transform.rotation.eulerAngles}");
#endif
    }
}

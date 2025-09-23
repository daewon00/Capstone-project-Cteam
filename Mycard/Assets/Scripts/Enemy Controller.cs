using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;
using BattleSnapshot;

public class EnemyController : MonoBehaviour
{   //카드를 배치하는 알고리즘이 들어가 있음
    public static EnemyController instance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        instance = this;
    }

    public List<CardScriptableObject> deckToUse = new List<CardScriptableObject>();
    private List<CardScriptableObject> activeCards = new List<CardScriptableObject>();

    public Card cardToSpawn;
    public Transform cardSpawnPoint;

    public enum AITpye { placeFromDeck, handRandomPlace, handDefensive, handAttacking }
    public AITpye enemyAIType;

    private List<CardScriptableObject> cardsInHand = new List<CardScriptableObject>();
    private readonly List<Card> stagedCards = new List<Card>();

    public IReadOnlyList<CardScriptableObject> ActiveDeck => activeCards;
    public IReadOnlyList<CardScriptableObject> CurrentHand => cardsInHand;
    public IReadOnlyList<Card> StagedCards => stagedCards;
    public int startHandSize;
    void Start()
    {
        SetupDeck();

        if (enemyAIType != AITpye.placeFromDeck)
        {
            SetupHand();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetupDeck()
    {
        activeCards.Clear();

        List<CardScriptableObject> tempDeck = new List<CardScriptableObject>();
        tempDeck.AddRange(deckToUse);
        int interations = 0;
        while (tempDeck.Count > 0 && interations < 500)
        {
            int selected = UnityEngine.Random.Range(0, tempDeck.Count);
            activeCards.Add(tempDeck[selected]);
            tempDeck.RemoveAt(selected); //선택되지 않은 activecard 값을 줄여준다.
            interations++;
        }
    }

    public void StartAction()
    {
        StartCoroutine(EnemyActionCo());
    }

    IEnumerator EnemyActionCo()
    {
        if (activeCards.Count == 0)
        {
            SetupDeck();
        }

        yield return new WaitForSeconds(.5f);
        
        for (int i = 0; i < CardPointsController.instance.enemyStayPoints.Length; i++)
        {
            if (CardPointsController.instance.enemyStayPoints[i].activeCard != null)
            {
                if (CardPointsController.instance.enemyCardPoints[i].activeCard == null)
                {
                    var Ecard = CardPointsController.instance.enemyStayPoints[i].activeCard;
                    Ecard.MoveToPoint(CardPointsController.instance.enemyCardPoints[i].transform.position, CardPointsController.instance.enemyCardPoints[i].transform.rotation);
                    if (HandController.instance != null)
                        Ecard.SetCardScale(HandController.instance.GetBoardScale());


                    CardPointsController.instance.enemyCardPoints[i].activeCard = Ecard;
                    Ecard.assignedPlace = CardPointsController.instance.enemyCardPoints[i];

                    Ecard.isPlayer = false; // 안전하게 적임을 명시
                    GameEvents.RaiseCardPlayed(Ecard);
                    Ecard.SetInteractable(false); // 적 카드 상호작용 비활성화
                    BattleDeckRuntimeSync.UpdateCardState(Ecard);

                    var effectService = ServiceRegistry.Get<ICardEffectService>();
                    effectService?.RegisterBoardCard(Ecard, false);


                    CardPointsController.instance.enemyStayPoints[i].activeCard = null;

                }
            }
        }
        if (enemyAIType != AITpye.placeFromDeck )
        {
            for(int i = 0; i< BattleController.instance.cardToDrawPerTurn; i++) 
            {
                cardsInHand.Add(activeCards[0]);
                activeCards.RemoveAt(0);

                if(activeCards.Count == 0)
                {
                    SetupDeck();
                }
            }
        }


        List<CardPlacePoint> cardPoints = new List<CardPlacePoint>();
        cardPoints.AddRange(CardPointsController.instance.enemyStayPoints);

        int randomPoint = UnityEngine.Random.Range(0, cardPoints.Count);
        CardPlacePoint selectedPoint = cardPoints[randomPoint];

        if (enemyAIType == AITpye.placeFromDeck || enemyAIType == AITpye.handRandomPlace)
        {
            cardPoints.Remove(selectedPoint);

            while (selectedPoint.activeCard != null && cardPoints.Count > 0) //카드를 랜덤 포인트에 배치 카드가 있다면
            {
                randomPoint = UnityEngine.Random.Range(0, cardPoints.Count);
                selectedPoint = cardPoints[randomPoint];
                cardPoints.RemoveAt(randomPoint);
            }
        }

        CardScriptableObject selectedCard = null;
        int iterations = 0;
        List<CardPlacePoint> preferradPoints = new List<CardPlacePoint>(); 
        List<CardPlacePoint> secondaryPoints = new List<CardPlacePoint>();


        switch (enemyAIType)
        {
            case AITpye.placeFromDeck:



            if (selectedPoint.activeCard == null)
            {
                Card newCard = Instantiate(cardToSpawn, cardSpawnPoint.position, cardSpawnPoint.rotation);
                newCard.cardSO = activeCards[0];
                activeCards.RemoveAt(0);
                newCard.SetupCard();
                newCard.SetBattleInstanceId(Guid.NewGuid().ToString("N"));
                newCard.MoveToPoint(selectedPoint.transform.position, selectedPoint.transform.rotation);
        if (HandController.instance != null)
            newCard.SetCardScale(HandController.instance.GetBoardScale());

                selectedPoint.activeCard = newCard;
                newCard.assignedPlace = selectedPoint;
                newCard.isPlayer = false;
                newCard.SetInteractable(false);
                BattleDeckRuntimeSync.UpdateCardState(newCard);

                

            }

            break;

            case AITpye.handRandomPlace:

                selectedCard = SelectedCardToPlay();

                iterations = 50;
                while(selectedCard != null && iterations > 0 && selectedPoint.activeCard == null)
                {
                    PlayCard(selectedCard, selectedPoint);

                    selectedCard = SelectedCardToPlay();

                    iterations--;

                    yield return new WaitForSeconds(CardPointsController.instance.timeBetweenAttacks);

                    while (selectedPoint.activeCard != null && cardPoints.Count > 0)
                    {
                        randomPoint = UnityEngine.Random.Range(0, cardPoints.Count);
                        selectedPoint = cardPoints[randomPoint];
                        cardPoints.RemoveAt(randomPoint);
                    }



                }
                break;

            case AITpye.handDefensive:

                selectedCard = SelectedCardToPlay();

                preferradPoints.Clear();
                secondaryPoints.Clear();

                for(int i = 0; i < cardPoints.Count; i++)
                {
                    if (cardPoints[i].activeCard == null)
                    {
                        if (CardPointsController.instance.playerCardPoints[i].activeCard != null)
                        {
                            preferradPoints.Add(cardPoints[i]);

                        }
                        else
                        {
                            secondaryPoints.Add(cardPoints[i]);
                        }
                    }
                }

                
                
                iterations = 50;
                while(selectedCard != null && iterations > 0 && preferradPoints.Count + secondaryPoints.Count > 0)
                {
                    if(preferradPoints.Count > 0)
                    {
                        int selectPoint = UnityEngine.Random.Range(0, preferradPoints.Count);
                        selectedPoint = preferradPoints[selectPoint];

                        preferradPoints.RemoveAt(selectPoint);
                    }
                    else
                    {
                        int selectPoint = UnityEngine.Random.Range(0, secondaryPoints.Count);
                        selectedPoint = secondaryPoints[selectPoint];

                        secondaryPoints.RemoveAt(selectPoint);
                    }

                    PlayCard(selectedCard,selectedPoint);

                    selectedCard = SelectedCardToPlay();

                    iterations--;

                    yield return new WaitForSeconds(CardPointsController.instance.timeBetweenAttacks);
                }
                

                break;

            case AITpye.handAttacking:

                selectedCard = SelectedCardToPlay();

                preferradPoints.Clear();
                secondaryPoints.Clear();

                for (int i = 0; i < cardPoints.Count; i++)
                {
                    if (cardPoints[i].activeCard == null)
                    {
                        if (CardPointsController.instance.playerCardPoints[i].activeCard == null)
                        {
                            preferradPoints.Add(cardPoints[i]);

                        }
                        else
                        {
                            secondaryPoints.Add(cardPoints[i]);
                        }
                    }
                }



                iterations = 50;
                while (selectedCard != null && iterations > 0 && preferradPoints.Count + secondaryPoints.Count > 0)
                {
                    if (preferradPoints.Count > 0)
                    {
                        int selectPoint = UnityEngine.Random.Range(0, preferradPoints.Count);
                        selectedPoint = preferradPoints[selectPoint];

                        preferradPoints.RemoveAt(selectPoint);
                    }
                    else
                    {
                        int selectPoint = UnityEngine.Random.Range(0, secondaryPoints.Count);
                        selectedPoint = secondaryPoints[selectPoint];

                        secondaryPoints.RemoveAt(selectPoint);
                    }

                    PlayCard(selectedCard, selectedPoint);

                    selectedCard = SelectedCardToPlay();

                    iterations--;

                    yield return new WaitForSeconds(CardPointsController.instance.timeBetweenAttacks);
                }

                break;
        }
        yield return new WaitForSeconds(.5f);

        BattleController.instance.AdvanceTurn();
    }

    void SetupHand()
    {
        for(int i = 0; i < startHandSize; i++)
        {
            if(activeCards.Count == 0)
            {
                SetupDeck();
            }

            cardsInHand.Add(activeCards[0]);
            activeCards.RemoveAt(0);
        }
    }

    public void PlayCard(CardScriptableObject cardSO, CardPlacePoint placePoint)
    {

        Card newCard = Instantiate(cardToSpawn, cardSpawnPoint.position, cardSpawnPoint.rotation);
        newCard.cardSO = cardSO;

        newCard.SetupCard();
        newCard.SetBattleInstanceId(Guid.NewGuid().ToString("N"));
        newCard.MoveToPoint(placePoint.transform.position, placePoint.transform.rotation);
        if (HandController.instance != null)
            newCard.SetCardScale(HandController.instance.GetBoardScale());

        placePoint.activeCard = newCard;
        newCard.assignedPlace = placePoint;

        // 적 카드로 명시하고 상호작용 비활성화(클릭 방지)
        newCard.isPlayer = false;
        newCard.SetInteractable(false);
        BattleDeckRuntimeSync.UpdateCardState(newCard);

        cardsInHand.Remove(cardSO);

        int effectiveCost = Mathf.Max(0, newCard.GetEffectiveManaCost());
        BattleController.instance.SpendEnemyrMana(effectiveCost);
        
        AudioManager.instance.PlaySFX(4);

        
    }

    CardScriptableObject SelectedCardToPlay()
    {
        CardScriptableObject cardToPlay= null;

        List<CardScriptableObject> cardsToPlay = new List<CardScriptableObject>();
        foreach(CardScriptableObject card in cardsInHand)
        {
            if(card.manaCost <= BattleController.instance.enemyMana)
            {
                cardsToPlay.Add(card);

            }
        }

        if(cardsToPlay.Count > 0)
        {
            int selected = UnityEngine.Random.Range(0, cardsToPlay.Count);

            cardToPlay = cardsToPlay[selected];
        }

        return cardToPlay;
    }

    public void RestoreStateFromSnapshot(EnemyCombatState enemyState, List<EnemyBoardSlotState> frontline, List<EnemyBoardSlotState> bench, BattleSceneContext context)
    {
        var catalog = context.CardCatalog;
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        activeCards.Clear();
        cardsInHand.Clear();

        if (enemyState != null)
        {
            if (enemyState.deckCardIds != null)
            {
                foreach (var id in enemyState.deckCardIds)
                {
                    var so = !string.IsNullOrEmpty(id) ? catalog?.GetCardData(id) : null;
                    if (so != null) activeCards.Add(so);
                }
            }

            if (enemyState.handCardIds != null)
            {
                foreach (var id in enemyState.handCardIds)
                {
                    var so = !string.IsNullOrEmpty(id) ? catalog?.GetCardData(id) : null;
                    if (so != null) cardsInHand.Add(so);
                }
            }
        }

        var board = CardPointsController.instance;
        if (board != null)
        {
            for (int i = 0; i < board.enemyCardPoints.Length; i++)
            {
                var slot = board.enemyCardPoints[i];
                if (slot != null && slot.activeCard != null)
                {
                    effectService?.UnregisterBoardCard(slot.activeCard);
                    Destroy(slot.activeCard.gameObject);
                    slot.activeCard = null;
                }
            }

            for (int i = 0; i < board.enemyStayPoints.Length; i++)
            {
                var slot = board.enemyStayPoints[i];
                if (slot != null && slot.activeCard != null)
                {
                    effectService?.UnregisterBoardCard(slot.activeCard);
                    Destroy(slot.activeCard.gameObject);
                    slot.activeCard = null;
                }
            }
        }

        stagedCards.Clear();

        if (frontline != null && board != null)
        {
            foreach (var slotState in frontline)
            {
                if (slotState == null) continue;
                if (slotState.slotIndex < 0 || slotState.slotIndex >= board.enemyCardPoints.Length) continue;
                var slot = board.enemyCardPoints[slotState.slotIndex];
                if (slot == null) continue;
                var so = !string.IsNullOrEmpty(slotState.cardId) ? catalog?.GetCardData(slotState.cardId) : null;
                if (so == null) continue;

                var card = Instantiate(cardToSpawn, slot.transform.position, slot.transform.rotation);
                card.cardSO = so;
                card.SetupCard();
                card.SetBattleInstanceId(!string.IsNullOrEmpty(slotState.instanceId) ? slotState.instanceId : Guid.NewGuid().ToString("N"));
                card.currentHealth = slotState.currentHp;
                card.attackPower = slotState.attack;
                card.UpdateCardDisplay();
                card.isPlayer = false;
                card.SetInteractable(false);
                card.assignedPlace = slot;
                slot.activeCard = card;
                effectService?.RegisterBoardCard(card, false, slotState.effectState);
            }
        }

        if (bench != null && board != null)
        {
            foreach (var slotState in bench)
            {
                if (slotState == null) continue;
                if (slotState.slotIndex < 0 || slotState.slotIndex >= board.enemyStayPoints.Length) continue;
                var slot = board.enemyStayPoints[slotState.slotIndex];
                if (slot == null) continue;
                var so = !string.IsNullOrEmpty(slotState.cardId) ? catalog?.GetCardData(slotState.cardId) : null;
                if (so == null) continue;

                var card = Instantiate(cardToSpawn, slot.transform.position, slot.transform.rotation);
                card.cardSO = so;
                card.SetupCard();
                card.SetBattleInstanceId(!string.IsNullOrEmpty(slotState.instanceId) ? slotState.instanceId : Guid.NewGuid().ToString("N"));
                card.currentHealth = slotState.currentHp;
                card.attackPower = slotState.attack;
                card.UpdateCardDisplay();
                card.isPlayer = false;
                card.SetInteractable(false);
                card.assignedPlace = slot;
                slot.activeCard = card;
                stagedCards.Add(card);
            }
        }
    }
}

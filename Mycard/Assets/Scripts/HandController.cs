using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandController : MonoBehaviour
{
    public static HandController instance;

    private void Awake()
    {
        instance = this;
    }

    public List<Card> heldCards = new List<Card>();

    public Transform minpos, maxpos;
    public List<Vector3> cardPositions = new List<Vector3>();
    
    // 드래그 중 레이아웃에서 일시 제외할 카드 목록
    private readonly HashSet<Card> _layoutLocked = new HashSet<Card>();

    void Start()
    {
        SetCardPositionsInHand();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void SetCardPositionsInHand()
    {
        cardPositions.Clear();

        Vector3 distanceBetweenPoints = Vector3.zero;
        if(heldCards.Count > 1)
        {
            distanceBetweenPoints = (maxpos.position - minpos.position) / (heldCards.Count - 1);
        }

        for(int i = 0; i < heldCards.Count; i++)
        {
            cardPositions.Add(minpos.position + (distanceBetweenPoints * i));

            var card = heldCards[i];
            if (card == null) continue;

            // 잠긴 카드는 레이아웃에서 제외(위치/인덱스 변경 안 함)
            if (_layoutLocked.Contains(card))
                continue;

            //카드가 움직이면 사용됩니다
            card.MoveToPoint(cardPositions[i], minpos.rotation);

            card.inHand = true;
            card.handPosition = i;

        }
    }

    // 드래그 시작 시 해당 카드를 레이아웃에서 제외
    public void SuspendLayoutFor(Card card)
    {
        if (card == null) return;
        _layoutLocked.Add(card);
    }

    // 드래그 종료/취소 시 레이아웃 제외 해제
    public void ResumeLayoutFor(Card card)
    {
        if (card == null) return;
        _layoutLocked.Remove(card);
    }

    public bool IsLayoutLocked(Card card)
    {
        return card != null && _layoutLocked.Contains(card);
    }

    public void RemoveCardFromHand(Card cardToRemove)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int before = heldCards.Count;
        string rmCardName = cardToRemove != null ? cardToRemove.name : "<null>";
        string rmInstanceId = cardToRemove != null ? cardToRemove.InstanceId : "<null>";
        Debug.Log($"[HandController] RemoveCardFromHand begin: target={rmCardName}, instance={rmInstanceId}, beforeCount={before}");
#endif
        // 우선 위치 인덱스 기반 제거 시도
        if (cardToRemove != null && cardToRemove.handPosition >= 0 && cardToRemove.handPosition < heldCards.Count && heldCards[cardToRemove.handPosition] == cardToRemove)
        {
            heldCards.RemoveAt(cardToRemove.handPosition);
        }
        else
        {
            // 인덱스가 불일치할 수 있으므로 안전하게 객체 기반 제거를 시도한다.
            int idx = heldCards.IndexOf(cardToRemove);
            if (idx >= 0)
            {
                heldCards.RemoveAt(idx);
            }
            else
            {
                Debug.LogError("Card at position" + (cardToRemove!=null?cardToRemove.handPosition:-1) + " is not the card being removed from hand");
            }
        }

        SetCardPositionsInHand();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int after = heldCards.Count;
        Debug.Log($"[HandController] RemoveCardFromHand end: afterCount={after}");
#endif
    }

    public void AddCardToHand(Card cardToAdd)
    {
        heldCards.Add(cardToAdd);
        SetCardPositionsInHand();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string addCardName = cardToAdd != null ? cardToAdd.name : "<null>";
        string addInstanceId = cardToAdd != null ? cardToAdd.InstanceId : "<null>";
        Debug.Log($"[HandController] AddCardToHand: added={addCardName}, instance={addInstanceId}, count={heldCards.Count}");
#endif
    }

    public void EmptyHand()
    {
        foreach(Card heldCard in heldCards)
        {
            heldCard.inHand = false;
            heldCard.MoveToPoint(BattleController.instance.discardPoint.position, heldCard.transform.rotation);

        }
        heldCards.Clear();
    }
}

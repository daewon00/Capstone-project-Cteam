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

    private bool _layoutPending;

    public Transform minpos, maxpos;
    public List<Vector3> cardPositions = new List<Vector3>();
    [Header("Visual")]
    [SerializeField] private Vector3 handScale = Vector3.one;
    [SerializeField] private Vector3 boardScale = Vector3.one;
    [Header("Press - Inspect")]
    [SerializeField, Tooltip("손패에서 눌러 카드 내용을 볼 때 사용할 스케일")] private Vector3 pressInspectScale = new Vector3(0.9f, 0.9f, 0.9f);
    [SerializeField, Tooltip("손패 확인용 확대 시 카드를 카메라 방향으로 당길 거리")] private float pressInspectForwardOffset = 0.3f;
    [Header("Press - Drag")]
    [SerializeField, Tooltip("드래그(배치 준비) 상태에서 사용할 스케일")] private Vector3 pressDragScale = Vector3.one;
    [SerializeField, Tooltip("드래그 중 카드가 카메라 쪽으로 이동할 추가 거리")] private float pressDragForwardOffset = 0.6f;
    [SerializeField, Tooltip("카메라 전환을 시작할 최소 드래그 거리(스크린 픽셀)")] private float dragCameraActivationDistance = 40f;
[Header("Sorting")]
    [Tooltip("손패 카드 정렬의 기준 오더. 오른쪽 카드로 갈수록 +index가 더해집니다.")]
    [SerializeField] private int baseSortingOrder = 1000;
    [Tooltip("드래그 중 최상위로 올릴 때 사용할 오더 값.")]
    [SerializeField] private int dragTopSortingOrder = 20000;
    
    // 드래그 중 레이아웃에서 일시 제외할 카드 목록
    private readonly HashSet<Card> _layoutLocked = new HashSet<Card>();
    // 프레스/드래그 동안 전체 레이아웃을 멈추기 위한 단일 잠금 카드
    private Card _lockedCard = null;

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
        // 레이아웃 잠김 상태에서는 자동 정렬을 수행하지 않습니다.
        if (_lockedCard != null)
        {
            _layoutPending = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[HandController] Layout is globally locked; skipping SetCardPositionsInHand()");
#endif
            return;
        }
        _layoutPending = false;
        cardPositions.Clear();

        int count = heldCards.Count;
        for (int i = 0; i < count; i++)
        {
            float t = 0.5f;
            if (count > 1)
            {
                t = (i + 0.5f) / count;
            }
            Vector3 pos = Vector3.Lerp(minpos.position, maxpos.position, t);
            cardPositions.Add(pos);

            var card = heldCards[i];
            if (card == null) continue;

            // 카드 단위 정렬 오더 적용(오른쪽 카드가 항상 위로)
            var binder = card.GetComponentInChildren<CardSortingBinder>(true);
            if (binder != null)
            {
                binder.ApplyOrder(baseSortingOrder + i);
            }

            // 잠긴 카드는 레이아웃에서 제외(위치/인덱스 변경 안 함)
            if (_layoutLocked.Contains(card))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[HandController] Layout locked card skipped: {card.GetBattleInstanceId()} index={i}");
#endif
                continue;
            }

            //카드가 움직이면 사용됩니다
            card.MoveToPoint(cardPositions[i], minpos.rotation);
            card.SetCardScale(handScale);

            card.inHand = true;
            card.handPosition = i;

            card.UpdateCardDisplay();
        }
        foreach (var card in heldCards)
        {
            if (card == null) continue;
            if (_layoutLocked.Contains(card))
                card.UpdateCardDisplay();
        }
        
    }

    // 드래그 시작 시 해당 카드를 레이아웃에서 제외
    public void SuspendLayoutFor(Card card)
    {
        if (card == null) return;
        _layoutLocked.Add(card);
        _lockedCard = card;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandController] SuspendLayoutFor {card.GetBattleInstanceId()} lockCount={_layoutLocked.Count}");
#endif
    }

    // 드래그 종료/취소 시 레이아웃 제외 해제
    public void ResumeLayoutFor(Card card)
    {
        if (card == null) return;
        _layoutLocked.Remove(card);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandController] ResumeLayoutFor {card.GetBattleInstanceId()} lockCount={_layoutLocked.Count}");
#endif
        if (_lockedCard == card)
        {
            _lockedCard = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[HandController] ResumeLayoutFor cleared global lock");
#endif
        }
        if (_lockedCard == null && _layoutPending)
        {
            SetCardPositionsInHand();
        }
    }

    /// <summary>
    /// 프레스/드래그로 인해 중단한 전체 레이아웃을 재개합니다.
    /// </summary>
    public void ResumeLayout()
    {
        if (_lockedCard != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[HandController] ResumeLayout global; was locked by={_lockedCard.GetBattleInstanceId()}");
#endif
            _layoutLocked.Remove(_lockedCard);
            _lockedCard = null;
        }
        SetCardPositionsInHand();
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
        if (_lockedCard != null)
        {
            int index = Mathf.Max(0, heldCards.Count - 1);
            cardToAdd.inHand = true;
            cardToAdd.handPosition = index;
            cardToAdd.SetCardScale(handScale);
        }
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
            heldCard.SetCardScale(boardScale);

        }
        heldCards.Clear();
        _layoutLocked.Clear();
    }

    public Vector3 GetBoardScale() => boardScale;
    public Vector3 GetHandScale() => handScale;
    public Vector3 GetPressInspectScale() => pressInspectScale;
    public float GetPressInspectForwardOffset() => pressInspectForwardOffset;
    public Vector3 GetPressDragScale() => pressDragScale;
    public float GetPressDragForwardOffset() => pressDragForwardOffset;
    public float GetDragCameraActivationDistance() => dragCameraActivationDistance;

    public int GetBaseSortingOrder() => baseSortingOrder;
    public int GetDragTopSortingOrder() => dragTopSortingOrder;

    /// <summary>
    /// 모든 레이아웃 잠금 상태를 즉시 해제합니다.
    /// </summary>
    public void ClearLayoutLocks()
    {
        _layoutLocked.Clear();
        _lockedCard = null;
    }
}

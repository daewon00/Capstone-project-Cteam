using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;
using DG.Tweening;

public class Card : MonoBehaviour
{
    public CardScriptableObject cardSO;

    public bool isPlayer;

    public int currentHealth;
    public int attackPower, manaCost;

    public TMP_Text healthText, attackText, costText, nameText, actionDescriptionText, loreText;
    public Image characterArt, bgArt;

    private Vector3 targetPoint;
    private Quaternion targetRot;
    public float moveSpeed = 5f, rotateSpeed = 540f;

    public bool inHand;
    public int handPosition;

    private HandController theHC;

    private bool isSelected;
    public Collider theCol;

    public LayerMask whatIsDesktop, whatIsPlacement;
    private bool justPressed;

    public CardPlacePoint assignedPlace;

    public Animator anim;

    void Start()
    {
        if (targetPoint == Vector3.zero)
        {
            targetPoint = transform.position;
            targetRot = transform.rotation;
        }

        SetupCard();

        theHC = FindAnyObjectByType<HandController>();
        theCol = GetComponent<Collider>();
    }

    public void SetupCard()
    {
        currentHealth = cardSO.currentHealth;
        attackPower = cardSO.attackPower;
        manaCost = cardSO.manaCost;

        UpdateCardDisplay();

        nameText.text = cardSO.cardName;
        actionDescriptionText.text = cardSO.actionDescription;
        loreText.text = cardSO.cardLore;

        characterArt.sprite = cardSO.characterSprite;
        bgArt.sprite = cardSO.bgSprite;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPoint, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

        if (isSelected && !BattleController.instance.battleEnded && Time.timeScale != 0f)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, whatIsDesktop))
                MoveToPoint(hit.point + new Vector3(0f, 2f, 0f), Quaternion.identity);

            if (Input.GetMouseButtonDown(1))
                ReturnToHand();

            if (Input.GetMouseButtonDown(0) && !justPressed)
            {
                if (Physics.Raycast(ray, out hit, 100f, whatIsPlacement)
                    && BattleController.instance.currentPhase == BattleController.TurnOrder.playerActive)
                {
                    CardPlacePoint selectedPoint = hit.collider.GetComponent<CardPlacePoint>();

                    if (selectedPoint.activeCard == null && selectedPoint.isPlayerPoint)
                    {
                        if (BattleController.instance.playerMana >= manaCost)
                        {
                            selectedPoint.activeCard = this;
                            assignedPlace = selectedPoint;
                            inHand = false;
                            isSelected = false;
                            theHC.RemoveCardFromHand(this);
                            BattleController.instance.SpendPlayerMana(manaCost);
                            AudioManager.instance.PlaySFX(4);

                            if (assignedPlace.cameraFocusPoint != null)
                                CameraController.instance.MoveTo(assignedPlace.cameraFocusPoint);
                            else
                                Debug.LogWarning("CardPlacePoint.cameraFocusPoint is not set!", assignedPlace);

                            MoveToPoint(selectedPoint.transform.position, transform.rotation);

                            CameraController.instance.MoveTo(CameraController.instance.homeTransform);
                        }
                        else
                        {
                            ReturnToHand();
                            UIController.instance.ShowManaWarning();
                        }
                    }
                    else
                    {
                        ReturnToHand();
                    }
                }
                else
                {
                    ReturnToHand();
                }
            }
        }

        justPressed = false;
    }

    private void OnMouseDown()
    {
        if (inHand && BattleController.instance.currentPhase == BattleController.TurnOrder.playerActive
            && isPlayer && !BattleController.instance.battleEnded && Time.timeScale != 0f)
        {
            isSelected = true;
            theCol.enabled = false;
            justPressed = true;

            // 카드 선택 시 카메라 클로즈업
            CameraController.instance.MoveTo(CameraController.instance.battleTransform);
        }
    }

    private void OnMouseOver()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition] + new Vector3(0f, 1f, .5f), Quaternion.identity);

    }

    private void OnMouseExit()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);

    }



    public void MoveToPoint(Vector3 pointToMoveTo, Quaternion rotToMatch)
    {
        targetPoint = pointToMoveTo;
        targetRot = rotToMatch;
    }

    public void ReturnToHand()
    {
        isSelected = false;
        theCol.enabled = true;
        MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);

        // 카드 반납 시 카메라 원위치
        CameraController.instance.MoveTo(CameraController.instance.homeTransform);
    }

    public void DamageCard(int damageAmount)
    {
        currentHealth -= damageAmount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            assignedPlace.activeCard = null;
            MoveToPoint(BattleController.instance.discardPoint.position, BattleController.instance.discardPoint.rotation);
            anim.SetTrigger("Jump");
            Destroy(gameObject, 5f);
            AudioManager.instance.PlaySFX(2);
        }
        else
        {
            AudioManager.instance.PlaySFX(1);
        }

        anim.SetTrigger("Hurt");
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay()
    {
        healthText.text = currentHealth.ToString();
        attackText.text = attackPower.ToString();
        costText.text = manaCost.ToString();
    }
}
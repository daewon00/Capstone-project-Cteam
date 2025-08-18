using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;
using DG.Tweening;

public class Card : MonoBehaviour
{
    public CardScriptableObject cardSO; //카드 설계도

    public static Card instance;

    public bool isPlayer;   //플레이어 카드인지 참 거짓

    public int currentHealth;   //카드 체력
    public int attackPower, manaCost;   //카드 공격력, 마나 코스트

    //카드 UI 연결
    public TMP_Text healthText, attackText, costText, nameText, actionDescriptionText, loreText;
    public Image characterArt, bgArt;

    //카드 움직임 관련
    private Vector3 targetPoint;
    private Quaternion targetRot;
    public float moveSpeed = 5f, rotateSpeed = 540f;

    public bool inHand; //핸드에 있는지 참 거짓
    public int handPosition; //핸드 위치

    private HandController theHC;   //핸드 전체를 관리하는 스크립트

    private bool isSelected;    //선택한 카드 참 거짓
    public Collider theCol; //카드 충돌 영역

    private bool justPressed;   //누린 직후 참 거짓(중복 클릭 방지)

    public CardPlacePoint assignedPlace;    //카드 필드 위치

    public Animator anim;// 카드 애니메이션

    public LayerMask whatIsDesktop, whatIsPlacement;    //카드 내려놓을 레이어

    void Awake()
    {
        instance = this; 
    }


    void Start()
    {
        if (targetPoint == Vector3.zero)
        {
            targetPoint = transform.position;
            targetRot = transform.rotation;
        }

        SetupCard();    //카드 설계도 값을 불러와 변수와 UI 적용

        theHC = FindAnyObjectByType<HandController>();
        theCol = GetComponent<Collider>();
    }

    public void SetupCard() //카드 설계도 값을 불러와 변수와 UI 적용
    {
        currentHealth = cardSO.currentHealth;
        attackPower = cardSO.attackPower;
        if (isPlayer && PlayerBuffs.instance != null)
        {
            attackPower += PlayerBuffs.instance.attackBonus;
        }
        manaCost = cardSO.manaCost;

        UpdateCardDisplay();

        nameText.text = cardSO.cardName;
        actionDescriptionText.text = cardSO.actionDescription;
        loreText.text = cardSO.cardLore;

        characterArt.sprite = cardSO.characterSprite;
        bgArt.sprite = cardSO.bgSprite;
        ApplyAttackBuffOutline(isPlayer && PlayerBuffs.instance != null && PlayerBuffs.instance.attackBonus > 0);
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPoint, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

        //카드가 선택되고 배틀이 진행중이라면 Y축 2 증가해서 든다
        if (isSelected && !BattleController.instance.battleEnded && Time.timeScale != 0f)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, whatIsDesktop))
                MoveToPoint(hit.point + new Vector3(0f, 2f, 0f), Quaternion.identity);

            //우클릭시 핸드로 다시 돌림
            if (Input.GetMouseButtonDown(1))
                ReturnToHand();

            //좌클릭을 텀을 두고 다시 눌렀을 경우 카드를 놓을 수 있다면(빈칸, 마나) 놓는다
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
                           

                            MoveToPoint(selectedPoint.transform.position, transform.rotation);

                            CameraController.instance.MoveTo(CameraController.instance.homeTransform);  // 카메라를 다시 기본 시점으로
                        }
                        else //마나 부족
                        {
                            ReturnToHand();
                            UIController.instance.ShowManaWarning();
                        }
                    }
                    else // 놓을 빈칸이 아니라면
                    {
                        ReturnToHand();
                    }
                }
                else // 허공에 클릭했다면
                {
                    ReturnToHand();
                }
            }
        }

        justPressed = false;    // 다음 클릭 준비
    }

    //카드 좌클릭 선택시 카메라를 필드뷰로 이동
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

    //카드 위에 마우스가 있을시 1프레임 띄워서 보여줌
    private void OnMouseOver()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition] + new Vector3(0f, .1f, .5f), transform.rotation);

    }

    //카드 위에 마우스가  벗어날시 원상태로
    private void OnMouseExit()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);

    }


    //카드를 지정된 위치와 회전값으로 이동을 위해 변수 설정
    public void MoveToPoint(Vector3 pointToMoveTo, Quaternion rotToMatch)
    {
        targetPoint = pointToMoveTo;
        targetRot = rotToMatch;
    }

    //핸드로 되돌림
    public void ReturnToHand()
    {
        isSelected = false;
        theCol.enabled = true;
        MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);

        // 카드 반납 시 카메라 원위치
        CameraController.instance.MoveTo(CameraController.instance.homeTransform);
    }

    //다른 카드로 부터 데미지를 받을때
    public void DamageCard(int damageAmount)
    {
        currentHealth -= damageAmount;
        if (currentHealth <= 0) // 죽을시
        {
            currentHealth = 0;
            assignedPlace.activeCard = null;    // 자리 비우고
            MoveToPoint(BattleController.instance.discardPoint.position, BattleController.instance.discardPoint.rotation);  // 묘지로 이동
            anim.SetTrigger("Jump");    // 점프 애니메이션
            Destroy(gameObject, 5f);    // 5초뒤 카드 제거
            AudioManager.instance.PlaySFX(2);   //2번 효과음
        }
        else // 살았다면 효과음 1 재생
        {
            AudioManager.instance.PlaySFX(1);
        }

        anim.SetTrigger("Hurt");    // 맞는 애니메이션
        UpdateCardDisplay();    //체력 UI 수정
    }

    //카드 현 상태 UI 텍스트 설정
    public void UpdateCardDisplay()
    {
        healthText.text = currentHealth.ToString();
        attackText.text = attackPower.ToString();
        costText.text = manaCost.ToString();
    }
    public void ApplyAttackBuffOutline(bool on)
    {
        // TextMeshProUGUI는 outlineWidth/outlineColor 제공
        var tmp = attackText; // TMP_Text
        if (on)
        {
            // 공유 머티리얼에 직접 쓰면 다른 카드에도 퍼질 수 있으니 인스턴스화 권장
            if (!ReferenceEquals(tmp.fontMaterial, tmp.fontSharedMaterial))
                ; // 이미 인스턴스 재료면 그대로 사용
            else
                tmp.fontMaterial = new Material(tmp.fontSharedMaterial);

            tmp.outlineWidth = 0.2f;          // 필요 시 조절
            tmp.outlineColor = Color.green;   // 요구사항: 초록색 외곽선
        }
        else
        {
            if (!ReferenceEquals(tmp.fontMaterial, tmp.fontSharedMaterial))
                tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
            tmp.outlineWidth = 0f;
            // 색상은 굳이 초기화 안 해도 됨
        }
    }
}
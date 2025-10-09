using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;
using DG.Tweening;
using UnityEngine.EventSystems;
using BattleSnapshot;

public class Card : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    public CardScriptableObject cardSO; //카드 설계도

    public bool isPlayer;   //플레이어 카드인지 참 거짓

    public int currentHealth;   //카드 체력
    private int _baseHealth; //체력유물 적용효과들
    private int _lastModifierMaxHealth;
    private bool _healthInitialized;
    public int BaseHealth => _baseHealth;
    public int MaxHealth => _lastModifierMaxHealth;
    public int attackPower, manaCost;   //카드 공격력, 마나 코스트

    //카드 UI 연결
    [Header("UI References")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text actionDescriptionText;
    [SerializeField] private TMP_Text loreText;
    [SerializeField] private Image characterArt;
    [SerializeField] private Image bgArt;
    [SerializeField] private Image skillEffectImage;
    [SerializeField] private TMP_Text skillEffectValueText;
    [SerializeField] private EffectIconDatabase iconDatabaseOverride;

    //카드 움직임 관련
    private Vector3 targetPoint;
    private Quaternion targetRot;
    public float moveSpeed = 5f, rotateSpeed = 540f;

    public bool inHand; //핸드에 있는지 참 거짓
    public int handPosition; //핸드 위치

    private HandController theHC;   //핸드 전체를 관리하는 스크립트
    public Collider theCol; //카드 충돌 영역


    public CardPlacePoint assignedPlace;    //카드 필드 위치

    public Animator anim;// 카드 애니메이션

    public LayerMask whatIsDesktop, whatIsPlacement;    //카드 내려놓을 레이어

    // 서비스 경로: 런타임 식별자와 서비스 참조
    public string InstanceId { get; private set; }
    [SerializeField] private string _battleInstanceId;
    private IDeckService _deckService;
    private EffectIconDatabase _iconDatabase;
    private bool _isInteractable = true;
    private bool _destroyBroadcasted; // 업적/메타 이벤트 중복 방지

    // 이벤트 기반 입력 상태(탭/드래그 구분)
    private bool _isDragging;
    private Vector2 _dragStartScreenPos;
    private float _dragStartTime;
    private const float TapTimeThreshold = 0.2f;      // 초
    private const float TapDistanceThreshold = 10f;   // 스크린 픽셀

    // --- 카드 누르기(Press) 피드백 ---
    // 살짝 들어 올리고(위/앞) 스케일을 키워 입력 피드백 제공
    private Vector3 _pressPositionOffset = new Vector3(0f, 0.12f, 0.4f);
    private Vector3 _pressScaleMultiplier = new Vector3(1.06f, 1.06f, 1.06f);
    private float _pressAnimationTime = 0.1f;
    private Vector3 _originalScale;

    // 드래그 중 현재 하이라이트한 슬롯 캐시(잔상 방지)
    private CardPlacePoint _currentHoveredSlot;

    void Awake()
    {
        _originalScale = transform.localScale;
        if (_iconDatabase == null && iconDatabaseOverride != null)
            _iconDatabase = iconDatabaseOverride;
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
        if (cardSO == null)
        {
            Debug.LogWarning("[Card] SetupCard called without cardSO", this);
            return;
        }

        attackPower = cardSO.attackPower;
        /*if (isPlayer && PlayerBuffs.instance != null)
        {
            attackPower += PlayerBuffs.instance.attackBonus;
        }*/
        manaCost = cardSO.manaCost;

        InitializeHealthFromDefinition();

        UpdateCardDisplay();

        if (nameText != null)
            nameText.text = cardSO.cardName;
        if (actionDescriptionText != null)
            actionDescriptionText.text = cardSO.actionDescription;
        if (loreText != null)
            loreText.text = cardSO.cardLore;

        if (characterArt != null)
            characterArt.sprite = cardSO.characterSprite;
        if (bgArt != null)
            bgArt.sprite = cardSO.bgSprite;

        UpdateSkillIcon();
        //ApplyAttackBuffOutline(isPlayer && PlayerBuffs.instance != null && PlayerBuffs.instance.attackBonus > 0);
    }
    private void InitializeHealthFromDefinition()
    {
        _baseHealth = Mathf.Max(0, cardSO != null ? cardSO.currentHealth : currentHealth);
        _lastModifierMaxHealth = _baseHealth;
        currentHealth = _baseHealth;
        _healthInitialized = true;
        RecalculateHealthFromModifiers(resetCurrent: true, updateDisplay: false);
    }

    private void RecalculateHealthFromModifiers(bool resetCurrent, bool updateDisplay = true)
    {
        if (!_healthInitialized)
            return;

        int baseValue = Mathf.Max(0, _baseHealth);
        int newMax = Mathf.Max(0, GameEvents.ApplyCardHealthModifiers(this, baseValue));

        if (resetCurrent)
        {
            currentHealth = newMax;
        }
        else
        {
            int diff = newMax - _lastModifierMaxHealth;
            currentHealth = Mathf.Clamp(currentHealth + diff, 0, newMax);
        }

        _lastModifierMaxHealth = newMax;

        if (updateDisplay)
            UpdateCardDisplay();
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPoint, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    // UI 이벤트 시스템 클릭 처리: 카드 선택 전용(사용은 배치 시 BattleController 경유)
    public void OnPointerClick(PointerEventData eventData)
    {
        // 이벤트 기반 드래그로 대체. 필요 시 탭 동작을 OnPointerUp에서 처리.
        return;
    }

    // 서비스/식별자/데이터를 주입하는 초기화 진입점
    public void Initialize(string instanceId, CardScriptableObject so, IDeckService deckService, EffectIconDatabase iconDatabase)
    {
        if (string.IsNullOrEmpty(instanceId) || so == null || deckService == null)
        {
            Debug.LogError($"[Card] Initialize 실패: id={instanceId}, so={(so==null?"null":so.name)}, svc={(deckService==null?"null":"ok")}", this);
            gameObject.SetActive(false);
            return;
        }
        InstanceId = instanceId;
        _battleInstanceId = instanceId;
        cardSO = so;
        _deckService = deckService;
        _iconDatabase = iconDatabase != null ? iconDatabase : iconDatabaseOverride;
        SetupCard();
        SetInteractable(true);
    }

    public void SetBattleInstanceId(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            _battleInstanceId = id;
        }
    }

    public string GetBattleInstanceId()
    {
        if (!string.IsNullOrEmpty(InstanceId)) return InstanceId;
        if (string.IsNullOrEmpty(_battleInstanceId))
            _battleInstanceId = Guid.NewGuid().ToString("N");
        return _battleInstanceId;
    }

    public void SetInteractable(bool value)
    {
        _isInteractable = value;
        if (theCol != null) theCol.enabled = value;
    }

    // 에디터에서만 호버 효과 유지(모바일 비활성)
#if !(UNITY_ANDROID || UNITY_IOS)
    private void OnMouseOver()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition] + new Vector3(0f, .1f, .5f), transform.rotation);
    }

    private void OnMouseExit()
    {
        if (inHand && isPlayer && !BattleController.instance.battleEnded)
            MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);
    }
#endif


    //카드를 지정된 위치와 회전값으로 이동을 위해 변수 설정
    public void MoveToPoint(Vector3 pointToMoveTo, Quaternion rotToMatch)
    {
        targetPoint = pointToMoveTo;
        targetRot = rotToMatch;
    }

    //핸드로 되돌림
    public void ReturnToHand()
    {
        ClearHoverHighlight();
        theCol.enabled = true;
        MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);
        if (HandController.instance != null)
            SetCardScale(HandController.instance.GetHandScale());

        // 카드 반납 시 카메라 원위치
        CameraController.instance.MoveTo(CameraController.instance.homeTransform);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Card] ReturnToHand: instance={InstanceId}, handPos={handPosition}");
#endif
    }

    //다른 카드로 부터 데미지를 받을때
    public CardDamageResult DamageCard(int damageAmount, Card attacker = null, DamageSourceKind sourceKind = DamageSourceKind.Attack)
    {
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        var mitigation = effectService?.ProcessCardDamage(this, attacker, damageAmount, sourceKind)
            ?? new DamageMitigationResult(damageAmount, 0);

        int appliedDamage = mitigation.RemainingDamage;
        if (appliedDamage <= 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (mitigation.BlockedDamage > 0)
            {
                Debug.Log($"[Card] Damage fully blocked by effects. instance={InstanceId}, blocked={mitigation.BlockedDamage}");
            }
#endif
            effectService?.HandleCardDamaged(this, attacker, 0, sourceKind);
            if (mitigation.BlockedDamage > 0)
            {
                UpdateCardDisplay();
                BattleDeckRuntimeSync.UpdateCardState(this);
            }
            return new CardDamageResult(0, false);
        }

        currentHealth -= appliedDamage;
        bool destroyed = currentHealth <= 0;

        effectService?.HandleCardDamaged(this, attacker, appliedDamage, sourceKind);

        if (destroyed)
        {
            currentHealth = 0;
            HandleDeath(effectService, attacker);
            return new CardDamageResult(appliedDamage, true);
        }

        AudioManager.instance.PlaySFX(1);
        anim.SetTrigger("Hurt");
        UpdateCardDisplay();
        BattleDeckRuntimeSync.UpdateCardState(this);
        return new CardDamageResult(appliedDamage, false);
    }

    public void ForceKill(Card killer = null)
    {
        var effectService = ServiceRegistry.Get<ICardEffectService>();
        HandleDeath(effectService, killer);
    }

    private void HandleDeath(ICardEffectService effectService, Card killer)
    {
        if (assignedPlace != null && assignedPlace.activeCard == this)
        {
            assignedPlace.activeCard = null;
        }
        assignedPlace = null;
        transform.SetParent(null, true);
        inHand = false;

        effectService?.UnregisterBoardCard(this);

        if (!_destroyBroadcasted && !isPlayer)
        {
            _destroyBroadcasted = true;
            try
            {
                var runId = (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
                    ? GameContext.I.RunId
                    : PlayerPrefs.GetString("lastRunId", "");
                if (!string.IsNullOrEmpty(runId))
                {
                    string cid = string.Empty;
                    if (cardSO != null)
                    {
                        cid = !string.IsNullOrEmpty(cardSO.CardId) ? cardSO.CardId : (cardSO.cardName ?? string.Empty);
                    }
                    MetaEvents.RaiseEnemyCardDestroyed(new MetaEvents.EnemyCardDestroyedPayload
                    {
                        RunId = runId,
                        CardId = cid,
                        InstanceId = InstanceId
                    });
                }
            }
            catch { }
        }

        MoveToPoint(BattleController.instance.discardPoint.position, BattleController.instance.discardPoint.rotation);
        anim.SetTrigger("Jump");
        AudioManager.instance.PlaySFX(2);
        BattleDeckRuntimeSync.UpdateCardState(this);
        Destroy(gameObject, 5f);
    }

    private void UpdateSkillIcon()
    {
        if (skillEffectImage == null)
            return;

        skillEffectImage.gameObject.SetActive(false);

        if (cardSO == null)
            return;

        var effects = cardSO.Effects;
        if (effects == null || effects.Count == 0)
            return;

        var effect = effects[0];
        if (effect == null)
            return;

        Sprite icon = _iconDatabase != null ? _iconDatabase.GetIcon(effect.Type) : null;
        int value = effect != null ? (effect.Value != 0 ? effect.Value : effect.Potency) : 0;
        if (effect != null && effect.Type == CardEffectType.AddShield)
        {
            var effectService = ServiceRegistry.Get<ICardEffectService>();
            var runtime = effectService?.CaptureCardState(this);
            if (runtime != null)
                value = runtime.shield;
        }
        if (icon == null)
            return;

        skillEffectImage.sprite = icon;
        skillEffectImage.gameObject.SetActive(true);
        if (skillEffectValueText != null)
        {
            if (value != 0)
            {
                skillEffectValueText.text = value > 0 ? $"+{value}" : value.ToString();
                skillEffectValueText.gameObject.SetActive(true);
            }
            else
            {
                skillEffectValueText.gameObject.SetActive(false);
            }
        }
    }

    public void SetCardScale(Vector3 scale)
    {
        transform.localScale = scale;
        _originalScale = scale;
    }

    //카드 현 상태 UI 텍스트 설정
    public void UpdateCardDisplay()
    {
        var shownAtk = GetEffectiveAttack(); //추가+++
        if (attackText != null)
            attackText.text = shownAtk.ToString();//추가+++ 공격력증가
        if (healthText != null)
            healthText.text = currentHealth.ToString();
        //attackText.text = attackPower.ToString(); //기존
        if (costText != null)
            costText.text = GetEffectiveManaCost().ToString();

        // (선택) 버프면 초록색 등 시각효과
        //bool buffed = isPlayer && shownAtk > attackPower;
        //attackText.color = buffed ? new Color(0.2f, 1f, 0.2f) : Color.white;
        UpdateSkillIcon();
    }

    /// <summary>
    /// 카드 공격력을 모든 런타임 보정(카드 전용 → 소유자 전역 → 오라 차감)까지 적용한 값으로 계산합니다.
    /// 전투 피해량 산출과 UI 표시가 동일한 계산 경로를 사용하도록 이 메서드를 공유합니다.
    /// </summary>
    public int CalculateCombatAttack(bool includeAuraPenalty = true)
    {
        int value = attackPower;
        if (BattleController.instance != null)
        {
            value = GameEvents.ApplyCardAttackModifiers(this, value);
 
            if (isPlayer)
            {
                value = GameEvents.ApplyPlayerAttackModifiers(value);
            }
            else
            {
                value = GameEvents.ApplyEnemyAttackModifiers(value);
            }
        }

        if (includeAuraPenalty && assignedPlace != null)
        {
            var effectService = ServiceRegistry.Get<ICardEffectService>();
            if (effectService != null)
            {
                var snapshot = effectService.CaptureCardState(this);
                if (snapshot != null && snapshot.auraBonus > 0)
                    value = Mathf.Max(0, value - snapshot.auraBonus);
            }
        }

        return value;
    }

    // 플레이어 카드면 유물 체인을 통과한 "표시용 공격력"을 돌려줌
    public int GetEffectiveAttack() => CalculateCombatAttack();

    public int GetEffectiveManaCost()
    {
        int value = manaCost;

        if (BattleController.instance != null)
        {
            value = GameEvents.ApplyCardManaCostModifiers(this, value);
        }

        return Mathf.Max(0, value);
    }
    private void OnEnable()
    {
        if (RelicSystem.Instance != null)
            RelicSystem.Instance.RelicsChanged += UpdateCardDisplay;
        GameEvents.OnPlayerAttackModifiersChanged += HandleAttackModifiersChanged;
        GameEvents.OnEnemyAttackModifiersChanged += HandleAttackModifiersChanged;
        GameEvents.OnCardAttackModifiersChanged += HandleAttackModifiersChanged;
        GameEvents.OnCardHealthModifiersChanged += HandleHealthModifiersChanged;
    }

    private void OnDisable()
    {
        if (RelicSystem.Instance != null)
            RelicSystem.Instance.RelicsChanged -= UpdateCardDisplay;
        GameEvents.OnPlayerAttackModifiersChanged -= HandleAttackModifiersChanged;
        GameEvents.OnEnemyAttackModifiersChanged -= HandleAttackModifiersChanged;
        GameEvents.OnCardAttackModifiersChanged -= HandleAttackModifiersChanged;
        GameEvents.OnCardHealthModifiersChanged -= HandleHealthModifiersChanged;

        // 비활성화 시 하이라이트 잔상 제거
        ClearHoverHighlight();
    }

    private void HandleAttackModifiersChanged()
    {
        if (assignedPlace != null || inHand)
            UpdateCardDisplay();
    }
    private void HandleHealthModifiersChanged()
    {
        if (!_healthInitialized)
            return;

        bool shouldUpdateDisplay = assignedPlace != null || inHand;
        RecalculateHealthFromModifiers(resetCurrent: false, updateDisplay: shouldUpdateDisplay);
    }
    // =============================
    // 이벤트 기반 입력 핸들러 구현
    // =============================
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_isInteractable || assignedPlace != null || BattleController.instance == null) return;
        _dragStartScreenPos = eventData.position;
        _dragStartTime = Time.time;

        // Press 피드백: 스케일 업 + 살짝 들어 올리기
        transform.DOKill(false);
        transform.DOScale(_pressScaleMultiplier, _pressAnimationTime).SetEase(Ease.OutQuad);
        if (theHC != null && handPosition >= 0 && handPosition < theHC.cardPositions.Count)
        {
            MoveToPoint(theHC.cardPositions[handPosition] + _pressPositionOffset, transform.rotation);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isInteractable || !inHand || assignedPlace != null) return;
        if (BattleController.instance == null || BattleController.instance.battleEnded) return;

        _isDragging = true;

        if (theCol != null) theCol.enabled = false; // 자기 자신 레이캐스트 방지

        if (CameraController.instance != null && CameraController.instance.battleTransform != null)
            CameraController.instance.MoveTo(CameraController.instance.battleTransform);

        // 드래그 중에는 레이아웃 재정렬에서 제외
        if (theHC != null) theHC.SuspendLayoutFor(this);

        // 프레스 트윈 잔여 제거(드래그로 자연 전환)
        transform.DOKill(false);

        // 드래그 시작: 전장 집중을 위해 일부 UI 숨김(CanvasGroup 기반)
        UIController.instance?.SetDragModeUIVisibility(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || BattleController.instance == null || BattleController.instance.battleEnded) return;

        Ray ray = Camera.main.ScreenPointToRay(eventData.position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, whatIsDesktop))
        {
            MoveToPoint(hit.point + new Vector3(0f, 2f, 0f), Quaternion.identity);
        }

        // 드롭 포인트 하이라이트(슬롯 변화 시에만 업데이트)
        UpdateHoverHighlight(ray);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (theCol != null) theCol.enabled = true;

        // 프레스/드래그 시각 효과 원복
        transform.DOKill(false);

        // 드래그 종료 시 하이라이트 정리
        ClearHoverHighlight();

        // 1) 유효한 배치 포인트 검사 + 플레이 가능성 선검사
        Ray ray = Camera.main.ScreenPointToRay(eventData.position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, whatIsPlacement))
        {
            var selectedPoint = hit.collider.GetComponent<CardPlacePoint>();
            if (selectedPoint != null && selectedPoint.activeCard == null && selectedPoint.isPlayerPoint)
            {
                // 플레이 가능성(턴/마나) 선검사
                var playable = BattleController.instance.EvaluatePlayability(this);
                if (playable != BattleController.Playability.Ok)
                {
                    UIController.instance?.ShowManaWarning();
                    if (theHC != null) theHC.ResumeLayoutFor(this);
                    UIController.instance?.SetDragModeUIVisibility(true);
                    ReturnToHand();
                    return;
                }

                // 2) 보드 표기 선반영으로 풀반납 방지
                selectedPoint.activeCard = this;
                assignedPlace = selectedPoint;
                inHand = false;

                bool success = BattleController.instance.AttemptPlayCard(this);
                if (success)
                {
                    GameEvents.RaiseCardPlayed(this);
                    AudioManager.instance.PlaySFX(4);

                    if (assignedPlace.cameraFocusPoint != null)
                        CameraController.instance.MoveTo(assignedPlace.cameraFocusPoint);

                    // 보드 컨테이너로 부모 변경(핸드 재정렬의 간섭 차단)
                    transform.SetParent(selectedPoint.transform, true);
                    MoveToPoint(selectedPoint.transform.position, transform.rotation);
                    if (HandController.instance != null)
                    {
                        SetCardScale(HandController.instance.GetBoardScale());
                        transform.DOKill(false);
                        transform.localScale = HandController.instance.GetBoardScale();
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Card] Placed: instance={InstanceId}, parent={(transform.parent != null ? transform.parent.name : "<none>")}, pos={transform.position}");
#endif
                    SetInteractable(false);
                    CameraController.instance.MoveTo(CameraController.instance.homeTransform);
                    if (theHC != null) theHC.ResumeLayoutFor(this);
                    UIController.instance?.SetDragModeUIVisibility(true);
                    BattleDeckRuntimeSync.UpdateCardState(this);
                    return;
                }

                // 3) 실패 시 원복
                selectedPoint.activeCard = null;
                assignedPlace = null;
                inHand = true;
                if (theHC != null) theHC.ResumeLayoutFor(this);
                UIController.instance?.SetDragModeUIVisibility(true);
                transform.DOScale(_originalScale, _pressAnimationTime).SetEase(Ease.OutQuad);
                ReturnToHand();
                return;
            }
        }

        // 2) 슬롯 미적중: (선택) UI 위 드롭 차단
        if (EventSystem.current != null)
        {
            // 마우스/터치 분기(마우스는 파라미터 없는 버전이 더 일관적)
            bool overUI = false;
#if UNITY_STANDALONE || UNITY_EDITOR
            overUI = EventSystem.current.IsPointerOverGameObject();
#else
            overUI = EventSystem.current.IsPointerOverGameObject(eventData.pointerId);
#endif
            if (overUI)
            {
                if (theHC != null) theHC.ResumeLayoutFor(this);
                UIController.instance?.SetDragModeUIVisibility(true);
                transform.DOScale(_originalScale, _pressAnimationTime).SetEase(Ease.OutQuad);
                ReturnToHand();
                return;
            }
        }

        // 3) 일반 미스: 핸드 복귀
        if (theHC != null) theHC.ResumeLayoutFor(this);
        UIController.instance?.SetDragModeUIVisibility(true);
        ReturnToHand();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isDragging) return; // 드래그 종료에서 처리됨
        float duration = Time.time - _dragStartTime;
        float dist = Vector2.Distance(eventData.position, _dragStartScreenPos);
        if (duration < TapTimeThreshold && dist < TapDistanceThreshold)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Card] Tapped: instance={InstanceId}, name={cardSO?.cardName}");
#endif
            // TODO: 카드 상세보기 등 탭 행동 연결 가능
        }

        // 탭/클릭 종료 시 비주얼 원복
        transform.DOKill(false);
        transform.DOScale(_originalScale, _pressAnimationTime).SetEase(Ease.OutQuad);

        // 필드 위 카드나 inHand가 아닌 경우는 위치 복귀를 수행하지 않음
        if (!inHand || assignedPlace != null)
        {
            return;
        }

        if (theHC != null && handPosition >= 0 && handPosition < theHC.cardPositions.Count)
        {
            MoveToPoint(theHC.cardPositions[handPosition], theHC.minpos.rotation);
        }
        BattleDeckRuntimeSync.UpdateCardState(this);
    }

    private void UpdateHoverHighlight(Ray pointerRay)
    {
        RaycastHit slotHit;
        CardPlacePoint next = null;
        if (Physics.Raycast(pointerRay, out slotHit, 100f, whatIsPlacement))
        {
            next = slotHit.collider.GetComponent<CardPlacePoint>()
                   ?? slotHit.collider.GetComponentInParent<CardPlacePoint>();
        }

        if (next == _currentHoveredSlot)
        {
            // 같은 슬롯이면 추가 판정 불필요(깜빡임 방지)
            return;
        }

        // 이전 슬롯 하이라이트 해제
        if (_currentHoveredSlot != null)
        {
            _currentHoveredSlot.SetHighlightState(CardPlacePoint.HighlightState.Off);
            _currentHoveredSlot = null;
        }

        // 신규 슬롯 반영
        if (next != null)
        {
            var allowed = false;
            if (next.activeCard == null && next.isPlayerPoint)
            {
                var playable = BattleController.instance.EvaluatePlayability(this);
                allowed = (playable == BattleController.Playability.Ok);
            }

            _currentHoveredSlot = next;
            _currentHoveredSlot.SetHighlightState(
                allowed ? CardPlacePoint.HighlightState.Allowed : CardPlacePoint.HighlightState.Blocked);
        }
    }

    private void ClearHoverHighlight()
    {
        if (_currentHoveredSlot != null)
        {
            _currentHoveredSlot.SetHighlightState(CardPlacePoint.HighlightState.Off);
            _currentHoveredSlot = null;
        }
    }
    /*public void ApplyAttackBuffOutline(bool on)
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
    }*/
}

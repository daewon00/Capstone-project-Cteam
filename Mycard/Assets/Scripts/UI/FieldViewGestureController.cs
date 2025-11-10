using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 중 간단한 스와이프 제스처로 카메라를 손패 뷰와 필드 뷰 사이에서 전환합니다.
/// </summary>
public class FieldViewGestureController : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField, Tooltip("드래그가 전환으로 인식되기 위한 최소 픽셀 거리")]
    private float swipeThreshold = 140f;

    [SerializeField, Tooltip("필드로 인식할 화면 Y 정규 구간의 하한 (0=바닥, 1=상단)")]
    [Range(0f, 1f)] private float fieldZoneMinNormalizedY = 0.35f;

    [SerializeField, Tooltip("필드로 인식할 화면 Y 정규 구간의 상한 (0=바닥, 1=상단)")]
    [Range(0f, 1f)] private float fieldZoneMaxNormalizedY = 0.95f;

    [Header("Interaction Guards")]
    [SerializeField, Tooltip("카드를 직접 선택한 입력은 제스처로 취급하지 않습니다.")]
    private LayerMask cardLayerMask = ~0;

    [SerializeField, Tooltip("Debug 로그를 출력합니다.")]
    private bool verboseLogging = false;

    [Header("Optional Overlay References")]
    [SerializeField, Tooltip("제스처 영역을 덮는 Graphic(Image 등). 비워두면 자동으로 자신의 Graphic을 사용합니다.")]
    private Graphic overlayGraphic;
    [SerializeField, Tooltip("제스처 영역을 제어할 CanvasGroup(optional)")]
    private CanvasGroup overlayCanvasGroup;
    [SerializeField, Tooltip("제스처가 무시해야 할 오버레이 루트 Transform")]
    private Transform overlayIgnoreRoot;
    [SerializeField, Tooltip("필드 제스처를 허용할 RectTransform. 비워두면 전체 화면을 사용합니다.")]
    private RectTransform allowedRegion;
    [Header("Tutorial Target")]
    [SerializeField, Tooltip("튜토리얼 하이라이트에 사용할 타깃 ID입니다.")]
    private string tutorialTargetId = "field-swipe-zone";

    private bool _tracking;
    private Vector2 _startScreenPosition;
    private int _activePointerId = -1;
    private bool _gestureTriggered;
    private PointerEventData _raycastEventData;
    private readonly List<RaycastResult> _raycastResults = new(16);
    private static int _cardDragSuppression;
    private TutorialTarget _tutorialTarget;

    public static void PushCardDragSuppression()
    {
        _cardDragSuppression = Mathf.Clamp(_cardDragSuppression + 1, 0, int.MaxValue);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[FieldViewGesture] Suppress ++ => count={_cardDragSuppression}");
#endif
    }

    public static void PopCardDragSuppression()
    {
        _cardDragSuppression = Mathf.Max(0, _cardDragSuppression - 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[FieldViewGesture] Suppress -- => count={_cardDragSuppression}");
#endif
    }

    private void Awake()
    {
        if (overlayGraphic == null)
            overlayGraphic = GetComponent<Graphic>();
        if (overlayGraphic != null)
        {
            overlayGraphic.raycastTarget = false;
            if (overlayIgnoreRoot == null)
                overlayIgnoreRoot = overlayGraphic.transform;
        }
        if (overlayCanvasGroup == null)
            overlayCanvasGroup = GetComponent<CanvasGroup>();
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.blocksRaycasts = false;
        }

        EnsureTutorialTarget();
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
            return;

        if (Input.touchSupported && Input.touchCount > 0)
        {
            ProcessTouches();
        }
        else
        {
            ProcessMouse();
        }
    }

    private void ProcessMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryBeginGesture(Input.mousePosition, -1);
        }
        else if (_tracking && Input.GetMouseButton(0))
        {
            UpdateGesture(Input.mousePosition);
        }
        else if (_tracking && Input.GetMouseButtonUp(0))
        {
            ResetGesture();
        }
    }

    private void ProcessTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // 아직 트래킹 중이 아니라면 첫 번째 터치를 기준으로 사용
                    if (!_tracking)
                    {
                        TryBeginGesture(touch.position, touch.fingerId);
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (_tracking && touch.fingerId == _activePointerId)
                    {
                        UpdateGesture(touch.position);
                    }
                    break;

                case TouchPhase.Canceled:
                case TouchPhase.Ended:
                    if (_tracking && touch.fingerId == _activePointerId)
                    {
                        ResetGesture();
                    }
                    break;
            }
        }
    }

    private void TryBeginGesture(Vector2 screenPosition, int pointerId)
    {
        if (_tracking)
            return;

        if (!IsGestureGloballyAllowed())
            return;

        if (!IsWithinFieldZone(screenPosition))
            return;

        if (IsPointerOverBlockingUI(screenPosition, pointerId))
            return;

        if (IsPointerOverCard(screenPosition))
            return;

        _tracking = true;
        _gestureTriggered = false;
        _activePointerId = pointerId;
        _startScreenPosition = screenPosition;
    }

    private void UpdateGesture(Vector2 currentPosition)
    {
        if (!_tracking || _gestureTriggered)
            return;

        if (!IsGestureGloballyAllowed())
        {
            ResetGesture();
            return;
        }

        float deltaY = currentPosition.y - _startScreenPosition.y;
        bool atHomeView = CameraController.instance != null && CameraController.instance.IsAtHomeView;
        bool atBattleView = CameraController.instance != null && CameraController.instance.IsAtBattleView;

        if (atHomeView && deltaY <= -swipeThreshold)
        {
            if (SwitchToFieldView())
            {
                _gestureTriggered = true;
                Log("[Gesture] Hand → Field via swipe.");
            }
        }
        else if (atBattleView && deltaY >= swipeThreshold)
        {
            if (SwitchToHandView())
            {
                _gestureTriggered = true;
                Log("[Gesture] Field → Hand via swipe.");
            }
        }
    }

    private bool SwitchToFieldView()
    {
        if (UIController.instance != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info("[FieldViewGesture] SwitchToFieldView via UIController.FieldButton");
#endif
            UIController.instance.FieldButton();
            SyncCameraTarget(CameraController.instance != null ? CameraController.instance.battleTransform : null);
            return true;
        }

        if (CameraController.instance != null && CameraController.instance.battleTransform != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info("[FieldViewGesture] SwitchToFieldView direct MoveTo battleTransform");
#endif
            CameraController.instance.MoveTo(CameraController.instance.battleTransform);
            SyncCameraTarget(CameraController.instance.battleTransform);
            ServiceRegistry.Get<ITutorialService>()?.ReportAction(TutorialRequiredActionType.ButtonClick, "field-view-open");
            return true;
        }

        return false;
    }

    private bool SwitchToHandView()
    {
        if (UIController.instance != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info("[FieldViewGesture] SwitchToHandView via UIController.FieldBack");
#endif
            UIController.instance.FieldBack();
            SyncCameraTarget(CameraController.instance != null ? CameraController.instance.homeTransform : null);
            ServiceRegistry.Get<ITutorialService>()?.ReportAction(TutorialRequiredActionType.ButtonClick, "hand-view-open");
            return true;
        }

        if (CameraController.instance != null && CameraController.instance.homeTransform != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info("[FieldViewGesture] SwitchToHandView direct MoveTo homeTransform");
#endif
            CameraController.instance.MoveTo(CameraController.instance.homeTransform);
            SyncCameraTarget(CameraController.instance.homeTransform);
            ServiceRegistry.Get<ITutorialService>()?.ReportAction(TutorialRequiredActionType.ButtonClick, "hand-view-open");
            return true;
        }

        return false;
    }

    private void ResetGesture()
    {
        _tracking = false;
        _gestureTriggered = false;
        _activePointerId = -1;
    }

    private void EnsureTutorialTarget()
    {
        if (string.IsNullOrEmpty(tutorialTargetId)) return;
        _tutorialTarget = GetComponent<TutorialTarget>() ?? gameObject.AddComponent<TutorialTarget>();
        _tutorialTarget.SetId(tutorialTargetId);
        if (_tutorialTarget.FocusRect == null && transform is RectTransform rect)
        {
            _tutorialTarget.SetFocusRect(rect);
        }
    }

    private bool IsGestureGloballyAllowed()
    {
        if (BattleController.instance == null)
            return false;

        if (_cardDragSuppression > 0)
            return false;

        if (BattleController.instance.battleEnded)
            return false;

        if (BattleController.instance.CurrentPhase != BattleController.TurnOrder.playerActive)
            return false;

        return true;
    }

    private bool IsWithinFieldZone(Vector2 screenPosition)
    {
        if (allowedRegion != null)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(allowedRegion, screenPosition, CameraController.instance != null ? CameraController.instance.mainCamera : null))
                return false;
        }
        float normalizedY = screenPosition.y / Screen.height;
        return normalizedY >= Mathf.Min(fieldZoneMinNormalizedY, fieldZoneMaxNormalizedY) &&
               normalizedY <= Mathf.Max(fieldZoneMinNormalizedY, fieldZoneMaxNormalizedY);
    }

    private bool IsPointerOverCard(Vector2 screenPosition)
    {
        if (Camera.main == null)
            return false;

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, cardLayerMask))
        {
            if (hit.collider != null && hit.collider.GetComponentInParent<Card>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverBlockingUI(Vector2 screenPosition, int pointerId)
    {
        if (EventSystem.current == null)
            return false;

        if (_raycastEventData == null)
            _raycastEventData = new PointerEventData(EventSystem.current);

        _raycastResults.Clear();
        _raycastEventData.Reset();
        _raycastEventData.position = screenPosition;
        _raycastEventData.pointerId = pointerId;
        EventSystem.current.RaycastAll(_raycastEventData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var result = _raycastResults[i];
            var go = result.gameObject;
            if (go == null)
                continue;

            // 자기 자신(오버레이) 또는 자식은 무시
            if (overlayIgnoreRoot != null && go.transform.IsChildOf(overlayIgnoreRoot))
                continue;
            if (overlayGraphic != null && go == overlayGraphic.gameObject)
                continue;

            // UI 버튼/토글 등 상호작용 요소 위라면 제스처 비활성화
            if (go.GetComponentInParent<Button>() != null)
                return true;
            if (go.GetComponentInParent<Toggle>() != null)
                return true;
            if (go.GetComponentInParent<Slider>() != null)
                return true;
            if (go.GetComponentInParent<Scrollbar>() != null)
                return true;

            // 일반 UI(Text, Image 등)는 무시
        }

        return false;
    }

    private void Log(string message)
    {
        if (verboseLogging)
        {
            GameLog.Info($"[FieldViewGestureController] {message}");
        }
    }

    private void SyncCameraTarget(Transform target)
    {
        if (target == null || CameraController.instance == null)
            return;

        CameraController.instance.ForceSetCurrentTarget(target);
    }
}

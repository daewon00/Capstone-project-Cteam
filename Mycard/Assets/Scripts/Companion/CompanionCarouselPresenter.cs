using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 동료 정보를 캐러셀 방식으로 탐색하고 드래그/버튼 입력에 따라 전환 애니메이션을 제공하는 프리젠터입니다.
/// </summary>
public class CompanionCarouselPresenter : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("씬 참조")]
    [Tooltip("캐러셀이 표시될 영역(RectTransform). RectMask2D가 권장됩니다.")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("중앙에 표시될 동료 카드 프리팹.")]
    [SerializeField] private CompanionDetailView detailPrefab;
    [Tooltip("이전 동료로 이동하는 버튼.")]
    [SerializeField] private Button previousButton;
    [Tooltip("다음 동료로 이동하는 버튼.")]
    [SerializeField] private Button nextButton;

    [Header("애니메이션")]
    [Tooltip("슬라이드로 판정되기까지 필요한 최소 드래그 거리(픽셀).")]
    [SerializeField, Min(10f)] private float dragThreshold = 120f;
    [Tooltip("슬라이드 이동 거리를 강제로 지정합니다. 0이면 뷰포트 너비를 사용합니다.")]
    [SerializeField, Min(0f)] private float slideDistanceOverride = 0f;
    [Tooltip("슬라이드 애니메이션이 완료되는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.05f)] private float slideDuration = 0.28f;
    [Tooltip("전환 중 옆 카드가 흐려질 정도(0=완전투명, 1=불투명).")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.55f;
    [Tooltip("슬라이드 조건이 충족되지 않았을 때 제자리로 돌아오는 시간(초).")]
    [SerializeField, Min(0.05f)] private float snapBackDuration = 0.2f;
    [Tooltip("이동 보간에 사용할 커브입니다. 기본은 Ease In/Out입니다.")]
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public event Action<CompanionDefinition> SelectionChanged;

    private readonly List<CompanionDefinition> _companions = new();
    private CompanionDetailView _currentView;
    private CompanionDetailView _pendingView;
    private int _currentIndex;
    private bool _initialized;
    private bool _dragActive;
    private bool _transitionActive;
    private float _dragOffset;
    private int _preparedDirection;

    private void Awake()
    {
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(OnClickPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnClickNext);
        }
    }

    private void OnDestroy()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(OnClickPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnClickNext);
        }
    }

    /// <summary>
    /// 캐러셀을 초기화하고 초기 인덱스로 지정된 동료를 표시합니다.
    /// </summary>
    public void Initialize(IReadOnlyList<CompanionDefinition> companions, int initialIndex = 0)
    {
        CleanupViews();

        _companions.Clear();
        if (companions != null)
        {
            for (int i = 0; i < companions.Count; i++)
            {
                if (companions[i] != null)
                {
                    _companions.Add(companions[i]);
                }
            }
        }

        _currentIndex = _companions.Count > 0
            ? Mathf.Clamp(initialIndex, 0, _companions.Count - 1)
            : 0;

        CreateCurrentView();
        UpdateNavigationState();

        _initialized = true;
        NotifySelectionChanged();
    }

    /// <summary>
    /// 외부 버튼 연결용 API.
    /// </summary>
    public void Step(int direction)
    {
        if (direction == 0)
            return;

        if (!_initialized || _companions.Count <= 1)
            return;

        if (_transitionActive)
            return;

        _preparedDirection = direction > 0 ? 1 : -1;
        StartCoroutine(SlideRoutine(direction, 0f));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_initialized || _companions.Count <= 1)
            return;

        if (_transitionActive)
            return;

        _dragActive = true;
        _dragOffset = 0f;
        _preparedDirection = 0;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragActive || _transitionActive)
            return;

        _dragOffset += eventData.delta.x;

        if (_preparedDirection == 0 && Mathf.Abs(_dragOffset) > 0.01f)
        {
            _preparedDirection = _dragOffset > 0f ? -1 : 1;
            PreparePendingView(_preparedDirection);
        }

        UpdateDragVisuals(_dragOffset);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragActive || _transitionActive)
            return;

        _dragActive = false;

        if (Mathf.Abs(_dragOffset) >= dragThreshold)
        {
            int direction = _preparedDirection != 0
                ? _preparedDirection
                : (_dragOffset < 0f ? 1 : -1);

            StartCoroutine(SlideRoutine(direction, _dragOffset));
        }
        else
        {
            StartCoroutine(SnapBackRoutine(_dragOffset));
        }
    }

    public CompanionDefinition GetCurrentSelection()
    {
        if (_companions.Count == 0)
            return null;

        return _companions[_currentIndex];
    }

    private void OnClickPrevious() => Step(-1);
    private void OnClickNext() => Step(1);

    private void CreateCurrentView()
    {
        if (detailPrefab == null || viewport == null)
            return;

        if (_companions.Count == 0)
            return;

        _currentView = InstantiateDetailView();
        _currentView.SetData(_companions[_currentIndex]);
        _currentView.SetAnchoredPosition(Vector2.zero);
        _currentView.SetAlpha(1f);
    }

    private void PreparePendingView(int direction)
    {
        if (detailPrefab == null || viewport == null)
            return;

        int targetIndex = WrapIndex(_currentIndex + direction);
        if (targetIndex == _currentIndex)
            return;

        if (_pendingView != null)
        {
            if (WrapIndex(_currentIndex + direction) == WrapIndex(_currentIndex + _preparedDirection))
                return;

            Destroy(_pendingView.gameObject);
            _pendingView = null;
        }

        _pendingView = InstantiateDetailView();
        _pendingView.SetData(_companions[targetIndex]);

        float distance = GetSlideDistance();
        float startX = direction > 0 ? distance : -distance;
        _pendingView.SetAnchoredPosition(new Vector2(startX, 0f));
        _pendingView.SetAlpha(fadedAlpha);
    }

    private void UpdateDragVisuals(float offset)
    {
        if (_currentView == null)
            return;

        float distance = GetSlideDistance();
        _currentView.SetAnchoredPosition(new Vector2(offset, 0f));

        if (_pendingView != null && _preparedDirection != 0)
        {
            float targetBase = _preparedDirection > 0 ? distance : -distance;
            float pendingX = targetBase + offset;
            _pendingView.SetAnchoredPosition(new Vector2(pendingX, 0f));

            float progress = Mathf.Clamp01(Mathf.Abs(offset) / distance);
            float currentAlpha = Mathf.Lerp(1f, fadedAlpha, progress);
            float pendingAlpha = Mathf.Lerp(fadedAlpha, 1f, progress);
            _currentView.SetAlpha(currentAlpha);
            _pendingView.SetAlpha(pendingAlpha);
        }
    }

    private System.Collections.IEnumerator SlideRoutine(int direction, float initialOffset)
    {
        _transitionActive = true;
        PreparePendingView(direction);

        if (_preparedDirection == 0)
        {
            _preparedDirection = direction > 0 ? 1 : -1;
        }

        if (_pendingView == null)
        {
            yield return SnapBackRoutine(initialOffset);
            yield break;
        }

        float distance = GetSlideDistance();
        float start = Mathf.Clamp(initialOffset, -distance, distance);
        float end = direction > 0 ? -distance : distance;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = slideCurve != null ? slideCurve.Evaluate(t) : t;
            float offset = Mathf.Lerp(start, end, eased);
            UpdateDragVisuals(offset);
            yield return null;
        }

        FinalizeSlide(direction);
    }

    private System.Collections.IEnumerator SnapBackRoutine(float initialOffset)
    {
        _transitionActive = true;
        float distance = GetSlideDistance();
        float start = Mathf.Clamp(initialOffset, -distance, distance);
        float elapsed = 0f;

        while (elapsed < snapBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapBackDuration);
            float eased = slideCurve != null ? slideCurve.Evaluate(t) : t;
            float offset = Mathf.Lerp(start, 0f, eased);
            UpdateDragVisuals(offset);
            yield return null;
        }

        ResetDragVisuals();
    }

    private void FinalizeSlide(int direction)
    {
        _currentIndex = WrapIndex(_currentIndex + direction);

        if (_currentView != null)
        {
            Destroy(_currentView.gameObject);
        }

        _currentView = _pendingView;
        _pendingView = null;
        _currentView.SetAnchoredPosition(Vector2.zero);
        _currentView.SetAlpha(1f);

        ResetDragTracking();
        UpdateNavigationState();
        NotifySelectionChanged();
    }

    private void ResetDragVisuals()
    {
        if (_currentView != null)
        {
            _currentView.SetAnchoredPosition(Vector2.zero);
            _currentView.SetAlpha(1f);
        }

        if (_pendingView != null)
        {
            Destroy(_pendingView.gameObject);
            _pendingView = null;
        }

        ResetDragTracking();
    }

    private void ResetDragTracking()
    {
        _dragOffset = 0f;
        _preparedDirection = 0;
        _dragActive = false;
        _transitionActive = false;
    }

    private void CleanupViews()
    {
        if (_currentView != null)
        {
            Destroy(_currentView.gameObject);
            _currentView = null;
        }

        if (_pendingView != null)
        {
            Destroy(_pendingView.gameObject);
            _pendingView = null;
        }
    }

    private float GetSlideDistance()
    {
        if (slideDistanceOverride > 0f)
            return slideDistanceOverride;

        if (viewport != null)
        {
            var rect = viewport.rect;
            if (!Mathf.Approximately(rect.width, 0f))
                return rect.width;
        }

        return 800f;
    }

    private void UpdateNavigationState()
    {
        bool hasMultiple = _companions.Count > 1;
        if (previousButton != null)
        {
            previousButton.interactable = hasMultiple;
        }

        if (nextButton != null)
        {
            nextButton.interactable = hasMultiple;
        }
    }

    private void NotifySelectionChanged()
    {
        SelectionChanged?.Invoke(GetCurrentSelection());
    }

    private int WrapIndex(int index)
    {
        int count = _companions.Count;
        if (count == 0)
            return 0;

        int value = index % count;
        if (value < 0)
        {
            value += count;
        }
        return value;
    }

    private CompanionDetailView InstantiateDetailView()
    {
        var view = Instantiate(detailPrefab, viewport);
        AttachRelayIfNeeded(view);
        return view;
    }

    private void AttachRelayIfNeeded(CompanionDetailView view)
    {
        if (view == null)
            return;

        var relay = view.GetComponent<CompanionCarouselDragRelay>();
        if (relay == null)
        {
            relay = view.gameObject.AddComponent<CompanionCarouselDragRelay>();
        }
        relay.Initialize(this);
    }
}

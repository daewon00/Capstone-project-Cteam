using UnityEngine;

/// <summary>
/// Battle hand card tooltip presentation service. Shows the action description near the pressed card.
/// </summary>
public class CardTooltipService : MonoBehaviour, ICardTooltipService
{
    [SerializeField, Tooltip("Screen-space offset (in pixels) applied after projecting the world anchor.")]
    private Vector2 screenOffset = new Vector2(0f, 48f);
    [SerializeField, Tooltip("전용 카드 툴팁 프리젠터 참조(없으면 자동 검색/로드)")]
    private CardTooltipPresenter presenterOverride;
    [SerializeField, Tooltip("Resources.Load 경로. override나 씬에서 찾을 수 없을 때만 사용됩니다.")]
    private string presenterResourcePath = "UI/CardTooltipPresenter";
    [SerializeField, Tooltip("씬 내에서 CardTooltipPresenter를 자동으로 찾아 사용할지 여부")]
    private bool searchSceneForPresenter = true;

    private ICardTooltipSource _activeSource;
    private CardTooltipData _activeData;
    private bool _visible;
    private CardTooltipPresenter _presenter;

    private void Awake()
    {
        EnsurePresenter();
    }

    public void Show(ICardTooltipSource source)
    {
        if (source == null || !source.IsTooltipValid)
        {
            HideAll();
            return;
        }

        _presenter = EnsurePresenter();
        if (_presenter == null)
        {
            HideAll();
            return;
        }

        _activeSource = source;
        _activeData = source.GetTooltipData();
        _visible = true;
        UpdateTooltipPosition(forceRefresh: true);
    }

    public void Hide(ICardTooltipSource source)
    {
        if (_activeSource != null && source != null && !ReferenceEquals(source, _activeSource))
        {
            return;
        }

        HideAll();
    }

    public void HideAll()
    {
        _visible = false;
        _activeSource = null;
        _presenter?.HideImmediate();
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void LateUpdate()
    {
        if (!_visible || _activeSource == null)
            return;

        if (!_activeSource.IsTooltipValid)
        {
            HideAll();
            return;
        }

        UpdateTooltipPosition(forceRefresh: false);
    }

    private void UpdateTooltipPosition(bool forceRefresh)
    {
        if (_activeSource == null)
        {
            HideAll();
            return;
        }

        var cam = CameraController.instance != null && CameraController.instance.mainCamera != null
            ? CameraController.instance.mainCamera
            : Camera.main;
        if (cam == null)
        {
            HideAll();
            return;
        }

        Vector3 anchor = GetAnchorPosition();
        Vector3 screen;
        if (TryProjectFromCanvas(_activeSource, anchor, out var canvasScreen))
        {
            screen = canvasScreen;
        }
        else
        {
            screen = cam.WorldToScreenPoint(anchor);
        }
        if (screen.z <= 0f)
        {
            HideAll();
            return;
        }

        if (_presenter == null)
        {
            _presenter = EnsurePresenter();
            if (_presenter == null)
            {
                HideAll();
                return;
            }
        }

        var offset = ResolveScreenOffset(screen);

        if (forceRefresh)
        {
            _presenter.Show(_activeData, screen, offset);
        }
        else
        {
            _presenter.UpdatePosition(screen, offset);
        }
    }

    private Vector3 GetAnchorPosition()
    {
        if (_activeSource == null)
            return Vector3.zero;

        return _activeSource.GetTooltipAnchorWorldPos();
    }

    private Vector2 ResolveScreenOffset(Vector3 screenPoint)
    {
        Vector2 offset = screenOffset;
        if (_activeSource == null)
            return offset;

        float xNorm = Screen.width > 0 ? screenPoint.x / Screen.width : 0.5f;
        float yNorm = Screen.height > 0 ? screenPoint.y / Screen.height : 0.5f;
        bool useHand = _activeSource.ShouldUseHandOffset;
        float leftThreshold = useHand ? 0.18f : 0.25f;
        float rightThreshold = useHand ? 0.7f : 0.75f;
        float baseX = Mathf.Abs(offset.x) < 1f ? (useHand ? 140f : 140f) : Mathf.Abs(offset.x);
        if (xNorm < leftThreshold)
            offset.x = baseX;
        else if (xNorm > rightThreshold)
            offset.x = -baseX;

        if (!useHand)
        {
            float baseY = Mathf.Abs(offset.y) < 1f ? 90f : Mathf.Abs(offset.y);
            if (yNorm < 0.35f)
                offset.y = baseY;
            else if (yNorm > 0.8f)
                offset.y = -baseY;
        }

        return offset;
    }

    private CardTooltipPresenter EnsurePresenter()
    {
        if (_presenter != null)
        {
            // 씬 리로드 후에도 항상 현재 씬의 유효한 Canvas에 붙여둔다.
            AttachPresenterToCanvas(_presenter);
            return _presenter;
        }

        if (presenterOverride != null)
        {
            _presenter = presenterOverride;
            AttachPresenterToCanvas(_presenter);
            return _presenter;
        }

        if (searchSceneForPresenter)
        {
#if UNITY_EDITOR
            _presenter = FindFirstObjectByType<CardTooltipPresenter>(FindObjectsInactive.Include);
#else
            _presenter = FindFirstObjectByType<CardTooltipPresenter>();
#endif
            if (_presenter != null)
            {
                AttachPresenterToCanvas(_presenter);
                return _presenter;
            }
        }

        if (!string.IsNullOrEmpty(presenterResourcePath))
        {
            var prefab = Resources.Load<CardTooltipPresenter>(presenterResourcePath);
            if (prefab != null)
            {
                _presenter = Instantiate(prefab);
                AttachPresenterToCanvas(_presenter);
                return _presenter;
            }
            GameLog.Warn($"[CardTooltipService] Failed to load CardTooltipPresenter at Resources/{presenterResourcePath}");
        }

        GameLog.Warn("[CardTooltipService] CardTooltipPresenter not found. Card press tooltip will be disabled.");
        return null;
    }

    private void AttachPresenterToCanvas(CardTooltipPresenter presenter)
    {
        if (presenter == null)
            return;

        var canvas = ResolveCanvas();
        if (canvas != null)
        {
            presenter.AttachToCanvas(canvas);
        }
        else
        {
            presenter.EnsureParentCanvas();
        }
    }

    private Canvas ResolveCanvas()
    {
        Canvas canvas = null;
        if (UIController.instance != null)
        {
            canvas = UIController.instance.GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        canvas = FindFirstObjectByType<Canvas>();
        return canvas;
    }

    private bool TryProjectFromCanvas(ICardTooltipSource source, Vector3 worldPos, out Vector3 screenPos)
    {
        screenPos = default;
        if (source is not Component component)
            return false;

        var canvas = component.GetComponentInParent<Canvas>();
        if (canvas == null)
            return false;

        Camera uiCamera = null;
        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                uiCamera = null;
                break;
            case RenderMode.ScreenSpaceCamera:
                uiCamera = canvas.worldCamera;
                break;
            case RenderMode.WorldSpace:
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                break;
        }

        var screen2D = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPos);
        screenPos = new Vector3(screen2D.x, screen2D.y, 1f);
        return true;
    }
}

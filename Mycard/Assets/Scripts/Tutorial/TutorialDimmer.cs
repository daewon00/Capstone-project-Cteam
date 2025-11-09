using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 단계에서 화면을 어둡게 처리하고 여러 강조 영역만 남겨두는 뷰 컨트롤러입니다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(CanvasGroup))]
public sealed partial class TutorialDimmer : MonoBehaviour
{
    [SerializeField] private RectTransform highlightFrame;
    [SerializeField] private float highlightPadding = 12f;
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Material overrideMaterial;

    [SerializeField] private bool enableDebugLogs = true;
    private CanvasGroup _group;
    private CanvasRenderer _renderer;
    private RectTransform _rootRect;
    private Material _runtimeMaterial;
    private Mesh _mesh;

    private readonly List<Rect> _cutouts = new();
    private readonly List<float> _gridX = new();
    private readonly List<float> _gridY = new();
    private readonly List<Vector3> _vertices = new();
    private readonly List<Color32> _colors = new();
    private readonly List<int> _indices = new();

    private Rect _lastRootRect;

    private const float Epsilon = 0.01f;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _renderer = GetComponent<CanvasRenderer>();
        _rootRect = transform as RectTransform;

        _mesh = new Mesh { name = "TutorialDimmerMesh" };
        _mesh.MarkDynamic();

        if (overrideMaterial != null)
        {
            _runtimeMaterial = Instantiate(overrideMaterial);
        }
        else
        {
            _runtimeMaterial = new Material(Graphic.defaultGraphicMaterial)
            {
                name = "TutorialDimmerMaterial"
            };
        }

        _renderer.SetMaterial(_runtimeMaterial, null);
        Hide();
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            Destroy(_mesh);
            _mesh = null;
        }

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }

    public void Apply(TutorialStepConfig config, RectTransform mainHighlight, IReadOnlyList<RectTransform> secondaryHighlights)
    {
        if (config == null || config.InteractionGate == TutorialInteractionGate.None)
        {
            Hide();
            return;
        }

        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;

        var rootRect = _rootRect.rect;
        CollectCutouts(rootRect, mainHighlight, secondaryHighlights);
        D($"Apply: root=({rootRect.xMin:F1},{rootRect.yMin:F1})-({rootRect.xMax:F1},{rootRect.yMax:F1}) cuts={_cutouts.Count} main={(mainHighlight!=null?mainHighlight.name:"<null>")}");

        if (_cutouts.Count == 0)
        {
            if (config.HighlightOptional)
            {
                ClearMesh();
                return;
            }

            GenerateSolidCover(rootRect);
            D("Apply: solid cover (no cutouts)");
            return;
        }

        GenerateMesh(rootRect);
        if (_cutouts.Count > 0)
        {
            var r = _cutouts[0];
            D($"Apply: mainCut=({r.xMin:F1},{r.yMin:F1}) size=({r.width:F1},{r.height:F1})");
        }
    }

    private void Hide()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        ClearMesh();
        if (highlightFrame != null)
        {
            highlightFrame.gameObject.SetActive(false);
        }
    }

    private void ClearMesh()
    {
        _renderer.SetMesh(null);
    }

    private void CollectCutouts(Rect rootRect, RectTransform mainHighlight, IReadOnlyList<RectTransform> secondary)
    {
        _cutouts.Clear();

        if (mainHighlight != null && TryGetHighlightRect(mainHighlight, out var mainRect))
        {
            InflateRect(ref mainRect, highlightPadding);
            mainRect = ClampToRoot(mainRect, rootRect);
            if (mainRect.width > Epsilon && mainRect.height > Epsilon)
            {
                _cutouts.Add(mainRect);
                UpdateHighlightFrame(mainRect, rootRect);
                D($"CollectCutouts: main='{mainHighlight.name}' rect=({mainRect.xMin:F1},{mainRect.yMin:F1}) size=({mainRect.width:F1},{mainRect.height:F1})");
            }
            else if (highlightFrame != null)
            {
                highlightFrame.gameObject.SetActive(false);
            }
        }
        else if (highlightFrame != null)
        {
            highlightFrame.gameObject.SetActive(false);
        }

        if (secondary == null)
        {
            return;
        }

        foreach (var rectTransform in secondary)
        {
            if (rectTransform == null) continue;
            if (!TryGetHighlightRect(rectTransform, out var rect)) continue;
            InflateRect(ref rect, highlightPadding);
            rect = ClampToRoot(rect, rootRect);
            if (rect.width <= Epsilon || rect.height <= Epsilon) continue;
            _cutouts.Add(rect);
            D($"CollectCutouts: secondary='{rectTransform.name}' size=({rect.width:F1},{rect.height:F1})");
        }
    }

    private void UpdateHighlightFrame(Rect rect, Rect rootRect)
    {
        if (highlightFrame == null) return;
        highlightFrame.gameObject.SetActive(true);

        float width = rootRect.width;
        float height = rootRect.height;

        float leftNorm = Mathf.Clamp01((rect.xMin - rootRect.xMin) / width);
        float rightNorm = Mathf.Clamp01((rect.xMax - rootRect.xMin) / width);
        float bottomNorm = Mathf.Clamp01((rect.yMin - rootRect.yMin) / height);
        float topNorm = Mathf.Clamp01((rect.yMax - rootRect.yMin) / height);

        highlightFrame.anchorMin = new Vector2(leftNorm, bottomNorm);
        highlightFrame.anchorMax = new Vector2(rightNorm, topNorm);
        highlightFrame.offsetMin = Vector2.zero;
        highlightFrame.offsetMax = Vector2.zero;
    }

    private void GenerateSolidCover(Rect rootRect)
    {
        _vertices.Clear();
        _indices.Clear();
        _colors.Clear();
        AddQuad(rootRect);
        CommitMesh();
    }

    private void GenerateMesh(Rect rootRect)
    {
        BuildGrid(rootRect);

        _vertices.Clear();
        _indices.Clear();
        _colors.Clear();

        for (int xi = 0; xi < _gridX.Count - 1; xi++)
        {
            for (int yi = 0; yi < _gridY.Count - 1; yi++)
            {
                var cell = Rect.MinMaxRect(_gridX[xi], _gridY[yi], _gridX[xi + 1], _gridY[yi + 1]);
                if (cell.width <= Epsilon || cell.height <= Epsilon) continue;

                var center = cell.center;
                bool inside = false;
                for (int i = 0; i < _cutouts.Count; i++)
                {
                    if (_cutouts[i].Contains(center))
                    {
                        inside = true;
                        break;
                    }
                }

                if (!inside)
                {
                    AddQuad(cell);
                }
            }
        }

        CommitMesh();
    }

    private void BuildGrid(Rect rootRect)
    {
        _gridX.Clear();
        _gridY.Clear();
        _gridX.Add(rootRect.xMin);
        _gridX.Add(rootRect.xMax);
        _gridY.Add(rootRect.yMin);
        _gridY.Add(rootRect.yMax);

        foreach (var rect in _cutouts)
        {
            _gridX.Add(Mathf.Clamp(rect.xMin, rootRect.xMin, rootRect.xMax));
            _gridX.Add(Mathf.Clamp(rect.xMax, rootRect.xMin, rootRect.xMax));
            _gridY.Add(Mathf.Clamp(rect.yMin, rootRect.yMin, rootRect.yMax));
            _gridY.Add(Mathf.Clamp(rect.yMax, rootRect.yMin, rootRect.yMax));
        }

        _gridX.Sort();
        _gridY.Sort();
        RemoveDuplicates(_gridX);
        RemoveDuplicates(_gridY);
    }

    private static void RemoveDuplicates(List<float> values)
    {
        for (int i = values.Count - 2; i >= 0; i--)
        {
            if (Mathf.Abs(values[i] - values[i + 1]) < Epsilon)
            {
                values.RemoveAt(i);
            }
        }
    }

    private void AddQuad(Rect rect)
    {
        var idx = _vertices.Count;
        _vertices.Add(new Vector3(rect.xMin, rect.yMin, 0f));
        _vertices.Add(new Vector3(rect.xMin, rect.yMax, 0f));
        _vertices.Add(new Vector3(rect.xMax, rect.yMax, 0f));
        _vertices.Add(new Vector3(rect.xMax, rect.yMin, 0f));

        var color32 = (Color32)overlayColor;
        _colors.Add(color32);
        _colors.Add(color32);
        _colors.Add(color32);
        _colors.Add(color32);

        _indices.Add(idx);
        _indices.Add(idx + 1);
        _indices.Add(idx + 2);
        _indices.Add(idx);
        _indices.Add(idx + 2);
        _indices.Add(idx + 3);
    }

    private void CommitMesh()
    {
        if (_vertices.Count == 0)
        {
            _renderer.SetMesh(null);
            return;
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_indices, 0);
        _renderer.SetMesh(_mesh);
        _renderer.SetColor(Color.white);
    }

    private static Rect ClampToRoot(Rect rect, Rect root)
    {
        float xMin = Mathf.Max(rect.xMin, root.xMin);
        float xMax = Mathf.Min(rect.xMax, root.xMax);
        float yMin = Mathf.Max(rect.yMin, root.yMin);
        float yMax = Mathf.Min(rect.yMax, root.yMax);
        if (xMax < xMin) xMax = xMin;
        if (yMax < yMin) yMax = yMin;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private bool TryGetHighlightRect(RectTransform target, out Rect rect)
    {
        rect = default;
        if (_rootRect == null || target == null)
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);
        Vector2[] localCorners = new Vector2[4];

        // 좌표 투영 기준 카메라 선택:
        // - 오버레이가 Overlay 모드이면 타겟이 속한 Canvas의 worldCamera를 우선 사용
        // - 오버레이가 Camera/WorldSpace 모드이면 오버레이 Canvas의 worldCamera 사용
        // - 폴백: Camera.main
        var overlayCanvas = GetComponentInParent<Canvas>();
        var targetCanvas = target.GetComponentInParent<Canvas>();
        Camera camera = null;
        if (overlayCanvas != null && overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = targetCanvas.worldCamera;
            }
        }
        else if (overlayCanvas != null)
        {
            camera = overlayCanvas.worldCamera;
        }
        if (camera == null)
        {
            camera = Camera.main;
        }

        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]), camera, out localCorners[i]);
        }

        Vector2 min = localCorners[0];
        Vector2 max = localCorners[0];
        for (int i = 1; i < localCorners.Length; i++)
        {
            min = Vector2.Min(min, localCorners[i]);
            max = Vector2.Max(max, localCorners[i]);
        }

        rect = new Rect(min, max - min);
        D($"TryGetHighlightRect: target='{target.name}' cam={(camera!=null?camera.name:"<null>")} local=({rect.xMin:F1},{rect.yMin:F1}) size=({rect.width:F1},{rect.height:F1})");
        return true;
    }

    private static void InflateRect(ref Rect rect, float amount)
    {
        rect.xMin -= amount;
        rect.xMax += amount;
        rect.yMin -= amount;
        rect.yMax += amount;
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
partial class TutorialDimmer
{
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void D(string msg)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"[TutorialDimmer] {msg}", this);
    }
}
#endif

#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
partial class TutorialDimmer
{
    // 릴리즈 빌드용 no-op 디버그 함수 스텁
    private void D(string msg) { }
}
#endif

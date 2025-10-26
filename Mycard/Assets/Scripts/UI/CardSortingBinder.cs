using UnityEngine;
using TMPro;

/// <summary>
/// 한 카드(GameObject) 하위의 Canvas/SpriteRenderer 들에 동일한 sortingOrder를 적용해
/// 카드 단위로 정렬 우선순위를 보장합니다. 드래그 중 일시 승격도 지원합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardSortingBinder : MonoBehaviour
{
    [Tooltip("World Space Canvas에 대해 overrideSorting을 강제할지 여부")] public bool overrideCanvasSorting = true;

    private Canvas[] _canvases;
    private SpriteRenderer[] _spriteRenderers;
    private int _currentOrder;
    private bool _hasSavedOrder;
    private int _savedOrder;

    private void Awake()
    {
        _canvases = GetComponentsInChildren<Canvas>(true);
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void ApplyOrder(int order)
    {
        _currentOrder = order;
        if (_canvases != null)
        {
            for (int i = 0; i < _canvases.Length; i++)
            {
                var c = _canvases[i];
                if (c == null) continue;
                if (overrideCanvasSorting) c.overrideSorting = true;
                c.sortingOrder = order;
            }
        }
        if (_spriteRenderers != null)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                var sr = _spriteRenderers[i];
                if (sr == null) continue;
                sr.sortingOrder = order;
            }
        }
    }

    public void ElevateForDrag(int topOrder)
    {
        if (!_hasSavedOrder)
        {
            _savedOrder = _currentOrder;
            _hasSavedOrder = true;
        }
        ApplyOrder(topOrder);
    }

    public void RestoreAfterDrag()
    {
        if (_hasSavedOrder)
        {
            ApplyOrder(_savedOrder);
            _hasSavedOrder = false;
        }
    }
}


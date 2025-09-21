using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScrollRect의 콘텐츠 스크롤에 맞춰 RawImage의 UV를 반복시키는 배경 타일러입니다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MapBackgroundTiler : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] [Tooltip("스크롤 위치를 UV 오프셋으로 변환할 배율값입니다.")]
    private float uvScrollScale = 0.001f;

    private RawImage _image;
    private Rect _baseRect;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        _baseRect = _image.uvRect;

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
            OnScrollChanged(scrollRect.normalizedPosition);
        }
    }

    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        }
    }

    private void OnScrollChanged(Vector2 _)
    {
        if (_image == null || scrollRect == null) return;

        float offsetY = scrollRect.content != null ? scrollRect.content.anchoredPosition.y * uvScrollScale : 0f;
        offsetY -= Mathf.Floor(offsetY);

        var rect = _baseRect;
        rect.y = offsetY;
        _image.uvRect = rect;
    }
}

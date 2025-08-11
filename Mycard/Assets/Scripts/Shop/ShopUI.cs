using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup panel;     // ShopUI 루트의 CanvasGroup
    [SerializeField] private Button dimmerButton;   // Dimmer(Button)
    [SerializeField] private Button closeButton;    // CloseButton(Button)

    void Reset()
    {
        // 에디터에서 컴포넌트 자동 할당 시도
        panel = GetComponent<CanvasGroup>();
        if (panel == null) panel = gameObject.AddComponent<CanvasGroup>();
        if (dimmerButton == null) dimmerButton = GetComponentInChildren<Button>(true);
    }

    void Awake()
    {
        // 버튼 리스너
        if (dimmerButton != null) dimmerButton.onClick.AddListener(Close);
        if (closeButton   != null) closeButton.onClick.AddListener(Close);

        HideImmediate(); // 시작 시 닫힌 상태
    }

    public void Open()
    {
        // 활성화 + 입력 허용
        gameObject.SetActive(true);
        panel.alpha = 1f;
        panel.interactable = true;
        panel.blocksRaycasts = true;
    }

    public void Close()
    {
        HideImmediate();
    }

    private void HideImmediate()
    {
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
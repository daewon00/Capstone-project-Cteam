using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class ShopSlotView : MonoBehaviour {
    public Button button;
    public Image icon;
    public TMP_Text titleText;
    public TMP_Text detailText;
    public GameObject soldOutOverlay;

    private string _logName;

    public void Bind(ShopSlotVM vm, System.Action onClick = null) {
        _logName = vm.title;
        if (titleText)  titleText.text  = vm.title ?? "";
        if (detailText) detailText.text = vm.detail ?? "";
        if (icon && vm.icon) icon.sprite = vm.icon;

        if (button == null) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => {
            if (vm.soldOut) return;
            Debug.Log($"[M2] 슬롯 클릭: {_logName}");
            onClick?.Invoke();
        });
        if (soldOutOverlay) soldOutOverlay.SetActive(vm.soldOut);
        button.interactable = !vm.soldOut;
    }

    private void Reset()
    {
        if (!button) button = GetComponent<Button>();
        if (!icon) icon = transform.Find("Icon")?.GetComponent<Image>();
        if (!titleText) titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!detailText) detailText = transform.Find("Detail")?.GetComponent<TMP_Text>();
        // soldOutOverlay는 프리팹에 "SoldOutOverlay" 오브젝트 하나 만들고 드래그로 연결
    }
}

[System.Serializable]
public struct ShopSlotVM {
    public string title;   // 예: "Strike"
    public string detail;  // 예: "Card" / "Relic" / "Consumable"
    public Sprite icon;    // (선택) 아이콘 없으면 null
    public bool soldOut;   // 판매 완료 여부
}
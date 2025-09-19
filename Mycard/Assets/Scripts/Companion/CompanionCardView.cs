using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 동료 선택 카드 UI를 구성하고 클릭 시 콜백을 호출합니다.
/// </summary>
public class CompanionCardView : MonoBehaviour
{
    public Image Portrait;
    public TMP_Text NameText;
    public TMP_Text DescText;
    public Button SelectButton;

    private CompanionDefinition _data;
    private System.Action<CompanionDefinition> _onSelect;

    /// <summary>
    /// 카드 UI를 전달된 동료 데이터로 갱신하고 선택 콜백을 설정합니다.
    /// </summary>
    public void Bind(CompanionDefinition data, System.Action<CompanionDefinition> onSelect)
    {
        _data = data; _onSelect = onSelect;
        if (Portrait) Portrait.sprite = data.Portrait;
        if (NameText) NameText.text = data.DisplayName;
        if (DescText) DescText.text = data.Description;
        if (SelectButton)
        {
            SelectButton.onClick.RemoveAllListeners();
            SelectButton.onClick.AddListener(() => _onSelect?.Invoke(_data));
        }
    }
}

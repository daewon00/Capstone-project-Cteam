using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionCardView : MonoBehaviour
{
    public Image Portrait;
    public TMP_Text NameText;
    public TMP_Text DescText;
    public Button SelectButton;

    private CompanionDefinition _data;
    private System.Action<CompanionDefinition> _onSelect;

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
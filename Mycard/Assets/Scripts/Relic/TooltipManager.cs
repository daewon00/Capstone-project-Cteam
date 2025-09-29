using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private TooltipUI tooltipUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!tooltipUI)
        {
            tooltipUI = GetComponentInChildren<TooltipUI>(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowTooltip(string title, string description)
    {
        
        if (!tooltipUI)
        {
            return;
        }

        tooltipUI.Show(title, description);
    }

    public void ShowRelicTooltip(RelicData data)
    {
        if (!data)
        {
            HideTooltip();
            return;
        }

        ShowTooltip(data.displayName, data.description);
    }

    public void HideTooltip()
    {
        
        tooltipUI?.Hide();
    }
}

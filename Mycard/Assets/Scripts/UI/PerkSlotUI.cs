using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 특전 슬롯 하나를 표시하고 레벨 조정 버튼을 처리하는 UI 컴포넌트입니다.
/// </summary>
public class PerkSlotUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private TextMeshProUGUI perkNameText;
    [SerializeField] private TextMeshProUGUI perkDescriptionText;
    [SerializeField] private TextMeshProUGUI perkLevelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button buyButton; // 사용하지 않을 수 있음(이전 방식 호환)
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private TextMeshProUGUI nextEffectText; // 선택: 다음 레벨 증가분 미리보기

    private PerkDefinition _perkDef;
    private int _currentLevel;   // DB의 현재 레벨(참고)
    private int _stagedLevel;    // 장바구니(표시/조정 대상)
    private int _availablePointsAfterStaging; // 남은 포인트(추가 1레벨 가능 판단)
    private Action<string, int> _onAdjustRequested; // (perkId, delta)

    /// <summary>
    /// 특전 정의와 현재/스테이징 레벨을 바인딩하고 버튼 콜백을 설정합니다.
    /// </summary>
    public void Init(
        PerkDefinition perkDef,
        int currentLevel,
        int stagedLevel,
        int availablePointsAfterStaging,
        System.Action<string, int> onAdjustRequested)
    {
        _perkDef = perkDef;
        _currentLevel = Mathf.Max(0, currentLevel);
        _stagedLevel = Mathf.Max(0, stagedLevel);
        _availablePointsAfterStaging = Mathf.Max(0, availablePointsAfterStaging);
        _onAdjustRequested = onAdjustRequested;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (plusButton == null || minusButton == null)
        {
            GameLog.Warn($"[PerkSlotUI] plus/minus Button not bound on '{gameObject.name}' (perkId={_perkDef?.Id ?? "<null>"}). Please assign in the prefab.");
        }
        if (_onAdjustRequested == null)
        {
            GameLog.Warn($"[PerkSlotUI] OnAdjustRequested callback is null on '{gameObject.name}' (perkId={_perkDef?.Id ?? "<null>"}).");
        }
#endif

        // 구버튼 제거(있다면 비활성)
        if (buyButton != null) buyButton.gameObject.SetActive(false);

        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(() => _onAdjustRequested?.Invoke(_perkDef.Id, +1));
        }
        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => _onAdjustRequested?.Invoke(_perkDef.Id, -1));
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_perkDef == null) return;

        if (perkNameText) perkNameText.text = _perkDef.DisplayName;
        if (perkDescriptionText) perkDescriptionText.text = _perkDef.Description;
        if (perkLevelText) perkLevelText.text = $"Lv. {_stagedLevel} / {_perkDef.MaxLevel}";

        bool atMax = _stagedLevel >= _perkDef.MaxLevel;
        bool canIncrease = !atMax && _availablePointsAfterStaging >= Mathf.Max(0, _perkDef.Cost);
        bool canDecrease = _stagedLevel > 0;

        if (costText)
        {
            if (atMax)
            {
                costText.text = "MAX";
            }
            else
            {
                costText.text = $"{_perkDef.Cost} P"; // 단위 비용
                // 포인트 부족 시 컬러 피드백(옵션)
                // costText.color = hasPoints ? Color.white : new Color(1f, 0.4f, 0.4f);
            }
        }

        if (plusButton) plusButton.interactable = canIncrease;
        if (minusButton) minusButton.interactable = canDecrease;

        // 다음 레벨 효과 미리보기(선택)
        if (nextEffectText)
        {
            if (atMax)
            {
                nextEffectText.text = "";
            }
            else
            {
                string delta = _perkDef.Kind == ValueKind.Flat
                    ? $"+{_perkDef.PerLevelValue:0.#}"
                    : $"+{_perkDef.PerLevelValue * 100f:0.#}%";
                nextEffectText.text = $"Next: {delta} {_perkDef.EffectKey}";
            }
        }
    }

    // 상위에서 포인트만 갱신하고 싶을 때 사용할 수 있는 보조 메서드(선택)
    /// <summary>
    /// 외부에서 스테이징 값을 갱신했을 때 UI를 새로고침합니다.
    /// </summary>
    public void SetDisplayAndRefresh(int stagedLevel, int pointsAfterStaging)
    {
        _stagedLevel = Mathf.Max(0, stagedLevel);
        _availablePointsAfterStaging = Mathf.Max(0, pointsAfterStaging);
        UpdateUI();
    }
}

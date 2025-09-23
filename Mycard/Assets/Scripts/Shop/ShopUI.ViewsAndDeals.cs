using System;
using UnityEngine;
using Random = UnityEngine.Random;

public partial class ShopUI : MonoBehaviour
{

    // ==========================================================
    // 9. 가격 및 특가 계산 (Business Logic)
    // - 아이템의 기본 가격을 계산하고, 특가를 적용하여 최종 가격을 결정하는 '규칙'입니다.
    // - 이 기능들은 'ShopService'가 책임져야 할 핵심 경제 로직입니다.
    // ==========================================================

    // 아이템 종류와 이름에 따라 기본 가격을 계산합니다.
    private int BasePriceOf(string detail, string title)
    {
        int baseVal = detail == "Card" ? 50 :
                      detail == "Relic" ? 120 :
                      detail == "Consumable" ? 40 : 50;
        baseVal += Mathf.Clamp((title?.Length ?? 0) * 2, 0, 30);
        return baseVal;
    }

    // 특가 여부에 따라 최종 가격을 계산합니다.
    private int FinalPrice(in ShopSlotVM v)
    {
        return v.isDeal
            ? Mathf.Max(1, Mathf.CeilToInt(v.price * (1f - dealDiscount)))
            : v.price;
    }

    // 현재 진열된 상품 목록에 랜덤으로 특가를 적용합니다.
    private void ApplyDeals()
    {
        for (int i = 0; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            var vm = _dummy[i];
            vm.isDeal = false;
            _dummy[i] = vm;
        }

        var picked = new System.Collections.Generic.List<int>(_dummy.Count);
        for (int i = 0; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            if (Random.value < dealChance) picked.Add(i);
        }

        while (picked.Count > Mathf.Max(0, maxDeals))
            picked.RemoveAt(Random.Range(0, picked.Count));

        foreach (var idx in picked)
        {
            var vm = _dummy[idx];
            vm.isDeal = true;
            _dummy[idx] = vm;
        }
    }

    // ==========================================================
    // 10. 화면 갱신 (View)
    // - 결정된 데이터(_dummy)를 바탕으로 실제 UI 요소들을 만들거나 업데이트합니다.
    // - 이 기능들은 '화면을 그리는' 순수한 View의 역할이므로, ShopUI에 남아있는 것이 적절합니다.
    // ==========================================================

    // 아이템 목록 전체를 처음부터 다시 그립니다.
    private void RebuildGrid()
    {
        foreach (Transform c in gridParent) Destroy(c.gameObject);
        _views.Clear();

        for (int i = 0; i < _dummy.Count; i++)
        {
            int slotIndex = i;
            var view = Instantiate(slotPrefab, gridParent);
            view.SetDealDiscount(dealDiscount);

            bool canBuy = !_dummy[slotIndex].soldOut && (Gold >= FinalPrice(_dummy[slotIndex]));
            view.Bind(_dummy[slotIndex], () => TryBuy(slotIndex), canBuy);

            _views.Add(view);
        }
    }

    // 이미 생성된 아이템 목록의 내용만 새로고침합니다. (구매 가능 여부 등)
    private void RefreshViews()
    {
        for (int i = 0; i < _dummy.Count && i < _views.Count; i++)
        {
            int slotIndex = i;
            _views[i].SetDealDiscount(dealDiscount);
            bool canBuy = !_dummy[slotIndex].soldOut && (Gold >= FinalPrice(_dummy[slotIndex]));
            _views[i].Bind(_dummy[slotIndex], () => TryBuy(slotIndex), canBuy);
        }
    }

    // 상단 골드와 리롤 버튼의 상태를 새로고침합니다.
    private void RefreshTopbar()
    {
        int cost = CurrentRerollCost();
        rerollPriceText?.SetText("{0:#,0}", cost);

        // Gold 속성을 통해 현재 골드 값을 가져와 goldText에 표시합니다.
        goldText?.SetText("{0:#,0}", Gold);
        
        if (rerollButton)
        {
            bool canAfford = Gold >= cost;
            // 쿨다운 중엔 강제로 비활성
            rerollButton.interactable = !_isRerollCooling && canAfford;
        }
    }

    // ==========================================================
    // 11. 구매/리롤 최종 실행 (Controller)
    // - 실제 구매, 리롤 비용 계산 등 플레이어의 행동을 최종적으로 처리하는 기능입니다.
    // - 이 기능들은 현재 View(ShopUI)가 Controller 역할까지 겸하고 있다는 것을 보여줍니다.
    // - 'ShopPresenter'나 'ShopOverlayController'가 이 책임을 가져가야 합니다.
    // ==========================================================

    // 현재 리롤 비용을 계산합니다. (규칙이므로 Service로 이동 대상)
    private int CurrentRerollCost()
    {
        return Mathf.RoundToInt(baseReroll * Mathf.Pow(rerollGrowth, _rerollCount));
    }

    // 구매 버튼을 눌렀을 때 실행되는 메인 함수입니다. (골드 확인, 구매 처리 등)
    private void TryBuy(int index)
    {
        if (index < 0 || index >= _dummy.Count) return;
        var vm = _dummy[index];
        if (vm.soldOut) return;

        int cost = FinalPrice(vm);
        if (Gold < cost) return;

        bool isCardSlot = vm.cardData != null || string.Equals(vm.detail, "Card", StringComparison.OrdinalIgnoreCase);
        string cardId = null;
        if (isCardSlot && !TryResolveCardId(in vm, out cardId))
        {
            Debug.LogWarning($"[ShopUI] 카드 ID 확인 실패: slot={index}, title={vm.title}", this);
            return;
        }

        if (!TrySpendGold(cost))
        {
            Debug.LogWarning($"[ShopUI] 골드 차감 실패: cost={cost}", this);
            RefreshTopbar();
            return;
        }

        if (isCardSlot)
        {
            if (TryAddCardToDeck == null)
            {
                Debug.LogError($"[ShopUI] 덱 서비스가 연결되지 않아 카드 구매를 취소합니다. cardId={cardId ?? "<null>"}", this);
                RefundGold(cost);
                RefreshTopbar();
                return;
            }

            if (string.IsNullOrEmpty(cardId) || !TryAddCardToDeck(cardId))
            {
                Debug.LogError($"[ShopUI] 카드 추가 실패: cardId={cardId ?? "<null>"}, slot={index}", this);
                RefundGold(cost);
                RefreshTopbar();
                return;
            }
        }

        vm.soldOut = true;
        _dummy[index] = vm;

        OnSessionChanged?.Invoke();
        RefreshViews();
        RefreshTopbar();
        
    }


}

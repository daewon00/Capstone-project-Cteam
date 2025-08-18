using UnityEngine;

public partial class ShopUI : MonoBehaviour
{
    private int BasePriceOf(string detail, string title)
    {
        int baseVal = detail == "Card" ? 50 :
                      detail == "Relic" ? 120 :
                      detail == "Consumable" ? 40 : 50;
        baseVal += Mathf.Clamp((title?.Length ?? 0) * 2, 0, 30);
        return baseVal;
    }

    private int FinalPrice(in ShopSlotVM v)
    {
        return v.isDeal
            ? Mathf.Max(1, Mathf.CeilToInt(v.price * (1f - dealDiscount)))
            : v.price;
    }

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

    private void RefreshTopbar()
    {
        int cost = CurrentRerollCost();
        rerollPriceText?.SetText("{0:#,0}", cost);
        
        if (rerollButton)
        {
            bool canAfford = Gold >= cost;
            // 쿨다운 중엔 강제로 비활성
            rerollButton.interactable = !_isRerollCooling && canAfford;
        }
    }

    private int CurrentRerollCost()
    {
        return Mathf.RoundToInt(baseReroll * Mathf.Pow(rerollGrowth, _rerollCount));
    }

    private void TryBuy(int index)
    {
        if (index < 0 || index >= _dummy.Count) return;
        var vm = _dummy[index];
        if (vm.soldOut) return;

        int cost = FinalPrice(vm);
        if (Gold < cost) return;

        Gold -= cost;

        vm.soldOut = true;
        _dummy[index] = vm;

        RefreshViews();
        RefreshTopbar();
    }
}

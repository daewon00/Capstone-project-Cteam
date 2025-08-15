using System.Collections.Generic;
using UnityEngine;

public partial class ShopUI : MonoBehaviour
{
    private void LoadAllCardData()
    {
        if (cardPool == null) cardPool = new List<CardScriptableObject>();

        var loadedCards = Resources.LoadAll<CardScriptableObject>(CardsPath);
        if (loadedCards != null && loadedCards.Length > 0)
        {
            var set = new HashSet<CardScriptableObject>(cardPool);
            foreach (var c in loadedCards)
                if (c != null && !set.Contains(c))
                    cardPool.Add(c);
        }
        Debug.Log($"[ShopUI] cardPool merged count = {cardPool.Count}");
        if (cardPool.Count == 0)
            Debug.LogWarning($"[ShopUI] No cards found in inspector or Resources/{CardsPath}");
    }

    private void BuildDummySlots()
    {
        _dummy = new List<ShopSlotVM>(6)
        {
            new ShopSlotVM{ title="Strike",       detail="Card" },
            new ShopSlotVM{ title="Defend",       detail="Card" },
            new ShopSlotVM{ title="Fireball",     detail="Card" },
            new ShopSlotVM{ title="Happy Flower", detail="Relic" },
            new ShopSlotVM{ title="Anchor",       detail="Relic" },
            new ShopSlotVM{ title="Block Potion", detail="Consumable" },
        };

        for (int i = 0; i < _dummy.Count; i++)
        {
            var vm = _dummy[i];
            vm.price = BasePriceOf(vm.detail, vm.title);
            _dummy[i] = vm;
        }
    }

    private ShopSlotVM ToVM(CardScriptableObject so)
    {
        var icon = so.characterSprite != null ? so.characterSprite : so.bgSprite;
        return new ShopSlotVM
        {
            title   = so.cardName,
            detail  = "Card",
            icon    = icon,
            price   = BasePriceOf("Card", so.cardName),
            soldOut = false,
            isDeal  = false,
        };
    }

    private void BuildCardSlotsInitial()
    {
        if (cardPool == null || cardPool.Count == 0) return;

        var exclude = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var pick = DrawUniqueCard(exclude);
            if (pick == null) { _cardSources[i] = null; continue; }

            exclude.Add(pick.cardName);     // 이름 기반(당신 선택 유지)
            _cardSources[i] = pick;
            _dummy[i] = ToVM(pick);
        }
    }

    private void RerollCardSlots()
    {
        var exclude = new HashSet<string>();
        for (int i = 0; i < 3; i++)
            if (_cardSources[i] != null)
                exclude.Add(_cardSources[i].cardName);

        for (int slot = 0; slot < 3; slot++)
        {
            if (_dummy[slot].soldOut) continue;

            var pick = DrawUniqueCard(exclude);
            if (pick == null) continue;

            exclude.Add(pick.cardName);
            _cardSources[slot] = pick;
            _dummy[slot] = ToVM(pick);
        }
    }

    private CardScriptableObject DrawUniqueCard(HashSet<string> exclude)
    {
        if (cardPool == null || cardPool.Count == 0) return null;

        var candidates = new List<CardScriptableObject>(cardPool.Count);
        foreach (var c in cardPool)
        {
            if (c == null) continue;
            if (exclude != null && exclude.Contains(c.cardName)) continue;
            candidates.Add(c);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    // 유물/소모품 문자열 풀용
    private string DrawUnique(string[] pool, HashSet<string> exclude)
    {
        var candidates = new List<string>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
        {
            string p = pool[i];
            if (!exclude.Contains(p))
                candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }

        private void OnReroll()
    {
        int cost = CurrentRerollCost();
        if (Gold < cost) return;

        if (rerollButton) rerollButton.interactable = false;

        Gold -= cost;
        _rerollCount++;

        RerollCardSlots(); // 카드 3칸 교체

        // (유물/소모품 교체 로직)
        var exclude = new HashSet<string>();
        for (int i = 0; i < _dummy.Count; i++)
            if (!string.IsNullOrEmpty(_dummy[i].title))
                exclude.Add(_dummy[i].title);

        for (int i = 3; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            string[] pool = _dummy[i].detail == "Relic" ? RelicsPool : ConsumablesPool;
            string newId = DrawUnique(pool, exclude);
            if (string.IsNullOrEmpty(newId)) continue;

            exclude.Add(newId);
            var vm = _dummy[i];
            vm.title = newId;
            vm.icon = null;
            vm.price = BasePriceOf(vm.detail, vm.title);
            _dummy[i] = vm;
        }

        ApplyDeals();
        RefreshViews();
        RefreshTopbar();
    }
    
}
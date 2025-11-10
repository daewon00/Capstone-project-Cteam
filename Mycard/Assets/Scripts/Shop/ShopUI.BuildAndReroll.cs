using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityRandom = UnityEngine.Random;

public partial class ShopUI : MonoBehaviour
{
    // ==========================================================
    // 4. 데이터 준비 (Model)
    // - 게임에 존재하는 모든 카드 데이터를 불러와서, 나중에 쉽게 찾아 쓸 수 있도록
    //   'ID 전화번호부'와 '이름 전화번호부'를 만드는 역할입니다.
    // - 이 기능은 데이터를 준비하는 순수한 로직이므로, 나중에 'Service'나
    //   별도의 'CardDataManager' 같은 곳으로 옮기면 더 좋습니다.
    // ==========================================================
    private void LoadAllCardData()// ... 카드 데이터 로드 및 _cardIdMap, _cardNameMap 채우는 로직 ...
    {
        if (cardPool == null) cardPool = new List<CardScriptableObject>();

        //카드 로드해서 정보 가져옴
        var loadedCards = Resources.LoadAll<CardScriptableObject>(CardsPath);
        if (loadedCards != null && loadedCards.Length > 0)
        {
            var set = new HashSet<CardScriptableObject>(cardPool);
            foreach (var c in loadedCards)
                if (c != null && !set.Contains(c))
                    cardPool.Add(c);
        }
        VLog($"[ShopUI] cardPool merged count = {cardPool.Count}");
        if (cardPool.Count == 0)
            GameLog.Warn($"[ShopUI] No cards found in inspector or Resources/{CardsPath}");

        // 카드 ID 매핑 구축 (여기가 빠지면 ImportSession에서 카드 3칸이 제대로 복원 안 됨)
        _cardIdMap.Clear();
        _cardNameMap.Clear();
        foreach (var card in cardPool)
        {
            if (card == null) continue;

            // Id 맵
            if (!string.IsNullOrEmpty(card.CardId))
            {
                if (_cardIdMap.ContainsKey(card.CardId))
                    GameLog.Warn($"[ShopUI] Duplicate CardId: {card.CardId}");
                else
                    _cardIdMap[card.CardId] = card;
            }

            // Name 맵 (표시용 이름)
            if (!string.IsNullOrEmpty(card.cardName))
            {
                if (_cardNameMap.ContainsKey(card.cardName))
                    GameLog.Warn($"[ShopUI] Duplicate CardName: {card.cardName}");
                else
                    _cardNameMap[card.cardName] = card;
            }
        }
        // [CCTV] 전화번호부가 완성되었는지 확인
        GameLog.Info($"<color=purple>[CCTV] '카드 전화번호부'(_cardIdMap) 생성 완료. 총 {cardPool.Count}개의 카드 중 { _cardIdMap.Count}개가 등록되었습니다.</color>", this);
    }

    // ==========================================================
    // 5. 상품 진열 로직 (Business Logic)
    // - 상점에 처음 진열할 상품 목록을 구성하는 기능들입니다.
    // - 이 기능들은 '어떻게' 상품을 진열할지에 대한 '규칙'이므로, 'ShopService'로 옮기기에
    //   가장 적합한 핵심 비즈니스 로직입니다.
    // ==========================================================
    private void BuildDummySlots() // '진열대'에 임시 상품(마네킹)을 채우고 기본 가격을 설정합니다.
    {
        // _dummy는 상점에 진열될 상품 정보를 담는 '진열대' 역할을 합니다.
        // _dummy 리스트를 4칸짜리 새 리스트로 초기화합니다.
        _dummy = new List<ShopSlotVM>(4)
        {
            new ShopSlotVM{ detail="Card",  rarity = CardRarity.Common },
            new ShopSlotVM{ detail="Card",  rarity = CardRarity.Common },
            new ShopSlotVM{ detail="Card",  rarity = CardRarity.Common },
            new ShopSlotVM{ detail="Relic", rarity = CardRarity.Common },
        };

        // 방금 진열한 모든 아이템('마네킹' 포함)을 하나씩 돌면서 가격을 계산하고 설정합니다.
        for (int i = 0; i < _dummy.Count; i++)
        {
            var vm = _dummy[i];
            vm.price = vm.detail == "Relic"
                ? 200
                : BasePriceOf(vm.detail, vm.title, vm.rarity);
            _dummy[i] = vm;
        }
    }

    // CardScriptableObject(카드 원본)를 ShopSlotVM(상점 표시용 데이터)으로 변환하는 공장입니다.
    private ShopSlotVM ToVM(CardScriptableObject so) 
    {
        var icon = so.characterSprite != null ? so.characterSprite : so.bgSprite;
        return new ShopSlotVM
        {
            cardData = so,
            itemId  = so.CardId,
            title   = so.cardName,
            detail  = "Card",
            icon    = icon,
            price   = BasePriceOf("Card", so.cardName, so.Rarity),
            soldOut = false,
            isDeal  = false,
            rarity  = so.Rarity,
        };
    }

    // '진열대'의 카드 마네킹 3개를 실제 랜덤 카드로 교체합니다.
    private void BuildCardSlotsInitial()
    {
        // (안전장치) 카드 데이터가 담긴 '창고'(cardPool)가 비어있으면, 함수를 즉시 종료합니다.
        if (cardPool == null || cardPool.Count == 0) return;

        var picker = CreateCardPicker();
        if (picker == null)
            return;

        // 뽑았던 카드를 다시 뽑지 않기 위한 '제외 목록'
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 상점의 첫 3칸(카드 전용 슬롯)에 대해서만 반복 작업을 합니다.
        for (int i = 0; i < 3; i++)
        {
            var pick = DrawUniqueCard(picker, exclude);
            if (pick == null) { _cardSources[i] = null; continue; }

            if (!string.IsNullOrEmpty(pick.CardId))
                exclude.Add(pick.CardId);
            _cardSources[i] = pick;
            // 진열대(_dummy)의 i번째 칸에 있던 '마네킹'을 방금 뽑은 진짜 카드(pick) 정보로 교체합니다.
            _dummy[i] = ToVM(pick);
        }

        PopulateRelicSlotsInitial();
    }

    // ==========================================================
    // 6. 리롤 로직 (Business Logic + Controller)
    // - 기존 상품 목록을 새로운 상품으로 교체하는 기능들입니다.
    // - 이 역시 'ShopService'가 담당해야 할 핵심 비즈니스 로직에 해당합니다.
    // - 지금은 상품을 뽑는 규칙(DrawUniqueCard)과 실제 행동(OnReroll)이 섞여있습니다.
    // ==========================================================

    // 판매 중인 카드를 제외하고, 새로운 카드 3개를 뽑아 교체합니다.
    private void RerollCardSlots()
    {
        var picker = CreateCardPicker();
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 3; i++)
        {
            if (_cardSources[i] != null && !string.IsNullOrEmpty(_cardSources[i].CardId))
                exclude.Add(_cardSources[i].CardId);
        }

        for (int slot = 0; slot < 3; slot++)
        {
            if (_dummy[slot].soldOut) continue;

            var pick = DrawUniqueCard(picker, exclude);
            if (pick == null) continue;

            if (!string.IsNullOrEmpty(pick.CardId))
                exclude.Add(pick.CardId);
            _cardSources[slot] = pick;
            _dummy[slot] = ToVM(pick);
        }
    }

    // 카드 창고(cardPool)에서 중복되지 않는 카드를 하나 뽑아옵니다.
    private CardScriptableObject DrawUniqueCard(WeightedCardPicker picker, HashSet<string> exclude)
    {
        if (picker == null)
            return null;

        return picker.PickOne(CardAcquisitionContext.Shop, () => UnityRandom.value, max => UnityRandom.Range(0, max), exclude);
    }

    private WeightedCardPicker CreateCardPicker()
    {
        if (cardPool == null || cardPool.Count == 0)
            return null;

        return new WeightedCardPicker(cardPool, card => card != null && !card.removeAfterCombat);
    }

    private void PopulateRelicSlotsInitial()
    {
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _dummy.Count; i++)
        {
            var id = _dummy[i].cardData != null ? _dummy[i].cardData.CardId : _dummy[i].itemId;
            if (!string.IsNullOrEmpty(id)) exclude.Add(id);
        }

        if (RelicSystem.Instance != null)
        {
            foreach (var ownedId in RelicSystem.Instance.EnumerateOwnedRelicIds())
            {
                if (!string.IsNullOrEmpty(ownedId))
                    exclude.Add(ownedId);
            }
        }

        for (int slot = 3; slot < _dummy.Count; slot++)
        {
            TryAssignRelicToSlot(slot, exclude);
        }
    }

    private bool TryAssignRelicToSlot(int index, HashSet<string> exclude)
    {
        if (index < 0 || index >= _dummy.Count) return false;

        var vm = _dummy[index];
        if (!string.Equals(vm.detail, "Relic", StringComparison.OrdinalIgnoreCase)) return false;
        if (vm.soldOut) return false;

        string relicId = DrawUnique(RelicsPool, exclude ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(relicId)) return false;
        exclude?.Add(relicId);

        var data = RelicSystem.Instance != null ? RelicSystem.Instance.GetRelicData(relicId) : null;

        vm.itemId = relicId;
        vm.title = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : relicId;
        vm.icon = data != null ? data.icon : null;
        vm.price = 200;
        vm.cardData = null;
        vm.detail = "Relic";
        _dummy[index] = vm;
        return true;
    }

    // 유물/소모품 목록에서 중복되지 않는 아이템 이름을 하나 뽑아옵니다.
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
        int idx = UnityRandom.Range(0, candidates.Count);
        return candidates[idx];
    }

    // '리롤' 버튼을 눌렀을 때 실행되는 메인 함수입니다. (골드 확인, 실제 교체 작업 지시 등)
    private void OnReroll()
    {
        if (_isRerollCooling) return;

        int cost = CurrentRerollCost();
        if (Gold < cost) return;

        _isRerollCooling = true;
        if (rerollButton) rerollButton.interactable = false;

        if (!TrySpendGold(cost))
        {
            _isRerollCooling = false;
            if (rerollButton) rerollButton.interactable = true;
            RefreshTopbar();
            return;
        }
        _rerollCount++;

        RerollCardSlots(); // 카드 3칸 교체

        // (유물/소모품 교체 로직)
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _dummy.Count; i++)
        {
            var id = _dummy[i].cardData != null ? _dummy[i].cardData.CardId : _dummy[i].itemId;
            if (!string.IsNullOrEmpty(id))
                exclude.Add(id);
        }

        if (RelicSystem.Instance != null)
        {
            foreach (var ownedId in RelicSystem.Instance.EnumerateOwnedRelicIds())
            {
                if (!string.IsNullOrEmpty(ownedId))
                    exclude.Add(ownedId);
            }
        }

        for (int i = 3; i < _dummy.Count; i++)
        {
            if (_dummy[i].soldOut) continue;
            if (_dummy[i].detail != "Relic") continue;

            TryAssignRelicToSlot(i, exclude);
        }


        ApplyDeals();
        OnSessionChanged?.Invoke(); // 상태 변화 감지로 db 저장 할수 있게
        
        RefreshViews();
        RefreshTopbar();
        _isRerollCooling = true;
        StartCoroutine(RerollCooldown());
        
    }

    // 리롤 버튼 연타를 막기 위한 쿨다운 코루틴입니다. (UI 제어)
    private IEnumerator RerollCooldown()
    {
        VLog($"[ShopUI] Reroll cooldown start ({rerollCooldownSec}s)");
        // UI는 보통 TimeScale 0에서도 동작해야 하니 unscaled 사용
        float t = 0f;
        while (t < rerollCooldownSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _isRerollCooling = false; // 락 해제
        RefreshTopbar();          // 조건(돈/후보) 맞으면 자동 재활성
        VLog("[ShopUI] Reroll cooldown end");
    }
    
}

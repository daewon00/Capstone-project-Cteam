using UnityEngine;
using Game.Save;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 이벤트 세션을 로드·저장하고 선택 결과를 런 상태에 반영하는 실행 서비스입니다.
/// </summary>
public sealed class EventManager : IEventManager 
{
    private readonly IDatabase _db;
    private readonly string _runId;
    private CurrentRun _run;

    private static readonly Dictionary<string, EventScriptableObject> EventCache = new();
    private readonly HashSet<string> _seededRngDomains = new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<string> _pendingUpgradeSelections = new Queue<string>();

    /// <summary>
    /// 런 ID와 DB 핸들을 받아 이벤트 매니저를 초기화합니다.
    /// </summary>
    public EventManager(IDatabase db, string runId)
    {
        _db = db;
        _runId = runId;
        _run = _db.LoadCurrentRun(_runId)?.Run;
        if (_run == null) Debug.LogError("[EventManager] 현재 런 정보를 찾을 수 없습니다.");
    }

    /// <summary>
    /// 활성 이벤트 세션을 불러오거나 없으면 지정한 ID 기준으로 새 세션을 생성합니다.
    /// </summary>
    public EventSessionDTO LoadActiveOrCreate(string eventIdFallback)
    {
        if (_run == null) return null;

        // 1. DB에서 먼저 데이터를 불러옵니다.
        var json = _db.LoadActiveEventSessionJson(_run.RunId);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var dto = JsonUtility.FromJson<EventSessionDTO>(json);
                HydrateSession(dto);
                return dto;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventManager] 활성 이벤트 세션 JSON 파싱 실패 - 기존 데이터를 삭제합니다. {e.Message}");
                _db.DeleteActiveEventSession(_run.RunId);
            }
        }

        // 2. DB에 데이터가 없으면 새로 생성합니다.
        var eventSO = LoadEventAsset(string.IsNullOrEmpty(eventIdFallback) ? null : eventIdFallback);
        if (eventSO == null)
        {
            Debug.LogError($"[EventManager] 이벤트 원본 파일 없음: {eventIdFallback}");
            return null;
        }

        var initialStage = eventSO.GetFirstStage();
        if (initialStage == null)
        {
            Debug.LogError($"[EventManager] 이벤트 '{eventSO.eventId}'에 유효한 스테이지가 없습니다.");
            return null;
        }

        var newSession = BuildSession(eventSO, initialStage);
        _db.UpsertActiveEventSession(_run.RunId, JsonUtility.ToJson(newSession));
        return newSession;
    }

    /// <summary>
    /// 활성 세션을 생성하지 않고 존재 여부만 확인해 불러옵니다.
    /// </summary>
    public EventSessionDTO TryLoadActive()
    {
        if (_run == null) return null;

        var json = _db.LoadActiveEventSessionJson(_run.RunId);
        if (string.IsNullOrEmpty(json))
        {
            return null; // DB에 없으면 null 반환 (새로 생성 안 함)
        }

        try
        {
            var dto = JsonUtility.FromJson<EventSessionDTO>(json);
            HydrateSession(dto);
            return dto;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EventManager] 활성 이벤트 JSON 파싱 실패 - 기존 데이터를 삭제합니다. {e.Message}");
            _db.DeleteActiveEventSession(_run.RunId);
            return null;
        }
    }

    public void QueueUpgradeSelection(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return;
        Debug.Log($"[EventManager] QueueUpgradeSelection {instanceId}");
        _pendingUpgradeSelections.Enqueue(instanceId);
    }

    /// <summary>
    /// 전달된 선택지를 적용하고 런 상태 및 맵 노드 저장 데이터를 갱신합니다.
    /// </summary>
    public bool ApplyChoice(EventSessionDTO session, EventChoiceDTO choice)
    {
        if (session == null || choice == null || _run == null)
        {
            return false;
        }

        // --- 1. 효과 적용 ---
        bool shouldReturnToMap = false;

        var effects = choice.effects ?? Array.Empty<EventEffectDTO>();

        int hpBefore = _run.CurrentHp;
        var tokenReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UPGRADE_MESSAGE"] = string.Empty,
            ["UPGRADED_CARD_NAMES"] = string.Empty,
            ["UPGRADED_COUNT"] = "0"
        };

        foreach (var effect in effects)
        {
            bool handled = false;

            if (effect.type == EventEffectType.HpDelta)
            {
                var targetHp = _run.CurrentHp + effect.amount;
                _db.UpdateRunHp(_run.RunId, targetHp);
                var maxHp = _run.MaxHpBase + _run.MaxHpFromPerks + _run.MaxHpFromRelics;
                _run.CurrentHp = Mathf.Clamp(targetHp, 0, Mathf.Max(1, maxHp));
                _run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                Debug.Log($"[EventManager] Applied HpDelta {effect.amount} → targetHp={_run.CurrentHp}");
                handled = true;
            }
            else if (effect.type == EventEffectType.GoldDelta)
            {
                // 지갑 서비스가 있으면 그것을 통해 처리(브로드캐스트 + DB-우선)
                var wallet = ServiceRegistry.Get<IWalletService>();
                if (wallet != null)
                {
                    wallet.Add(effect.amount);
                    _run.Gold = Mathf.Max(0, wallet.Gold);
                    Debug.Log($"[EventManager] Applied GoldDelta {effect.amount} via WalletService");
                }
                else
                {
                    // 폴백: 기존 DB 직접 업데이트
                    var newGold = _run.Gold + effect.amount;
                    _db.UpdateRunGold(_run.RunId, newGold);
                    _run.Gold = Mathf.Max(0, newGold);
                    Debug.Log($"[EventManager] Applied GoldDelta {effect.amount} directly to DB");
                }
                _run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                handled = true;
            }
            else if (effect.type == EventEffectType.MaxHpDelta)
            {
                handled = true;
                int delta = effect.amount;
                if (delta == 0)
                {
                    Debug.Log("[EventManager] MaxHpDelta 효과가 0이므로 변동 없음");
                }
                else
                {
                    int oldBase = _run.MaxHpBase;
                    int newBase = Mathf.Max(1, oldBase + delta);
                    int oldMax = oldBase + _run.MaxHpFromPerks + _run.MaxHpFromRelics;
                    int newMax = newBase + _run.MaxHpFromPerks + _run.MaxHpFromRelics;

                    int newCurrent = _run.CurrentHp;
                    if (delta > 0)
                    {
                        newCurrent = Mathf.Clamp(newCurrent + delta, 0, Mathf.Max(1, newMax));
                    }
                    else
                    {
                        newCurrent = Mathf.Clamp(newCurrent, 0, Mathf.Max(1, newMax));
                    }

                    _db.UpdateRunMaxHp(_run.RunId, newBase, newCurrent);
                    _run.MaxHpBase = newBase;
                    _run.CurrentHp = newCurrent;
                    _run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                    Debug.Log($"[EventManager] MaxHpDelta {delta} 적용: max {oldMax} → {newMax}, current={newCurrent}");
                }
            }
            else if (effect.type == EventEffectType.AddCard)
            {
                handled = true;
                if (!TryAddCards(effect, isCurse: false))
                {
                    Debug.LogWarning("[EventManager] AddCard 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.AddCurse)
            {
                handled = true;
                if (!TryAddCards(effect, isCurse: true))
                {
                    Debug.LogWarning("[EventManager] AddCurse 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.AddRelic)
            {
                handled = true;
                if (!TryAddRelic(effect))
                {
                    Debug.LogWarning("[EventManager] AddRelic 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.HealPercent)
            {
                handled = true;
                if (!TryApplyHealPercent(effect))
                {
                    Debug.LogWarning("[EventManager] HealPercent 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.TransformCard)
            {
                handled = true;
                if (!TryTransformCards(effect))
                {
                    Debug.LogWarning("[EventManager] TransformCard 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.UpgradeRandomCard)
            {
                handled = true;
                Debug.Log("[EventManager] UpgradeRandomCard 효과 처리 시작");
                if (!TryUpgradeRandomCards(effect, tokenReplacements))
                {
                    Debug.LogWarning("[EventManager] UpgradeRandomCard 효과 적용 중 문제가 발생했습니다.");
                }
            }
            else if (effect.type == EventEffectType.ReturnToMap)
            {
                shouldReturnToMap = true;
                Debug.Log("[EventManager] ReturnToMap 플래그 적용됨");
                handled = true;
            }

            if (!handled)
            {
                Debug.LogError($"[EventManager] Unknown event effect type '{effect.type}' (eventId={session.eventId}, choiceId={choice.id})");
            }
        }

        int hpAfter = _run.CurrentHp;
        int hpDeltaTotal = hpAfter - hpBefore;
        int maxHpAfter = _run.MaxHpBase + _run.MaxHpFromPerks + _run.MaxHpFromRelics;
        tokenReplacements["HP_DELTA"] = hpDeltaTotal.ToString();
        tokenReplacements["HP_DELTA_ABS"] = Mathf.Abs(hpDeltaTotal).ToString();
        tokenReplacements["HP_CURRENT"] = hpAfter.ToString();
        tokenReplacements["HP_MAX"] = maxHpAfter.ToString();

        if (shouldReturnToMap)
        {
            // --- 3. 상세한 결과 기록 생성 ---
            var resolution = new EventResolutionSnapshot
            {
                eventId = session.eventId,
                selectedChoiceId = choice.id,
                appliedEffects = choice.effects,
                resolvedAtUtc = DateTime.UtcNow.ToString("o")
            };

            // --- 4. MapNodeState에 해결 기록 저장 ---
            var resolvedNodeType = ResolveNodeType(session.eventId);
            var node = new MapNodeState
            {
                RunId = _run.RunId,
                Act = _run.Act,
                Floor = _run.Floor,
                NodeIndex = _run.NodeIndex,
                Type = resolvedNodeType,
                Visited = true,
                Cleared = true,
                EventResolutionJson = JsonUtility.ToJson(resolution)
            };
            _db.UpsertNodeState(node);

            // --- 5. 활성 이벤트 세션 삭제 ---
            _db.DeleteActiveEventSession(_run.RunId);

            RunCacheSynchronizer.Sync();
            NotifyRunOverlay();
            return true;
        }

        if (!string.IsNullOrEmpty(choice.nextStageId))
        {
            var eventSO = LoadEventAsset(session.eventId);
            if (eventSO == null)
            {
                Debug.LogError($"[EventManager] 이벤트 원본을 찾을 수 없어 다음 스테이지로 이동할 수 없습니다: {session.eventId}");
                _db.DeleteActiveEventSession(_run.RunId);
                return true; // 안전하게 이벤트 종료
            }

            var nextStage = eventSO.GetStageOrFirst(choice.nextStageId);
            if (nextStage == null)
            {
                Debug.LogError($"[EventManager] 이벤트 '{session.eventId}'에서 스테이지 '{choice.nextStageId}'를 찾을 수 없습니다. 맵으로 복귀합니다.");
                _db.DeleteActiveEventSession(_run.RunId);
                return true;
            }

            var nextSession = BuildSession(eventSO, nextStage);
            ApplyTokenReplacements(nextSession, tokenReplacements);
            nextSession.eventId = session.eventId;
            nextSession.pickedChoiceId = choice.id;
            nextSession.stageId = nextStage.stageId;
            _db.UpsertActiveEventSession(_run.RunId, JsonUtility.ToJson(nextSession));
            NotifyRunOverlay();
            return false;
        }

        Debug.LogWarning($"[EventManager] 선택지 '{choice.id}'에 ReturnToMap 효과나 nextStageId가 없어 자동으로 맵으로 복귀합니다.");
        _db.DeleteActiveEventSession(_run.RunId);
        RunCacheSynchronizer.Sync();
        NotifyRunOverlay();
        return true;
    }

    private bool TryUpgradeRandomCards(EventEffectDTO effect, Dictionary<string, string> tokenReplacements)
    {
        Debug.Log($"[EventManager] TryUpgradeRandomCards 시작 - pendingQueue={_pendingUpgradeSelections.Count}");
        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService == null)
        {
            SetUpgradeTokens(tokenReplacements, "카드를 관리하는 서비스가 없어 강화가 취소되었습니다.", Array.Empty<string>());
            return false;
        }

        var catalog = ServiceRegistry.Get<ICardCatalog>();
        if (catalog == null)
        {
            SetUpgradeTokens(tokenReplacements, "카드 데이터를 찾을 수 없어 강화가 취소되었습니다.", Array.Empty<string>());
            return false;
        }

        var rng = ServiceRegistry.Get<IRngService>();
        if (rng == null)
        {
            SetUpgradeTokens(tokenReplacements, "무작위 서비스를 찾을 수 없어 강화가 취소되었습니다.", Array.Empty<string>());
            return false;
        }

        var candidates = new List<CardRuntimeState>();
        var locations = new[]
        {
            CardLocation.Hand,
            CardLocation.DrawPile,
            CardLocation.DiscardPile,
            CardLocation.ExhaustPile
        };

        foreach (var location in locations)
        {
            var cards = deckService.GetCardsInLocation(location);
            if (cards == null) continue;

            foreach (var card in cards)
            {
                if (card == null) continue;
                if (!CardUpgradeRules.IsUpgradeable(card, catalog))
                    continue;
                candidates.Add(card);
            }
        }

        if (candidates.Count == 0)
        {
            SetUpgradeTokens(tokenReplacements, "강화할 수 있는 카드가 없어 조용히 휴식을 취했습니다.", Array.Empty<string>());
            return true;
        }

        int countToUpgrade = effect.quantity > 0 ? effect.quantity : 1;
        EnsureRngSeeded(rng, "event-card-upgrade");

        var toUpgrade = new List<CardRuntimeState>();

        while (_pendingUpgradeSelections.Count > 0 && toUpgrade.Count < countToUpgrade)
        {
            var requestedId = _pendingUpgradeSelections.Dequeue();
            if (string.IsNullOrEmpty(requestedId))
                continue;

            var match = candidates.FirstOrDefault(c => string.Equals(c.InstanceId, requestedId, StringComparison.Ordinal));
            if (match != null)
            {
                Debug.Log($"[EventManager] 선택한 카드와 일치: {match.InstanceId}");
                toUpgrade.Add(match);
                candidates.Remove(match);
            }
            else
            {
                Debug.LogWarning($"[EventManager] 요청된 카드({requestedId})를 강화 후보에서 찾을 수 없어 무작위로 대체합니다.");
            }
        }

        while (toUpgrade.Count < countToUpgrade && candidates.Count > 0)
        {
            int index = rng.NextInt("event-card-upgrade", 0, candidates.Count);
            var selected = candidates[index];
            candidates.RemoveAt(index);
            toUpgrade.Add(selected);
        }

        var upgradedNames = new List<string>();
        foreach (var selected in toUpgrade)
        {
            if (selected == null)
                continue;

            if (!catalog.TryGetCardData(selected.CardId, out var cardData) || cardData == null)
            {
                Debug.LogWarning($"[EventManager] 카드 데이터 없음: {selected.CardId}");
                continue;
            }

            if (!deckService.SetCardUpgradeState(selected.InstanceId, true))
            {
                Debug.LogWarning($"[EventManager] SetCardUpgradeState 실패: {selected.InstanceId}");
                continue;
            }

            var displayName = !string.IsNullOrEmpty(cardData.cardName) ? cardData.cardName : cardData.name;
            upgradedNames.Add(displayName);
        }

        if (upgradedNames.Count == 0)
        {
            SetUpgradeTokens(tokenReplacements, "강화 시도가 있었지만 덱 상태는 변하지 않았습니다.", Array.Empty<string>());
            return true;
        }

        string message = upgradedNames.Count == 1
            ? $"강화의 열기로 '{upgradedNames[0]}' 카드가 새롭게 태어났습니다."
            : $"강화의 열기로 {upgradedNames.Count}장의 카드가 한층 더 강해졌습니다: {string.Join(", ", upgradedNames)}.";
        SetUpgradeTokens(tokenReplacements, message, upgradedNames);
        return true;
    }

    private static void SetUpgradeTokens(Dictionary<string, string> tokens, string message, IList<string> cardNames)
    {
        if (tokens == null) return;
        tokens["UPGRADE_MESSAGE"] = message ?? string.Empty;
        var names = cardNames != null && cardNames.Count > 0 ? string.Join(", ", cardNames) : string.Empty;
        tokens["UPGRADED_CARD_NAMES"] = names;
        tokens["UPGRADED_COUNT"] = (cardNames?.Count ?? 0).ToString();
    }

    private void EnsureRngSeeded(IRngService rng, string domain)
    {
        if (rng == null || string.IsNullOrEmpty(domain)) return;
        if (_run == null || string.IsNullOrEmpty(_run.RunId)) return;
        if (_seededRngDomains.Contains(domain)) return;

        try
        {
            rng.Seed(domain, HashRunIdToSeed(_run.RunId, domain));
            _seededRngDomains.Add(domain);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EventManager] RNG 시드 설정 실패(domain={domain}): {e.Message}");
        }
    }

    private static uint HashRunIdToSeed(string runId, string domain)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(runId))
            {
                foreach (char c in runId)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            if (!string.IsNullOrEmpty(domain))
            {
                foreach (char c in domain)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }

    // 결과 기록용 내부 클래스
    [Serializable]
    private class EventResolutionSnapshot
    {
        /// <summary>
        /// 해결된 이벤트 ID입니다.
        /// </summary>
        public string eventId;
        /// <summary>
        /// 플레이어가 선택한 선택지 ID입니다.
        /// </summary>
        public string selectedChoiceId;
        /// <summary>
        /// 적용된 효과 목록입니다.
        /// </summary>
        public EventEffectDTO[] appliedEffects;
        /// <summary>
        /// 해결 시각(UTC ISO8601)입니다.
        /// </summary>
        public string resolvedAtUtc;
    }

    private static void ApplyTokenReplacements(EventSessionDTO session, IReadOnlyDictionary<string, string> tokens)
    {
        if (session == null || tokens == null || tokens.Count == 0) return;
        session.description = ReplaceTokens(session.description, tokens);
        if (session.choices == null) return;

        foreach (var choice in session.choices)
        {
            if (choice == null) continue;
            choice.label = ReplaceTokens(choice.label, tokens);
        }
    }

    private static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(text) || tokens == null || tokens.Count == 0) return text;
        foreach (var pair in tokens)
        {
            if (string.IsNullOrEmpty(pair.Key)) continue;
            text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
        }
        return text;
    }

    private static Game.Save.NodeType ResolveNodeType(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId) && string.Equals(eventId, EventIds.CampfireRest, StringComparison.Ordinal))
        {
            return Game.Save.NodeType.Rest;
        }

        return Game.Save.NodeType.Event;
    }

    private void HydrateSession(EventSessionDTO dto)
    {
        if (dto == null) return;

        if (string.IsNullOrEmpty(dto.eventId))
        {
            Debug.LogWarning("[EventManager] 이벤트 ID가 지정되지 않은 세션입니다.");
            return;
        }

        var eventSO = LoadEventAsset(dto.eventId);
        if (eventSO == null)
        {
            return;
        }

        var stage = eventSO.GetStageOrFirst(dto.stageId);
        if (stage == null)
        {
            Debug.LogWarning($"[EventManager] 이벤트 '{dto.eventId}'에서 stage '{dto.stageId}'를 찾을 수 없어 첫 번째 스테이지로 대체합니다.");
            stage = eventSO.GetFirstStage();
        }

        if (stage == null)
        {
            dto.description = string.Empty;
            dto.choices = Array.Empty<EventChoiceDTO>();
            return;
        }

        dto.stageId = stage.stageId;

        if (string.IsNullOrEmpty(dto.description))
        {
            dto.description = stage.description ?? string.Empty;
        }

        if (dto.choices == null || dto.choices.Length == 0)
        {
            dto.choices = stage.choices?.Select(ConvertChoiceToDto).ToArray() ?? Array.Empty<EventChoiceDTO>();
        }
        dto.resolved = false;
    }

    private EventSessionDTO BuildSession(EventScriptableObject so, EventStage stage)
    {
        return new EventSessionDTO
        {
            eventId = so.eventId,
            stageId = stage.stageId,
            resolved = false,
            description = stage.description ?? string.Empty,
            choices = stage.choices?.Select(ConvertChoiceToDto).ToArray() ?? Array.Empty<EventChoiceDTO>()
        };
    }

    private EventChoiceDTO ConvertChoiceToDto(EventChoice choice)
    {
        if (choice == null)
        {
            return new EventChoiceDTO
            {
                id = string.Empty,
                label = string.Empty,
                effects = Array.Empty<EventEffectDTO>(),
                nextStageId = string.Empty
            };
        }

        return new EventChoiceDTO
        {
            id = choice.id,
            label = choice.label,
            nextStageId = choice.nextStageId,
            effects = choice.effects?.Select(e => new EventEffectDTO
            {
                type = e.type,
                amount = e.amount,
                refId = e.refId,
                quantity = e.quantity,
                upgrade = e.upgrade
            }).ToArray() ?? Array.Empty<EventEffectDTO>()
        };
    }

    private EventScriptableObject LoadEventAsset(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return null;

        if (EventCache.TryGetValue(eventId, out var cached) && cached != null)
        {
            cached.EnsureStageData();
            return cached;
        }

        var so = Resources.Load<EventScriptableObject>($"Events/{eventId}");
        if (so == null)
        {
            Debug.LogError($"[EventManager] 이벤트 원본 파일을 불러올 수 없습니다: {eventId}");
            EventCache.Remove(eventId);
            return null;
        }

        so.EnsureStageData();
        EventCache[eventId] = so;
        return so;
    }

    private bool TryAddCards(EventEffectDTO effect, bool isCurse)
    {
        if (string.IsNullOrEmpty(effect.refId))
        {
            Debug.LogWarning("[EventManager] AddCard/AddCurse 효과에 refId가 비어 있습니다.");
            return false;
        }

        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService == null)
        {
            Debug.LogError("[EventManager] IDeckService가 등록되지 않아 카드를 추가할 수 없습니다.");
            return false;
        }

        int quantity = effect.quantity;
        if (quantity <= 0)
        {
            quantity = effect.amount != 0 ? Mathf.Abs(effect.amount) : 1;
        }
        for (int i = 0; i < quantity; i++)
        {
            deckService.AddCardToDeckById(effect.refId, effect.upgrade);
        }

        Debug.Log($"[EventManager] {(isCurse ? "AddCurse" : "AddCard")} 적용: cardId={effect.refId}, qty={quantity}, upgrade={effect.upgrade}");
        return true;
    }

    private bool TryAddRelic(EventEffectDTO effect)
    {
        if (string.IsNullOrEmpty(effect.refId))
        {
            Debug.LogWarning("[EventManager] AddRelic 효과에 relicId(refId)가 비어 있습니다.");
            return false;
        }

        var relicSystem = RelicSystem.Instance;
        if (relicSystem == null)
        {
            Debug.LogError("[EventManager] RelicSystem 인스턴스를 찾을 수 없어 유물을 지급할 수 없습니다.");
            return false;
        }

        int stacks = effect.quantity;
        if (stacks <= 0)
        {
            stacks = effect.amount != 0 ? Mathf.Abs(effect.amount) : 1;
        }
        bool granted = relicSystem.AddRelicById(effect.refId, stacks, save: true);
        if (!granted)
        {
            Debug.LogWarning($"[EventManager] 유물 지급에 실패했습니다. relicId={effect.refId}");
        }
        else
        {
            Debug.Log($"[EventManager] AddRelic 적용: relicId={effect.refId}, stacks={stacks}");
        }

        return granted;
    }

    private bool TryApplyHealPercent(EventEffectDTO effect)
    {
        if (effect.amount == 0) return true;

        var maxHp = _run.MaxHpBase + _run.MaxHpFromPerks + _run.MaxHpFromRelics;
        if (maxHp <= 0) maxHp = _run.CurrentHp;

        float ratio = effect.amount / 100f;
        float rawDelta = maxHp * ratio;
        int hpDelta = effect.amount >= 0 ? Mathf.CeilToInt(rawDelta) : Mathf.FloorToInt(rawDelta);
        if (hpDelta == 0)
        {
            hpDelta = effect.amount >= 0 ? 1 : -1;
        }

        int targetHp = Mathf.Clamp(_run.CurrentHp + hpDelta, 0, Mathf.Max(1, maxHp));
        _db.UpdateRunHp(_run.RunId, targetHp);
        _run.CurrentHp = targetHp;
        _run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        Debug.Log($"[EventManager] HealPercent {effect.amount}% → delta={hpDelta}, hp={_run.CurrentHp}/{maxHp}");
        return true;
    }

    private bool TryTransformCards(EventEffectDTO effect)
    {
        if (string.IsNullOrEmpty(effect.refId))
        {
            Debug.LogWarning("[EventManager] TransformCard 효과에 target cardId(refId)가 비어 있습니다.");
            return false;
        }

        var deckService = ServiceRegistry.Get<IDeckService>();
        if (deckService == null)
        {
            Debug.LogError("[EventManager] IDeckService가 등록되지 않아 카드를 변환할 수 없습니다.");
            return false;
        }

        int count = effect.quantity;
        if (count <= 0)
        {
            count = effect.amount != 0 ? Mathf.Abs(effect.amount) : 1;
        }
        int transformed = deckService.TransformCards(effect.refId, count, effect.upgrade);
        if (transformed <= 0)
        {
            Debug.LogWarning($"[EventManager] TransformCard 효과 적용 결과 변환된 카드가 없습니다. target={effect.refId}");
            return false;
        }

        Debug.Log($"[EventManager] TransformCard 적용: target={effect.refId}, requested={count}, applied={transformed}, upgrade={effect.upgrade}");
        return true;
    }

    public bool TryGetRunSnapshot(out EventRunSnapshot snapshot)
    {
        if (_run == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = new EventRunSnapshot
        {
            RunId = _run.RunId,
            CurrentHp = _run.CurrentHp,
            MaxHpBase = _run.MaxHpBase,
            MaxHpFromPerks = _run.MaxHpFromPerks,
            MaxHpFromRelics = _run.MaxHpFromRelics,
            EnergyMax = _run.EnergyMax,
            Gold = _run.Gold
        };
        return true;
    }

    public void RebindRunCache(CurrentRun freshRun)
    {
        if (freshRun == null)
        {
            Debug.LogWarning("[EventManager] RebindRunCache called with null run; ignoring.");
            return;
        }

        if (!string.Equals(freshRun.RunId, _runId, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[EventManager] RebindRunCache runId mismatch. expected={_runId}, provided={freshRun.RunId}");
        }

        if (_run != null && string.Equals(_run.UpdatedAtUtc, freshRun.UpdatedAtUtc, StringComparison.Ordinal))
        {
            return;
        }

        _run = freshRun;
    }

    private void NotifyRunOverlay()
    {
        var overlay = ServiceRegistry.Get<RunStatOverlay>();
        if (overlay == null) return;

        if (TryGetRunSnapshot(out var snapshot))
        {
            overlay.Refresh(snapshot);
        }
        else
        {
            overlay.RefreshFallback();
        }
    }
}

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

    /// <summary>
    /// 전달된 선택지를 적용하고 런 상태 및 맵 노드 저장 데이터를 갱신합니다.
    /// </summary>
    public bool ApplyChoice(EventSessionDTO session, EventChoiceDTO choice)
    {
        if (choice == null || _run == null)
        {
            return false;
        }

        // --- 1. 효과 적용 ---
        bool shouldReturnToMap = false;

        var effects = choice.effects ?? Array.Empty<EventEffectDTO>();

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
            var node = new MapNodeState
            {
                RunId = _run.RunId,
                Act = _run.Act,
                Floor = _run.Floor,
                NodeIndex = _run.NodeIndex,
                Type = Game.Save.NodeType.Event,
                Visited = true,
                Cleared = true,
                EventResolutionJson = JsonUtility.ToJson(resolution)
            };
            _db.UpsertNodeState(node);

            // --- 5. 활성 이벤트 세션 삭제 ---
            _db.DeleteActiveEventSession(_run.RunId);

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
            nextSession.eventId = session.eventId;
            nextSession.pickedChoiceId = choice.id;
            nextSession.stageId = nextStage.stageId;
            _db.UpsertActiveEventSession(_run.RunId, JsonUtility.ToJson(nextSession));
            NotifyRunOverlay();
            return false;
        }

        Debug.LogWarning($"[EventManager] 선택지 '{choice.id}'에 ReturnToMap 효과나 nextStageId가 없어 자동으로 맵으로 복귀합니다.");
        _db.DeleteActiveEventSession(_run.RunId);
        NotifyRunOverlay();
        return true;
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
        dto.description = stage.description ?? string.Empty;
        dto.choices = stage.choices?.Select(ConvertChoiceToDto).ToArray() ?? Array.Empty<EventChoiceDTO>();
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

    private void NotifyRunOverlay()
    {
        var overlay = ServiceRegistry.Get<EventRunStatOverlay>();
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

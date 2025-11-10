using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;
using UnityEngine.SceneManagement;

/// <summary>
/// 상점 UI를 열고 닫으며 세션 상태를 DB와 메모리에 저장·복원하는 컨트롤러입니다.
/// </summary>
public class ShopOverlayController : MonoBehaviour
{
    [SerializeField] private ShopUI shopUI;

    // 각 노드 주소별 상점 상태를 기억할 메모리 캐시
    private readonly Dictionary<(int floor, int index), ShopSessionDTO> _sessionMemory = new();
    private (int floor, int index) _currentKey;
    private CurrentRun _currentRun; // 현재 플레이어의 런 데이터
    private IDeckService _deckService;

    void Awake()
    {
        if (shopUI == null) shopUI = FindObjectOfType<ShopUI>(true);

        // --- 지갑 연결 ---
        // 1. 현재 런 데이터를 불러옵니다.
        var runId = PlayerPrefs.GetString("lastRunId", "");
        var data = string.IsNullOrEmpty(runId) ? null : DatabaseManager.Instance.LoadCurrentRun(runId);
        _currentRun = data?.Run;

        // 2. 가능한 경우 WalletService를 통해 골드를 관리합니다. (DB-우선, 브로드캐스트 지원)
        var wallet = ServiceRegistry.Get<IWalletService>();
        if (wallet != null)
        {
            shopUI.GetGold = () => wallet.Gold;
            shopUI.SpendGold = amount =>
            {
                if (amount == 0) return true;
                if (amount > 0) return wallet.TrySpend(amount);
                wallet.Add(-amount);
                return true;
            };
        }
        else if (_currentRun != null)
        {
            // 폴백: 기존 방식 유지
            shopUI.GetGold = () => _currentRun.Gold;
            shopUI.SpendGold = amount =>
            {
                if (_currentRun == null) return false;
                if (amount == 0) return true;
                if (amount < 0)
                {
                    _currentRun.Gold += -amount;
                    DatabaseManager.Instance.UpdateRunGold(_currentRun.RunId, _currentRun.Gold);
                    return true;
                }
                if (_currentRun.Gold < amount) return false;
                _currentRun.Gold = Mathf.Max(0, _currentRun.Gold - amount);
                DatabaseManager.Instance.UpdateRunGold(_currentRun.RunId, _currentRun.Gold);
                return true;
            };
        }
        // 지갑/런 정보가 전혀 없는 에디터 테스트 환경에서는 ShopUI가 내부 테스트 골드를 사용하도록 둡니다.

        _deckService = SafeGetDeckService();
        shopUI.TryAddCardToDeck = TryAddCardToDeckProxy;
        // 3. ShopUI의 상태가 바뀌면(OnSessionChanged) 자동으로 저장하도록 연결합니다.
        shopUI.OnSessionChanged += SaveCurrentShopSession;
    }

    /// <summary>
    /// 특정 노드 좌표에 해당하는 상점 세션을 불러오고 UI를 엽니다.
    /// </summary>
    public void OpenForNode(int floor, int index)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (shopUI != null)
        {
            if (!shopUI.gameObject.activeSelf)
            {
                shopUI.gameObject.SetActive(true);
            }

            if (!shopUI.enabled)
            {
                shopUI.enabled = true;
            }
        }

        _currentKey = (floor, index);
        GameLog.Info($"<color=cyan>OPENING shop for ({floor},{index})...</color>", this);

        if (_currentRun == null)
        {
            var runId = PlayerPrefs.GetString("lastRunId", "");
            var data = string.IsNullOrEmpty(runId) ? null : DatabaseManager.Instance.LoadCurrentRun(runId);
            _currentRun = data?.Run;
        }

        ShopSessionDTO dto = null;

        var savedSession = DatabaseManager.Instance.LoadActiveShopSession(_currentRun.RunId);

        // DB에서 불러온 세션 정보가 있고, 그 정보의 위치가 '현재 위치'와 일치하는지 확인합니다.
        if (savedSession != null && savedSession.Floor == floor && savedSession.Index == index)
        {
            // 위치가 일치하면, 저장된 JSON 데이터를 DTO 객체로 변환합니다.
            if (!string.IsNullOrEmpty(savedSession.Json))
            {
                try { dto = JsonUtility.FromJson<ShopSessionDTO>(savedSession.Json); }
                catch (System.Exception e) { GameLog.Warn($"[Shop] JSON parse fail: {e.Message}", this); }
            }
        }
        // DB에 데이터가 있었지만 위치가 다른 경우에는 즉시 삭제해 동기화를 맞춥니다.
        else if (savedSession != null)
        {
            // DB에 저장된 정보가 '이전 상점'의 낡은 정보라면, 즉시 삭제하여 DB를 깨끗하게 유지합니다.
            GameLog.Warn($"[Shop] 이전 상점({savedSession.Floor},{savedSession.Index})의 데이터가 감지되어 DB에서 삭제합니다.");
            DatabaseManager.Instance.DeleteActiveShopSession(_currentRun.RunId);
        }

        // DB 데이터가 dto로 옮겨졌다면 해당 정보로 상점을 복원합니다.
        if (dto != null)
        {
            GameLog.Info($"<color=green>SUCCESS: Loaded from DATABASE.</color>", this);
            _sessionMemory[_currentKey] = dto; // 메모리 캐시도 최신 정보로 갱신해줍니다.
            shopUI.ImportSession(dto);  // 상점에 정보를 넣는다.
        }
        // DB에 데이터가 없을 때는 메모리 캐시를 확인합니다.
        else if (_sessionMemory.TryGetValue(_currentKey, out var memDto))
        {
            // 같은 게임 세션 내에서 재방문한 경우, 메모리 정보로 상점을 복원합니다.
            GameLog.Info($"<color=green>SUCCESS: Loaded from MEMORY CACHE.</color>", this);
            shopUI.ImportSession(memDto);
        }
        // DB와 메모리 두 곳 모두에 데이터가 없으면 새 세션을 초기화합니다.
        else
        {
            // '완전 최초 방문'이므로, 새로운 상점으로 초기화합니다.
            GameLog.Info($"<color=yellow>INFO: Nothing found in DB or Memory. Resetting session.</color>", this);
            shopUI.ResetSession();
        }

        // --- 3. UI 표시 및 초기 상태 저장 ---
        // 준비된 내용으로 상점 UI를 화면에 표시합니다.
        shopUI.Open();

        var stageService = ServiceRegistry.Get<IRunStageService>();
        if (stageService != null && _currentRun != null)
        {
            var payload = new RunStagePayloads.Shop
            {
                act = _currentRun.Act,
                floor = floor,
                nodeIndex = index
            };
            stageService.SetStage(RunStageType.ShopOverlay, SceneManager.GetActiveScene().name, RunStageService.ToJson(payload));
        }

        // 완전 최초 방문이라면 초기 상태를 DB에 저장합니다.
        if (dto == null && !_sessionMemory.ContainsKey(_currentKey))
        {
            // 두 조건이 모두 참일 때만, 방금 생성된 초기 상태를 DB에 저장합니다.
            GameLog.Info($"<color=orange>First visit detected. Saving initial state to DB...</color>", this);
            SaveCurrentShopSession();
        }
    }

    /// <summary>
    /// 현재 상점 세션을 DB와 메모리 캐시에 저장합니다.
    /// </summary>
    public void SaveCurrentShopSession()
    {
        if (_currentRun == null)
        {
            // [CCTV] 저장 실패: _currentRun이 없음
            GameLog.Error($"<color=red>SAVE FAILED:</color> _currentRun is null. Cannot save shop state.", this);
            return;
        }

        var dto = shopUI.ExportSession();
        var json = JsonUtility.ToJson(dto);

        GameLog.Info($"<color=orange>SAVING shop state for ({_currentKey.floor},{_currentKey.index}):</color>\n{json}", this);

        // 1) DB에 RunId 단일 세션으로 upsert
        DatabaseManager.Instance.UpsertActiveShopSession(_currentRun.RunId, json, _currentKey.floor,  _currentKey.index);

        // 2) 메모리 캐시도 최신화
        _sessionMemory[_currentKey] = dto;
    }

    private IDeckService SafeGetDeckService()
    {
        try
        {
            return ServiceRegistry.Get<IDeckService>();
        }
        catch
        {
            return null;
        }
    }

    private bool TryAddCardToDeckProxy(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            GameLog.Error("[Shop] TryAddCardToDeckProxy: cardId가 비어 있습니다.", this);
            return false;
        }

        if (_deckService == null)
        {
            _deckService = SafeGetDeckService();
            if (_deckService == null)
            {
                GameLog.Error($"[Shop] 덱 서비스가 등록되어 있지 않습니다. cardId={cardId}", this);
                return false;
            }
        }

        try
        {
            _deckService.AddCardToDeckById(cardId);
            return true;
        }
        catch (Exception e)
        {
            GameLog.Error($"[Shop] 덱 추가 실패: cardId={cardId}, error={e.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 상점 세션 초기화 플래그를 해제합니다.
    /// </summary>
    public void ResetShopSession()
    {
        shopUI?.ResetSession();
    }
    
    /// <summary>
    /// 메모리에 캐시된 모든 상점 세션 정보를 깨끗하게 비웁니다.
    /// </summary>
    public void ClearCachedSession()
    {
        _sessionMemory.Clear();
        GameLog.Info("[Shop] 메모리 캐시가 초기화되었습니다.");
    }
}

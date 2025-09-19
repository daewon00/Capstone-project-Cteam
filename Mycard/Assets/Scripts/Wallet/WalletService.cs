using UnityEngine;
using System;

/// <summary>
/// 플레이어 골드를 DB-우선 전략으로 관리하고 메타 이벤트를 발행하는 지갑 서비스입니다.
/// </summary>
public sealed class WalletService : IWalletService
{
    private readonly IDatabase _db;
    private string _runId;
    private int _gold;

    public event Action<int> OnGoldChanged;
    public int Gold => _gold;

    /// <summary>
    /// 지갑 서비스에 DB 핸들과 초기 런 ID를 주입합니다.
    /// </summary>
    public WalletService(IDatabase db, string runId)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        RebindRun(runId);
    }

    /// <summary>
    /// 런을 재바인딩하고 DB에서 최신 골드를 동기화합니다.
    /// </summary>
    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;

        int newGold = 0;
        if (!string.IsNullOrEmpty(_runId))
        {
            try
            {
                var loaded = _db.LoadCurrentRun(_runId);
                newGold = loaded?.Run != null ? loaded.Run.Gold : 0;
            }
            catch (Exception e)
            {
            Debug.LogError($"[WalletService] LoadCurrentRun 실패: {e.Message}");
            newGold = 0;
        }
        }

        newGold = Mathf.Max(0, newGold);
        if (_gold != newGold)
        {
            _gold = newGold;
            OnGoldChanged?.Invoke(_gold);
        }
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (_gold < amount) return false;
        return Set(_gold - amount);
    }

    public void Add(int amount)
    {
        if (amount == 0) return;
        Set(_gold + amount);
    }

    /// <summary>
    /// DB에 먼저 기록하고 성공 시 메모리 값을 변경한 뒤 브로드캐스트합니다.
    /// </summary>
    public bool Set(int amount)
    {
        int target = Mathf.Max(0, amount);
        int delta = target - _gold;
        if (target == _gold) return true;

        if (!string.IsNullOrEmpty(_runId))
        {
            try
            {
                _db.UpdateRunGold(_runId, target);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WalletService] DB UpdateRunGold 실패: {e.Message}");
                return false;
            }
        }

        _gold = target;
        OnGoldChanged?.Invoke(_gold);
        // 업적/진행도 훅을 위해 골드 변화를 브로드캐스트합니다.
        try
        {
            if (!string.IsNullOrEmpty(_runId) && delta != 0)
            {
                MetaEvents.RaiseGoldChanged(new MetaEvents.GoldChangedPayload
                {
                    RunId = _runId,
                    Delta = delta,
                    After = _gold
                });
            }
        }
        catch { }
        return true;
    }
}


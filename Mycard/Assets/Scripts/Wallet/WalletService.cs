using UnityEngine;
using System;

// 플레이어 골드의 단일 출입구. DB-우선 전략으로 일관성 보장.
public sealed class WalletService : IWalletService
{
    private readonly IDatabase _db;
    private string _runId;
    private int _gold;

    public event Action<int> OnGoldChanged;
    public int Gold => _gold;

    public WalletService(IDatabase db, string runId)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        RebindRun(runId);
    }

    // 새로운 런으로 지갑 상태를 재설정하고 DB에서 최신 골드를 불러옵니다.
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

    // DB를 먼저 갱신하고 성공 시 메모리/브로드캐스트를 수행합니다.
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
        // Broadcast gold change for achievements/progression hooks
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


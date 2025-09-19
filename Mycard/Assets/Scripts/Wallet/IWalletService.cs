using System;

/// <summary>
/// 런 진행 중 플레이어 골드를 DB-우선으로 관리하고 변경 이벤트를 제공하는 서비스 계약입니다.
/// </summary>
public interface IWalletService
{
    int Gold { get; }
    event Action<int> OnGoldChanged;

    bool TrySpend(int amount);
    void Add(int amount);

    /// <summary>
    /// DB를 먼저 갱신한 뒤 성공 시 메모리 값과 이벤트를 갱신합니다.
    /// </summary>
    bool Set(int amount);

    /// <summary>
    /// 런 ID를 변경하고 DB에서 최신 골드를 다시 읽어옵니다.
    /// </summary>
    void RebindRun(string runId);
}

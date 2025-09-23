using Game.Save;
using UnityEngine;

/// <summary>
/// 씬 전환 시 런 캐시와 HUD를 최신 DB 상태로 다시 맞추기 위한 헬퍼입니다.
/// </summary>
public static class RunCacheSynchronizer
{
    public static void Sync()
    {
        string runId = GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId)
            ? GameContext.I.RunId
            : PlayerPrefs.GetString("lastRunId", string.Empty);
        if (string.IsNullOrEmpty(runId)) return;

        RunLoadResult runData = null;
        try
        {
            runData = DatabaseManager.Instance.LoadCurrentRun(runId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RunCacheSynchronizer] LoadCurrentRun failed: {e.Message}");
            return;
        }

        var run = runData?.Run;
        if (run == null) return;

        // 이벤트 매니저 캐시 새로고침
        if (ServiceRegistry.Get<IEventManager>() is EventManager em)
        {
            em.RebindRunCache(run);
        }

        // 지갑 서비스 재바인딩
        ServiceRegistry.Get<IWalletService>()?.RebindRun(runId);

        // HUD 새로고침
        ServiceRegistry.Get<RunStatOverlay>()?.RefreshFallback();
    }
}

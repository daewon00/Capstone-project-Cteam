using System;
using UnityEngine;

/// <summary>
/// 진행 중인 런의 생성/삭제와 관련된 PlayerPrefs 및 서비스 상태를 정리하는 구현체입니다.
/// </summary>
public sealed class RunLifecycleService : IRunLifecycleService
{
    private readonly IDatabase _database;
    private const string LogTag = "[RunLifecycleService]";

    public RunLifecycleService(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool HasActiveRun()
    {
        var runId = GetActiveRunId();
        if (string.IsNullOrEmpty(runId)) return false;

        try
        {
            var loaded = _database.LoadCurrentRun(runId);
            return loaded?.Run != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} HasActiveRun check failed: {e.Message}");
            return false;
        }
    }

    public string GetActiveRunId()
    {
        if (GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId))
        {
            return GameContext.I.RunId;
        }

        return PlayerPrefs.GetString("lastRunId", string.Empty);
    }

    public void ResetActiveRun()
    {
        var runId = GetActiveRunId();
        if (string.IsNullOrEmpty(runId))
        {
            ClearPrefsAndContext();
            return;
        }

        SafeExecute(() => _database.DeleteActiveEventSession(runId), nameof(IDatabase.DeleteActiveEventSession));
        SafeExecute(() => _database.DeleteActiveShopSession(runId), nameof(IDatabase.DeleteActiveShopSession));
        SafeExecute(() => _database.DeleteActiveBattleState(runId), nameof(IDatabase.DeleteActiveBattleState));
        SafeExecute(() => _database.DeleteRunStageState(runId), nameof(IDatabase.DeleteRunStageState));
        SafeExecute(() => _database.DeleteMapLayout(runId), nameof(IDatabase.DeleteMapLayout));
        SafeExecute(() => _database.DeleteCurrentRun(runId), nameof(IDatabase.DeleteCurrentRun));

        var stageService = ServiceRegistry.Get<IRunStageService>();
        stageService?.ClearStage();
        stageService?.RebindRun(string.Empty);

        var walletService = ServiceRegistry.Get<IWalletService>();
        walletService?.RebindRun(string.Empty);

        var deckService = ServiceRegistry.Get<IDeckService>();
        deckService?.LoadAndPrepareDeck(string.Empty);

        var runService = ServiceRegistry.Get<IRunService>();
        runService?.RebindRun(string.Empty);

        ServiceRegistry.Get<IModifierService>()?.RebindRun(string.Empty);

        // 이벤트 매니저는 런별로 생성되므로 해제합니다.
        ServiceRegistry.Register<IEventManager>(null);

        ClearPrefsAndContext();
    }

    public void RegisterNewRun(string runId, string companionId)
    {
        if (string.IsNullOrEmpty(runId))
        {
            Debug.LogWarning($"{LogTag} RegisterNewRun called with empty runId.");
            return;
        }

        if (GameContext.I != null)
        {
            GameContext.I.RunId = runId;
            if (!string.IsNullOrEmpty(companionId))
            {
                GameContext.I.SelectedCompanionId = companionId;
            }
        }

        PlayerPrefs.SetString("lastRunId", runId);
        if (!string.IsNullOrEmpty(companionId))
        {
            PlayerPrefs.SetString("selectedCompanionId", companionId);
        }
        else
        {
            PlayerPrefs.DeleteKey("selectedCompanionId");
        }

        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} PlayerPrefs.Save failed: {e.Message}");
        }
    }

    private static void ClearPrefsAndContext()
    {
        if (GameContext.I != null)
        {
            GameContext.I.RunId = string.Empty;
            GameContext.I.SelectedCompanionId = string.Empty;
        }

        PlayerPrefs.DeleteKey("lastRunId");
        PlayerPrefs.DeleteKey("selectedCompanionId");

        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} PlayerPrefs.Save failed while clearing: {e.Message}");
        }
    }

    private static void SafeExecute(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} {label} threw: {e.Message}");
        }
    }
}

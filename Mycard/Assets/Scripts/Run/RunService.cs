using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Save;

/// <summary>
/// 전투 종료 결과에 따라 보상, 런 요약, 스테이지 전환을 처리하는 서비스 구현체입니다.
/// </summary>
public class RunService : IRunService
{
    private readonly IDatabase _database;
    private readonly IRngService _rngService;
    private readonly ICardCatalog _cardCatalog;

    private string _runId;
    private bool _hasCommitted = false;

    public event Action OnRunEnded;

    /// <summary>
    /// 런 서비스에 필요한 의존성을 주입합니다.
    /// </summary>
    public RunService(IDatabase database, IRngService rngService, ICardCatalog cardCatalog)
    {
        _database = database;
        _rngService = rngService;
        _cardCatalog = cardCatalog;
    }

    /// <summary>
    /// 현재 작업 중인 런 ID를 바인딩하고 커밋 상태를 초기화합니다.
    /// </summary>
    public void RebindRun(string runId)
    {
        _runId = runId ?? string.Empty;
        _hasCommitted = false;
        Debug.Log($"[RunService] Rebound to Run ID: {_runId}");

        var tutorialService = ServiceRegistry.Get<ITutorialService>();
        if (tutorialService != null)
        {
            bool isTutorialRun = false;
            if (!string.IsNullOrEmpty(_runId))
            {
                try
                {
                    var row = _database.LoadCurrentRun(_runId);
                    isTutorialRun = row?.Run?.IsTutorialRun ?? false;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RunService] Failed to query tutorial state: {e.Message}");
                }
            }

            tutorialService.BindRun(_runId, isTutorialRun);
        }
    }

    /// <summary>
    /// 전투 종료 결과를 보고받아 승리/패배 처리 로직을 실행합니다.
    /// </summary>
    public void ReportCombatEnded(CombatResult result)
    {
        if (_hasCommitted)
        {
            Debug.LogWarning("[RunService] Combat result already committed. Ignoring.");
            return;
        }
        _hasCommitted = true;

        switch (result)
        {
            case CombatResult.Victory:
                ProcessVictory();
                break;
            case CombatResult.Defeat:
                ProcessDefeat();
                break;
        }
    }

    /// <summary>
    /// 전투 승리 시 보상 생성 또는 런 클리어 처리를 수행합니다.
    /// </summary>
    private void ProcessVictory()
    {
        Debug.Log("[RunService] Processing VICTORY...");

        var lr = _database.LoadCurrentRun(_runId);
        if (lr == null || lr.Run == null)
        {
            Debug.LogError($"[RunService] Failed to load run {_runId}. Did you start the battle scene directly or finish the run earlier? Routing to Main Menu.");
            try { SceneManager.LoadScene("Main Menu"); } catch { }
            return;
        }

        var run = lr.Run;
        var stageService = ServiceRegistry.Get<IRunStageService>();

        // 보스 전투 승리 시: 즉시 런 클리어 처리로 분기 (GameContext가 없으면 PlayerPrefs 폴백)
        var battleKind = GameContext.I != null
            ? GameContext.I.CurrentBattleKind
            : (GameContext.BattleKind)PlayerPrefs.GetInt("currentBattleKind", (int)GameContext.BattleKind.Normal);
        Debug.Log($"[BossFlow][RunService] ProcessVictory: battleKind={battleKind}, runId={_runId}");
        if (battleKind == GameContext.BattleKind.Boss)
        {
            // 전투 승리 이벤트를 메타 이벤트 허브에 방송합니다.
            try
            {
                MetaEvents.RaiseCombatVictory(new MetaEvents.CombatVictoryPayload
                {
                    RunId = _runId,
                    Act = run.Act,
                    Floor = run.Floor,
                    NodeIndex = run.NodeIndex
                });
            }
            catch { }

            ServiceRegistry.Get<ITutorialService>()?.CompleteTutorial(TutorialIds.CoreOnboarding);

            // 런 종료 요약을 작성하고 클리어 상태로 저장합니다.
            var summary = new RunSummary
            {
                RunId = _runId,
                ProfileId = run.ProfileId ?? "default_profile",
                Cleared = true,
                EndedAtUtc = DateTime.UtcNow.ToString("o")
            };
            Debug.Log("[BossFlow][RunService] EndRunAndSummarize(Cleared=true)");
            _database.EndRunAndSummarize(summary);
            stageService?.ClearStage();

            // 런 종료 이벤트를 방송해 업적/진행도를 갱신합니다.
            try
            {
                MetaEvents.RaiseRunEnded(new MetaEvents.RunEndedPayload
                {
                    RunId = _runId,
                    ProfileId = summary.ProfileId,
                    Cleared = true,
                    DurationSeconds = summary.DurationSeconds
                });
            }
            catch { }

            // 에디터 배치형 UGUI(씬의 RunClearedView)가 MetaEvents.OnRunEnded를 수신해 스스로 표시합니다.
            Debug.Log("[BossFlow][RunService] Broadcast done. Expect RunClearedView in scene to activate.");
            // 저장된 전투 종류 태그를 정리합니다.
            try { PlayerPrefs.DeleteKey("currentBattleKind"); PlayerPrefs.Save(); } catch { }
            return;
        }
        var nodeState = lr.Nodes?.FirstOrDefault(n =>
            n.Act == run.Act && n.Floor == run.Floor && n.NodeIndex == run.NodeIndex)
            ?? new MapNodeState { RunId = _runId, Act = run.Act, Floor = run.Floor, NodeIndex = run.NodeIndex };

        // 보상 생성 도메인이 초기화되지 않았다면 현재 런 ID를 기반으로 시드합니다.
        TryEnsureSeeded("reward-generation");

        var rewardContainer = GenerateRewards();
        nodeState.RewardsJson = JsonUtility.ToJson(rewardContainer);
        nodeState.Cleared = true;

        _database.UpsertNodeState(nodeState);
        _database.UpsertRngStates(_runId, _rngService.GetStatesForSave());

        stageService?.SetStage(RunStageType.Reward, string.Empty, RunStageService.ToJson(new RunStagePayloads.Reward
        {
            act = run.Act,
            floor = run.Floor,
            nodeIndex = run.NodeIndex
        }));

        // 일반 전투 승리 이벤트도 방송해 업적/진행도를 갱신합니다.
        try
        {
            MetaEvents.RaiseCombatVictory(new MetaEvents.CombatVictoryPayload
            {
                RunId = _runId,
                Act = run.Act,
                Floor = run.Floor,
                NodeIndex = run.NodeIndex
            });
        }
        catch { }

        ServiceRegistry.Get<ITutorialService>()?.NotifyBattleCompleted();

        Debug.Log("[RunService] Node cleared. Rewards saved. Transitioning to Map Scene.");
        RunCacheSynchronizer.Sync();
        var nextMapScene = PlayerPrefs.GetString("lastMapScene", "Map Scene");
        if (string.IsNullOrEmpty(nextMapScene))
        {
            nextMapScene = "Map Scene";
        }
        SceneManager.LoadScene(nextMapScene);
    }

    /// <summary>
    /// 보상 생성 전에 RNG 도메인이 초기화되어 있는지 확인하고 필요 시 시드합니다.
    /// </summary>
    private void TryEnsureSeeded(string domain)
    {
        if (_rngService == null) return;
        try { _rngService.NextUInt(domain); }
        catch (InvalidOperationException)
        {
            _rngService.Seed(domain, HashRunIdToSeed(_runId, domain));
        }
    }

    /// <summary>
    /// 런 ID와 도메인을 조합해 안정적인 시드 값을 생성합니다.
    /// </summary>
    private static uint HashRunIdToSeed(string runId, string domain)
    {
        unchecked
        {
            uint h = 2166136261u; // FNV-1a 기준값
            if (!string.IsNullOrEmpty(runId))
            {
                foreach (char c in runId) { h ^= c; h *= 16777619u; }
            }
            if (!string.IsNullOrEmpty(domain))
            {
                foreach (char c in domain) { h ^= c; h *= 16777619u; }
            }
            return h == 0u ? 1u : h;
        }
    }

    /// <summary>
    /// 전투 승리 보상 컨테이너를 생성합니다.
    /// </summary>
    private RewardContainer GenerateRewards()
    {
        var container = new RewardContainer();
        int goldAmount = _rngService.NextInt("reward-generation", 80, 121);
        container.Items.Add(new RewardItem { Type = "Gold", Amount = goldAmount });

        // v2.0: 카드 선택지 3장을 생성하며 중복을 방지합니다.
        try
        {
            var allIds = _cardCatalog?.GetAllCardIds();
            if (allIds != null && allIds.Count > 0)
            {
                var cards = new System.Collections.Generic.List<CardScriptableObject>(allIds.Count);
                foreach (var id in allIds)
                {
                    var so = _cardCatalog.GetCardData(id);
                    if (so == null || so.removeAfterCombat)
                        continue;
                    cards.Add(so);
                }

                if (cards.Count > 0)
                {
                    var picker = new WeightedCardPicker(cards);
                    var exclude = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    System.Func<float> nextFloat = () => _rngService.NextFloat("reward-generation");
                    System.Func<int, int> nextInt = max => _rngService.NextInt("reward-generation", 0, max);
                    var selected = picker.PickMany(CardAcquisitionContext.Reward, 3, nextFloat, nextInt, exclude);

                    foreach (var card in selected)
                    {
                        container.SelectableCards.Add(new RewardCardOption
                        {
                            CardId = card.CardId,
                            IsUpgraded = false,
                            Rarity = card.Rarity
                        });
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RunService] 카드 보상 생성 실패: {e.Message}");
        }
        return container;
    }

    /// <summary>
    /// 전투 패배 시 런 요약을 작성하고 메인 메뉴로 이동합니다.
    /// </summary>
    private void ProcessDefeat()
    {
        Debug.Log("[RunService] Processing DEFEAT...");

        var lr = _database.LoadCurrentRun(_runId);
        var summary = new RunSummary
        {
            RunId = _runId,
            ProfileId = lr?.Run?.ProfileId ?? "default_profile",
            Cleared = false,
            EndedAtUtc = DateTime.UtcNow.ToString("o")
        };
        _database.EndRunAndSummarize(summary);
        ServiceRegistry.Get<IRunStageService>()?.ClearStage();

        // 패배 상태의 런 종료 이벤트를 방송합니다.
        try
        {
            MetaEvents.RaiseRunEnded(new MetaEvents.RunEndedPayload
            {
                RunId = _runId,
                ProfileId = summary.ProfileId,
                Cleared = false,
                DurationSeconds = summary.DurationSeconds
            });
        }
        catch { }

        Debug.Log($"[RunService] Run {_runId} ended. Firing OnRunEnded and transitioning to Main Menu.");
        OnRunEnded?.Invoke();
        // 패배로 런이 종료되었으므로 이어하기 키를 정리해 혼선을 방지합니다.
        try { PlayerPrefs.DeleteKey("lastRunId"); PlayerPrefs.Save(); } catch { }
        SceneManager.LoadScene("Main Menu");
    }
}

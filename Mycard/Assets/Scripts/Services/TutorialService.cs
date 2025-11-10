using System;
using System.Collections.Generic;
using Game.Save;
using UnityEngine;

/// <summary>
/// 튜토리얼 시퀀스를 로드하고 단계 진행, 하이라이트, 입력 제한 정보를 관리합니다.
/// </summary>
public sealed class TutorialService : ITutorialService
{
    private const string DefaultSequencePath = "Tutorials/CoreOnboarding";

    private readonly IDatabase _database;
    private readonly Dictionary<TutorialStep, TutorialStepConfig> _stepLookup = new();
    private readonly Dictionary<string, TutorialTarget> _targets = new(StringComparer.OrdinalIgnoreCase);

    private string _profileId = "P1";
    private string _activeTutorialId;
    private TutorialProgress _progress;
    private TutorialSequenceDefinition _sequence;
    private int _currentIndex = -1; // steps 배열 기준 진행 커서(0-based). 완료 시 steps.Length 이상.

    [Serializable]
    private class Flags
    {
        public int seqIndex;
        public int schema;
    }

    private string _activeRunId = string.Empty;
    private bool _isTutorialRun;
    private bool _isPreviewMode;

    public event Action<TutorialStep> OnStepChanged;
    public event Action<TutorialStepConfig, RectTransform> OnStepVisualChanged;

    public bool IsActive => (_isTutorialRun || _isPreviewMode) && _progress != null && !_progress.IsCompleted;
    public bool IsTutorialRun => _isTutorialRun;
    public string ActiveTutorialId => _activeTutorialId;
    public TutorialStep CurrentStep
    {
        get
        {
            var cfg = GetConfigAtIndex(_currentIndex);
            if (cfg != null) return cfg.Step;
            if (_progress != null && _progress.IsCompleted) return TutorialStep.Completed;
            return TutorialStep.None;
        }
    }
    public TutorialStepConfig CurrentConfig { get; private set; }
    public RectTransform CurrentHighlight { get; private set; }
    public bool CanAdvanceViaOverlay => IsActive
                                        && CurrentConfig != null
                                        && CurrentConfig.AllowTapToContinue
                                        && (CurrentConfig.RequiredAction == TutorialRequiredActionType.None
                                            || CurrentConfig.RequiredAction == TutorialRequiredActionType.ShowOverlayOnly);

    public TutorialService(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        LoadSequence(DefaultSequencePath);
    }

    public void RebindProfile(string profileId)
    {
        _profileId = string.IsNullOrEmpty(profileId) ? "P1" : profileId;
        LoadProgress(TutorialIds.CoreOnboarding);
    }

    public bool BeginTutorialIfNeeded(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId))
        {
            return false;
        }

        _isPreviewMode = false;
        EnsureSequence(tutorialId);
        LoadProgress(tutorialId);

        if (_progress == null)
        {
            CreateProgressRow(tutorialId);
        }

        if (_progress.IsCompleted)
        {
            return false;
        }

        _activeTutorialId = tutorialId;
        var firstIndex = GetFirstIndex();
        if (_currentIndex < firstIndex)
        {
            SetIndex(firstIndex);
        }
        else
        {
            RefreshVisuals();
        }

        return true;
    }

    public bool BeginPreviewIfEligible(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId))
        {
            return false;
        }

        EnsureSequence(tutorialId);
        LoadProgress(tutorialId);

        if (_progress == null)
        {
            CreateProgressRow(tutorialId);
        }

        if (_progress.IsCompleted)
        {
            _isPreviewMode = false;
            return false;
        }

        _activeTutorialId = tutorialId;
        _isPreviewMode = true;

        _progress.CurrentStep = (int)TutorialStep.None;
        _currentIndex = -1;
        SetIndex(GetFirstIndex());

        return true;
    }

    public void BindRun(string runId, bool isTutorialRun)
    {
        _activeRunId = runId ?? string.Empty;
        _isTutorialRun = isTutorialRun && !string.IsNullOrEmpty(_activeRunId);
        _isPreviewMode = false;

        if (!_isTutorialRun)
        {
            _activeTutorialId = null;
            CurrentConfig = null;
            CurrentHighlight = null;
            OnStepVisualChanged?.Invoke(null, null);
            return;
        }

        if (_progress == null)
        {
            CreateProgressRow(TutorialIds.CoreOnboarding);
        }

        if (string.IsNullOrEmpty(_activeTutorialId))
        {
            _activeTutorialId = _progress.TutorialId;
        }

        RefreshVisuals();
    }

    public void NotifyBattleCompleted()
    {
        ReportAction(TutorialRequiredActionType.BattleCompleted, null);
    }

    public void NotifyMapNodeVisited(int act, int floor, int nodeIndex)
    {
        ReportAction(TutorialRequiredActionType.NodeTravel, FormatNodeContext(act, floor, nodeIndex));
    }

    public bool CanMoveToNode(int act, int floor, int nodeIndex)
    {
        if (!IsActive) return true;
        var cfg = CurrentConfig;
        if (cfg == null) return true;
        return cfg.IsNodeAllowed(act, floor, nodeIndex);
    }

    public void ReportAction(TutorialRequiredActionType actionType, string context = null)
    {
        if (!IsActive) return;
        var cfg = CurrentConfig;
        if (cfg == null || cfg.RequiredAction == TutorialRequiredActionType.None)
        {
            return;
        }

        if (!cfg.MatchesAction(actionType, context))
        {
            return;
        }

        AdvanceStep();
    }

    public bool TryAdvanceOverlayStep()
    {
        if (!CanAdvanceViaOverlay)
        {
            GameLog.Info($"[TutorialService] TryAdvanceOverlayStep blocked: isActive={IsActive} cfgNull={CurrentConfig==null} req={CurrentConfig?.RequiredAction} allowTap={CurrentConfig?.AllowTapToContinue}");
            return false;
        }

        GameLog.Info($"[TutorialService] TryAdvanceOverlayStep advancing: step={CurrentStep}");
        AdvanceStep();
        return true;
    }

    public void CompleteTutorial(string tutorialId)
    {
        if (_progress == null || string.IsNullOrEmpty(tutorialId))
        {
            return;
        }

        if (!string.Equals(_progress.TutorialId, tutorialId, StringComparison.Ordinal))
        {
            return;
        }

        if (_progress.IsCompleted)
        {
            return;
        }

        _progress.IsCompleted = true;
        PersistProgress();
        _isTutorialRun = false;
        _isPreviewMode = false;
        _activeTutorialId = null;

        CurrentConfig = null;
        CurrentHighlight = null;
        OnStepChanged?.Invoke(TutorialStep.Completed);
        OnStepVisualChanged?.Invoke(null, null);

        if (!string.IsNullOrEmpty(_activeRunId))
        {
            try
            {
                var load = _database.LoadCurrentRun(_activeRunId);
                if (load?.Run != null && load.Run.IsTutorialRun)
                {
                    load.Run.IsTutorialRun = false;
                    load.Run.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                    _database.UpsertCurrentRun(load.Run);
                }
            }
            catch (Exception e)
            {
                GameLog.Warn($"[TutorialService] Failed to clear tutorial flag on run: {e.Message}");
            }
        }
    }

    public bool HasCompleted(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId))
        {
            return false;
        }

        if (_progress == null || !string.Equals(_progress.TutorialId, tutorialId, StringComparison.Ordinal))
        {
            LoadProgress(tutorialId);
        }

        return _progress != null && _progress.IsCompleted;
    }

    public void ResetActiveRun()
    {
        _activeRunId = string.Empty;
        _isTutorialRun = false;
        _isPreviewMode = false;
        _activeTutorialId = null;
        CurrentConfig = null;
        CurrentHighlight = null;
        OnStepVisualChanged?.Invoke(null, null);
    }

    public void RegisterTarget(TutorialTarget target)
    {
        if (target == null || string.IsNullOrEmpty(target.TargetId))
        {
            return;
        }

        _targets[target.TargetId] = target;
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[TutorialService] RegisterTarget id='{target.TargetId}' focusRect={(target.FocusRect!=null?target.FocusRect.name:"<null>")} currentStep={CurrentStep} currentHighlightId='{CurrentConfig?.HighlightTargetId}'");
        #endif
        if (CurrentConfig != null && string.Equals(CurrentConfig.HighlightTargetId, target.TargetId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshVisuals();
        }
    }

    public void UnregisterTarget(TutorialTarget target)
    {
        if (target == null || string.IsNullOrEmpty(target.TargetId))
        {
            return;
        }

        if (_targets.TryGetValue(target.TargetId, out var existing) && existing == target)
        {
            _targets.Remove(target.TargetId);
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Info($"[TutorialService] UnregisterTarget id='{target.TargetId}'");
            #endif
            if (CurrentConfig != null && string.Equals(CurrentConfig.HighlightTargetId, target.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                RefreshVisuals();
            }
        }
    }

    public RectTransform GetTargetRect(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;
        if (_targets.TryGetValue(targetId, out var target) && target != null)
        {
            return target.FocusRect;
        }
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[TutorialService] GetTargetRect MISS id='{targetId}'. Registered={_targets.Count}");
        #endif
        return null;
    }

    private void AdvanceStep()
    {
        AdvanceIndex();
    }

    private void SetStep(TutorialStep newStep)
    {
        if (_progress == null) return;
        if (newStep == TutorialStep.None) return;

        int idx = IndexOfStep(newStep);
        if (idx < 0)
        {
            GameLog.Warn($"[TutorialService] No configuration found for step {newStep}. Skipping.");
            return;
        }
        SetIndex(idx);
    }

    private void RefreshVisuals()
    {
        var cfg = GetConfigAtIndex(_currentIndex);
        CurrentConfig = cfg;
        CurrentHighlight = ResolveHighlight(cfg);
        OnStepVisualChanged?.Invoke(cfg, CurrentHighlight);
    }

    private TutorialStepConfig GetConfig(TutorialStep step)
    {
        if (_stepLookup.TryGetValue(step, out var cfg) && cfg != null)
        {
            return cfg;
        }

        if (_sequence == null) return null;
        var resolved = _sequence.GetStep(step);
        _stepLookup[step] = resolved;
        return resolved;
    }

    private TutorialStep GetFirstStep()
    {
        if (_sequence == null) return TutorialStep.CompanionSelection;
        var steps = _sequence.Steps;
        if (steps == null || steps.Length == 0) return TutorialStep.CompanionSelection;
        // 에셋에 정의된 순서를 우선합니다: 첫 번째 유효 항목 반환
        foreach (var cfg in steps)
        {
            if (cfg == null) continue;
            if (cfg.Step != TutorialStep.None)
            {
                return cfg.Step;
            }
        }
        return TutorialStep.CompanionSelection;
    }

    private TutorialStep GetNextStep(TutorialStep current)
    {
        if (_sequence == null) return TutorialStep.Completed;
        return _sequence.GetNextStep(current);
    }

    private int GetFirstIndex()
    {
        var steps = _sequence?.Steps;
        if (steps == null || steps.Length == 0) return 0;
        for (int i = 0; i < steps.Length; i++)
        {
            var cfg = steps[i];
            if (cfg != null && cfg.Step != TutorialStep.None)
            {
                return i;
            }
        }
        return 0;
    }

    private int GetNextIndex(int current)
    {
        var steps = _sequence?.Steps;
        if (steps == null) return -1;
        for (int i = current + 1; i < steps.Length; i++)
        {
            if (steps[i] != null) return i;
        }
        return steps.Length;
    }

    private TutorialStepConfig GetConfigAtIndex(int index)
    {
        var steps = _sequence?.Steps;
        if (steps == null || index < 0 || index >= steps.Length) return null;
        return steps[index];
    }

    private int IndexOfStep(TutorialStep step)
    {
        var steps = _sequence?.Steps;
        if (steps == null) return -1;
        for (int i = 0; i < steps.Length; i++)
        {
            var cfg = steps[i];
            if (cfg != null && cfg.Step == step) return i;
        }
        return -1;
    }

    private void AdvanceIndex()
    {
        var steps = _sequence?.Steps;
        if (steps == null) return;
        var next = GetNextIndex(_currentIndex);
        if (next >= steps.Length || next < 0)
        {
            CompleteTutorial(_activeTutorialId);
        }
        else
        {
            SetIndex(next);
        }
    }

    private void SetIndex(int newIndex)
    {
        if (_progress == null) return;
        var steps = _sequence?.Steps;
        if (steps == null || newIndex < 0)
        {
            return;
        }

        if (newIndex >= steps.Length)
        {
            CompleteTutorial(_activeTutorialId);
            return;
        }

        var cfg = steps[newIndex];
        if (cfg == null)
        {
            int idx = GetNextIndex(newIndex - 1);
            if (idx >= steps.Length) { CompleteTutorial(_activeTutorialId); return; }
            newIndex = idx;
            cfg = steps[newIndex];
        }

        if (newIndex <= _currentIndex)
        {
            RefreshVisuals();
            return;
        }

        _currentIndex = newIndex;
        PersistProgress();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[TutorialService] Index -> {_currentIndex} step={cfg.Step} (run={_activeRunId})");
#endif

        OnStepChanged?.Invoke(cfg.Step);
        RefreshVisuals();

        if (cfg.AutoAdvance)
        {
            AdvanceIndex();
        }
    }

    private RectTransform ResolveHighlight(TutorialStepConfig cfg)
    {
        if (cfg == null || string.IsNullOrEmpty(cfg.HighlightTargetId))
        {
            return null;
        }

        if (_targets.TryGetValue(cfg.HighlightTargetId, out var target) && target != null)
        {
            return target.FocusRect;
        }

        return null;
    }

    private void LoadSequence(string resourcePath)
    {
        _sequence = Resources.Load<TutorialSequenceDefinition>(resourcePath);
        if (_sequence == null)
        {
            GameLog.Warn($"[TutorialService] Tutorial sequence not found at Resources/{resourcePath}. Using empty configuration.");
        }
        _stepLookup.Clear();
    }

    private void EnsureSequence(string tutorialId)
    {
        if (_sequence != null && string.Equals(_sequence.TutorialId, tutorialId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LoadSequence($"Tutorials/{tutorialId}");
        if (_sequence == null || !string.Equals(_sequence.TutorialId, tutorialId, StringComparison.OrdinalIgnoreCase))
        {
            LoadSequence(DefaultSequencePath);
        }
    }

    private void LoadProgress(string tutorialId)
    {
        if (_database == null)
        {
            return;
        }

        var loaded = _database.LoadTutorialProgress(_profileId, tutorialId);
        if (loaded != null)
        {
            _progress = loaded;
            // 인덱스 복원: FlagsJson(seqIndex) → 없으면 CurrentStep 매핑
            _currentIndex = TryParseSeqIndex(_progress.FlagsJson);
            if (_currentIndex < 0)
            {
                var steps = _sequence?.Steps;
                if (steps != null)
                {
                    int idx = -1;
                    var savedStep = (TutorialStep)_progress.CurrentStep;
                    for (int i = 0; i < steps.Length; i++)
                    {
                        if (steps[i] != null && steps[i].Step == savedStep) { idx = i; break; }
                    }
                    _currentIndex = idx;
                }
            }
        }
    }

    private void CreateProgressRow(string tutorialId)
    {
        _progress = new TutorialProgress
        {
            ProfileId = _profileId,
            TutorialId = tutorialId,
            CurrentStep = (int)TutorialStep.None,
            IsCompleted = false,
            FlagsJson = string.Empty,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o")
        };

        PersistProgress();
    }

    private void PersistProgress()
    {
        if (_database == null || _progress == null)
        {
            return;
        }

        _progress.ProfileId = _profileId;
        _progress.UpdatedAtUtc = DateTime.UtcNow.ToString("o");

        // 동기화: CurrentStep(하위호환), FlagsJson(seqIndex)
        var cfg = GetConfigAtIndex(_currentIndex);
        if (cfg != null)
        {
            _progress.CurrentStep = (int)cfg.Step;
        }
        var flags = new Flags { seqIndex = _currentIndex, schema = 1 };
        try
        {
            _progress.FlagsJson = JsonUtility.ToJson(flags);
        }
        catch
        {
            _progress.FlagsJson = $"{{\"seqIndex\":{_currentIndex},\"schema\":1}}";
        }

        _database.UpsertTutorialProgress(_progress);
    }

    private static string FormatNodeContext(int act, int floor, int nodeIndex)
    {
        return $"{act}:{floor}:{nodeIndex}";
    }

    private static int TryParseSeqIndex(string flagsJson)
    {
        if (string.IsNullOrEmpty(flagsJson)) return -1;
        try
        {
            var f = JsonUtility.FromJson<Flags>(flagsJson);
            return f != null ? f.seqIndex : -1;
        }
        catch
        {
            return -1;
        }
    }
}

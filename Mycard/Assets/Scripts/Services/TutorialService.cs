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

    private string _activeRunId = string.Empty;
    private bool _isTutorialRun;
    private bool _isPreviewMode;

    public event Action<TutorialStep> OnStepChanged;
    public event Action<TutorialStepConfig, RectTransform> OnStepVisualChanged;

    public bool IsActive => (_isTutorialRun || _isPreviewMode) && _progress != null && !_progress.IsCompleted;
    public bool IsTutorialRun => _isTutorialRun;
    public string ActiveTutorialId => _activeTutorialId;
    public TutorialStep CurrentStep => _progress == null ? TutorialStep.None : (TutorialStep)_progress.CurrentStep;
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
        var firstStep = GetFirstStep();
        _progress.CurrentStep = (int)TutorialStep.None;
        SetStep(firstStep);

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
        SetStep(GetFirstStep());

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

        if (_progress.CurrentStep == (int)TutorialStep.None)
        {
            SetStep(GetFirstStep());
        }
        else
        {
            RefreshVisuals();
        }
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
            return false;
        }

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
                Debug.LogWarning($"[TutorialService] Failed to clear tutorial flag on run: {e.Message}");
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
            if (CurrentConfig != null && string.Equals(CurrentConfig.HighlightTargetId, target.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                RefreshVisuals();
            }
        }
    }

    private void AdvanceStep()
    {
        var nextStep = GetNextStep(CurrentStep);
        if (nextStep == TutorialStep.Completed)
        {
            CompleteTutorial(_activeTutorialId);
        }
        else
        {
            SetStep(nextStep);
        }
    }

    private void SetStep(TutorialStep newStep)
    {
        if (_progress == null) return;
        if (newStep == TutorialStep.None) return;

        var cfg = GetConfig(newStep);
        if (cfg == null)
        {
            Debug.LogWarning($"[TutorialService] No configuration found for step {newStep}. Skipping.");
            return;
        }

        int value = (int)newStep;
        if (value <= _progress.CurrentStep)
        {
            RefreshVisuals();
            return;
        }

        _progress.CurrentStep = value;
        PersistProgress();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TutorialService] Step -> {newStep} (run={_activeRunId})");
#endif

        OnStepChanged?.Invoke(newStep);
        RefreshVisuals();

        if (cfg.AutoAdvance)
        {
            AdvanceStep();
        }
    }

    private void RefreshVisuals()
    {
        var cfg = GetConfig(CurrentStep);
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
        TutorialStep min = TutorialStep.Completed;
        foreach (var cfg in steps)
        {
            if (cfg == null) continue;
            if (cfg.Step != TutorialStep.None && cfg.Step < min)
            {
                min = cfg.Step;
            }
        }
        return min == TutorialStep.Completed ? TutorialStep.CompanionSelection : min;
    }

    private TutorialStep GetNextStep(TutorialStep current)
    {
        if (_sequence == null) return TutorialStep.Completed;
        return _sequence.GetNextStep(current);
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
            Debug.LogWarning($"[TutorialService] Tutorial sequence not found at Resources/{resourcePath}. Using empty configuration.");
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
        _database.UpsertTutorialProgress(_progress);
    }

    private static string FormatNodeContext(int act, int floor, int nodeIndex)
    {
        return $"{act}:{floor}:{nodeIndex}";
    }
}

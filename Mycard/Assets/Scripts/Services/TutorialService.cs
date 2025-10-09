using System;
using Game.Save;
using UnityEngine;

/// <summary>
/// 코어 온보딩 튜토리얼의 활성 여부와 단계를 관리합니다.
/// </summary>
public sealed class TutorialService : ITutorialService
{
    private readonly IDatabase _database;

    private string _profileId = "P1";
    private string _activeTutorialId;
    private TutorialProgress _progress;

    private string _activeRunId = string.Empty;
    private bool _isTutorialRun;

    public event Action<TutorialStep> OnStepChanged;

    public bool IsActive => _isTutorialRun && _progress != null && !_progress.IsCompleted;
    public bool IsTutorialRun => _isTutorialRun;
    public string ActiveTutorialId => _activeTutorialId;
    public TutorialStep CurrentStep => _progress == null ? TutorialStep.None : (TutorialStep)_progress.CurrentStep;

    public TutorialService(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
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

        if (_progress.CurrentStep < (int)TutorialStep.CompanionSelection)
        {
            SetStep(TutorialStep.CompanionSelection);
        }

        return true;
    }

    public void BindRun(string runId, bool isTutorialRun)
    {
        _activeRunId = runId ?? string.Empty;
        _isTutorialRun = isTutorialRun && !string.IsNullOrEmpty(_activeRunId);

        if (!_isTutorialRun)
        {
            _activeTutorialId = null;
            return;
        }

        if (_progress == null)
        {
            // 튜토리얼 런이지만 프로그레스가 없는 경우 기본 행을 만들고 시작합니다.
            CreateProgressRow(TutorialIds.CoreOnboarding);
        }

        if (string.IsNullOrEmpty(_activeTutorialId))
        {
            _activeTutorialId = _progress.TutorialId;
        }

        if (_progress.CurrentStep < (int)TutorialStep.FirstBattlePending)
        {
            SetStep(TutorialStep.FirstBattlePending);
        }
    }

    public void NotifyBattleCompleted()
    {
        if (!IsActive)
        {
            return;
        }

        if (_progress.CurrentStep < (int)TutorialStep.FirstBattleCompleted)
        {
            SetStep(TutorialStep.FirstBattleCompleted);
        }

        if (_progress.CurrentStep < (int)TutorialStep.MapMovePending)
        {
            SetStep(TutorialStep.MapMovePending);
        }
    }

    public void NotifyMapNodeVisited(int act, int floor, int nodeIndex)
    {
        if (!IsActive)
        {
            return;
        }

        if (_progress.CurrentStep < (int)TutorialStep.MapMovePending)
        {
            return;
        }

        // 첫 노드 이동은 0층이 아닌 다른 층을 방문했을 때로 간주합니다.
        if (floor > 0 && _progress.CurrentStep < (int)TutorialStep.MapMoveCompleted)
        {
            SetStep(TutorialStep.MapMoveCompleted);
            CompleteTutorial(_activeTutorialId);
        }
    }

    public bool CanMoveToNode(int act, int floor, int nodeIndex)
    {
        if (!IsActive)
        {
            return true;
        }

        if (_progress.CurrentStep < (int)TutorialStep.MapMovePending)
        {
            return true;
        }

        // 맵 이동 단계에서는 1층 이상으로 이동하는 행동만 허용합니다.
        return floor > 0;
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
        SetStep(TutorialStep.Completed);
        PersistProgress();
        _isTutorialRun = false;
        _activeTutorialId = null;

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
        _activeTutorialId = null;
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

    private void SetStep(TutorialStep newStep)
    {
        if (_progress == null)
        {
            return;
        }

        int stepValue = (int)newStep;
        if (stepValue <= _progress.CurrentStep)
        {
            return;
        }

        _progress.CurrentStep = stepValue;
        PersistProgress();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TutorialService] Step advanced to {newStep} (run={_activeRunId})");
#endif
        OnStepChanged?.Invoke(newStep);
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
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Game.Save;

public class MainMenu : MonoBehaviour
{
    [Header("UI 연결")]
    public Button newGameButton;
    public Button continueButton;
    // ▼ 테스트용: 이어하기 데이터 삭제 버튼 추가
    public Button deleteSaveButton;

    [Header("씬 이름 연결")]
    public string companionSelectScene = "CompanionSelectScene";
    public string mapScene = "MapScene";

    [Header("Confirmation")]
    [SerializeField] private RunResetConfirmModal confirmModalPrefab;
    [SerializeField] private Transform modalParent;

    private const string ResetWarningMessage = "이미 진행 중인 런이 있습니다. 새 게임을 시작하면 해당 진행이 삭제됩니다. 계속하시겠습니까?";
    private RunResetConfirmModal _activeModal;

    void Start()
    {
        AudioManager.instance.PlayMenuMusic();//노래 시작

        // DB에 연결하여 저장된 게임이 있는지 확인합니다.
        DatabaseManager.Instance.Connect();

        // "이어하기" 데이터가 있는지 검사합니다.
        var runId = PlayerPrefs.GetString("lastRunId", "");
        bool hasContinueData = false;
        if (!string.IsNullOrEmpty(runId))
        {
            // DB에 정말로 해당 데이터가 있는지 한번 더 확인하여 안정성을 높입니다.
            var data = DatabaseManager.Instance.LoadCurrentRun(runId);
            hasContinueData = (data != null);
        }

        // 이어하기 데이터가 있을 때만 "Continue" 버튼을 보여줍니다.
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(hasContinueData);
        }

        // 버튼 클릭 시 어떤 함수를 실행할지 연결합니다.
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnClickNewGame);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnClickContinue);
        }
        if (deleteSaveButton != null)
        {
            deleteSaveButton.onClick.RemoveAllListeners();
            deleteSaveButton.onClick.AddListener(OnClickDeleteCurrentRun);
        }

        // 최초 UI 상태 갱신
        RefreshUI();
    }

    // 현재 이어하기 데이터 유무에 따라 버튼 표시/숨김
    void RefreshUI()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        bool hasContinueData = false;

        if (!string.IsNullOrEmpty(runId))
        {
            var data = DatabaseManager.Instance.LoadCurrentRun(runId);
            hasContinueData = (data != null);

            // 깨진 키 정리(혹시 DB에 없으면 Prefs에서 제거)
            if (!hasContinueData)
            {
                PlayerPrefs.DeleteKey("lastRunId");
                PlayerPrefs.Save();
            }
        }

        if (continueButton != null)
            continueButton.gameObject.SetActive(hasContinueData);

        // 삭제 버튼은 이어하기 있을 때만 노출
        if (deleteSaveButton != null)
            deleteSaveButton.gameObject.SetActive(hasContinueData);
    }

    // "새 게임" 버튼을 눌렀을 때
    void OnClickNewGame()
    {
        var lifecycle = ServiceRegistry.Get<IRunLifecycleService>();
        if (lifecycle != null && lifecycle.HasActiveRun())
        {
            if (confirmModalPrefab == null)
            {
                lifecycle.ResetActiveRun();
                SceneManager.LoadScene(companionSelectScene);
                return;
            }

            if (_activeModal != null) return;

            if (newGameButton != null)
            {
                newGameButton.interactable = false;
            }

            var parent = ResolveModalParent();
            _activeModal = Instantiate(confirmModalPrefab, parent, false);
            _activeModal.Show(
                ResetWarningMessage,
                () =>
                {
                    lifecycle.ResetActiveRun();
                    if (newGameButton != null) newGameButton.interactable = true;
                    _activeModal = null;
                    SceneManager.LoadScene(companionSelectScene);
                },
                () =>
                {
                    if (newGameButton != null) newGameButton.interactable = true;
                    _activeModal = null;
                });
            return;
        }

        lifecycle?.ResetActiveRun();
        SceneManager.LoadScene(companionSelectScene);
    }

    // "이어하기" 버튼을 눌렀을 때
    void OnClickContinue()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId))
        {
            SceneManager.LoadScene(mapScene);
            return;
        }

        RunStageState stageState = null;
        try
        {
            stageState = DatabaseManager.Instance.LoadRunStageState(runId);
        }
        catch (System.Exception e)
        {
            GameLog.Warn($"[MainMenu] Failed to load RunStageState: {e.Message}");
        }

        if (stageState != null)
        {
            var payloadPreview = string.IsNullOrEmpty(stageState.PayloadJson)
                ? "(empty)"
                : (stageState.PayloadJson.Length > 128 ? stageState.PayloadJson.Substring(0, 128) + "..." : stageState.PayloadJson);
            GameLog.Info($"[MainMenu] Continue stage={stageState.Stage}, sceneHint='{stageState.SceneHint}', payload={payloadPreview}");
        }
        else
        {
            GameLog.Info("[MainMenu] Continue stageState is null; defaulting to Map.");
        }

        var targetScene = mapScene;
        var stage = stageState != null ? stageState.Stage : RunStageType.Map;

        switch (stage)
        {
            case RunStageType.Event:
                if (!string.IsNullOrEmpty(stageState?.SceneHint)) targetScene = stageState.SceneHint;
                break;
            case RunStageType.Battle:
                if (!string.IsNullOrEmpty(stageState?.SceneHint)) targetScene = stageState.SceneHint;
                break;
            case RunStageType.BattlePending:
                targetScene = mapScene;
                break;
            case RunStageType.ShopOverlay:
            case RunStageType.Reward:
            case RunStageType.Map:
            case RunStageType.Unknown:
            default:
                targetScene = mapScene;
                break;
        }

        SceneManager.LoadScene(targetScene);
    }

    // ★ 테스트용: 현재 런 데이터 삭제
    void OnClickDeleteCurrentRun()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (!string.IsNullOrEmpty(runId))
        {
            // DB에서 현재 런 관련 테이블 레코드들 제거
            DatabaseManager.Instance.DeleteCurrentRun(runId);
            GameLog.Info($"[MainMenu] Deleted current run: {runId}");
        }

        // PlayerPrefs 키 정리
        PlayerPrefs.DeleteKey("lastRunId");
        PlayerPrefs.DeleteKey("selectedCompanionId");
        PlayerPrefs.Save();

        // UI 갱신(컨티뉴/삭제 버튼 숨김)
        RefreshUI();
    }

    //끝내기 버튼을 눌렀을 때
    public void QuitGame()
    {
        Application.Quit();

        GameLog.Info("Quit game");

        AudioManager.instance.PlaySFX(0);
    }

    Transform ResolveModalParent()
    {
        if (modalParent != null) return modalParent;

        var canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }
}

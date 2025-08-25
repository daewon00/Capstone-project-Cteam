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

    void Start()
    {
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
        // (선택사항) 만약 이어하기 데이터가 남아있다면,
        // "새 게임을 시작하면 이전 데이터가 지워집니다" 라는 경고창을 띄우고
        // 기존 런 데이터를 삭제하는 로직을 여기에 추가할 수 있습니다.

        SceneManager.LoadScene(companionSelectScene);
    }

    // "이어하기" 버튼을 눌렀을 때
    void OnClickContinue()
    {
        SceneManager.LoadScene(mapScene);
    }

    // ★ 테스트용: 현재 런 데이터 삭제
    void OnClickDeleteCurrentRun()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (!string.IsNullOrEmpty(runId))
        {
            // DB에서 현재 런 관련 테이블 레코드들 제거
            DatabaseManager.Instance.DeleteCurrentRun(runId);
            Debug.Log($"[MainMenu] Deleted current run: {runId}");
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

        Debug.Log("Quit game");

        AudioManager.instance.PlaySFX(0);
    }
}

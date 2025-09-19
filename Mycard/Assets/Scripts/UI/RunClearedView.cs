using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 런 클리어 시 메타 이벤트를 수신해 승리 패널을 표시하는 UGUI 컨트롤러입니다.
/// Battle_android 씬의 Canvas 하위에 배치하여 사용합니다.
/// </summary>
public class RunClearedView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootPanel;    // 전체 패널(켜고/끄는 대상)

    [Header("Optional UI")]
    [SerializeField] private TMP_Text titleText;      // "RUN CLEARED!" 등 제목 텍스트(선택)
    [SerializeField] private TMP_Text descText;       // 설명 텍스트(선택)
    [SerializeField] private TMP_Text listText;       // 새로 해금된 업적 목록을 단순 텍스트로 표시(선택)

    private void Awake()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        Debug.Log("[BossFlow][RunClearedView] Awake: subscribing to MetaEvents.OnRunEnded");
        MetaEvents.OnRunEnded += HandleRunEnded;
    }

    private void OnDestroy()
    {
        MetaEvents.OnRunEnded -= HandleRunEnded;
    }

    private void HandleRunEnded(MetaEvents.RunEndedPayload payload)
    {
        // 보스전 승리(클리어) 상황에서만 표시
        Debug.Log($"[BossFlow][RunClearedView] HandleRunEnded: cleared={payload.Cleared}, runId={payload.RunId}");
        if (!payload.Cleared) return;

        try
        {
            PopulateNewAchievements();
        }
        catch { /* UI 채우기 실패는 치명적이지 않음 */ }

        if (titleText != null) titleText.text = "RUN CLEARED!";
        if (descText != null) descText.text = "보스를 물리치고 런을 클리어했습니다!";
        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            Debug.Log("[BossFlow][RunClearedView] rootPanel.SetActive(true)");
        }
        else
        {
            gameObject.SetActive(true);
            Debug.Log("[BossFlow][RunClearedView] rootPanel is null; activated self GameObject");
        }
    }

    private void PopulateNewAchievements()
    {
        var svc = ServiceRegistry.Get<IAchievementService>();
        if (svc == null || listText == null) return;

        var newly = svc.GetNewlyUnlockedSinceLastFlush();
        if (newly == null || newly.Count == 0)
        {
            listText.text = "이번 런에서 새로 해금된 업적이 없습니다.";
            return;
        }

        var defs = svc.GetAllDefinitions();
        var map = new Dictionary<string, AchievementDefinition>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var d in defs)
        {
            if (d != null && !string.IsNullOrEmpty(d.Id)) map[d.Id] = d;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("이번에 해금된 업적");
        foreach (var id in newly)
        {
            if (map.TryGetValue(id, out var def))
            {
                sb.Append("• ").Append(def.DisplayName);
                if (!string.IsNullOrEmpty(def.Description))
                    sb.Append(" — ").Append(def.Description);
                sb.AppendLine();
            }
            else
            {
                sb.Append("• ").Append(id).AppendLine();
            }
        }
        listText.text = sb.ToString();
        Debug.Log($"[BossFlow][RunClearedView] Populated {newly.Count} newly unlocked achievements");
    }

    // UI의 '메인 메뉴로' 버튼에 연결
    public void OnClickReturnToMainMenu()
    {
        try { PlayerPrefs.DeleteKey("lastRunId"); PlayerPrefs.Save(); } catch { }
        SceneManager.LoadScene("Main Menu");
    }
}

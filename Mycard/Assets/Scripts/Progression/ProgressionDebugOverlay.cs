using UnityEngine;
using Game.Save;
using System.Linq;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

// 매번 세이브를 초기화하지 않고도 진행도를 테스트하기 위한 런타임 디버그 오버레이입니다.
// F10(에디터/개발 빌드) 또는 3손가락 터치로 토글합니다.
[DefaultExecutionOrder(10000)]
/// <summary>
/// 진행도 관련 테스트를 위한 디버그 오버레이 UI를 제공하고 위험 작업을 수행합니다.
/// </summary>
public class ProgressionDebugOverlay : MonoBehaviour
{
    private bool _visible;
    private IPerkService _perk;
    private IModifierService _mod;
    private IDatabase _db;
    private string _profileId = "P1";
    private Vector2 _scroll;
    private float _multiTouchTimer;
    private const float MultiTouchHoldSeconds = 0.2f; // 최소 홀드 시간(오동작 방지)
    // UI 스케일링
    private float _userScale = 1f;
    private const string ScalePrefsKey = "dbg.overlay.scale";

    // 위험 구역(데이터 삭제) 상태 값
    private bool _dangerExpanded;
    private string _dangerConfirm = string.Empty; // "DELETE" 입력 확인용 텍스트
    private bool _alsoDeletePrefs = true;
    private bool _reloadMainMenuAfterWipe = true;
    private string _lastWipeLog = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    /// <summary>
    /// 개발 빌드에서 오버레이 오브젝트를 자동 생성합니다.
    /// </summary>
    private static void Bootstrap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var go = new GameObject("ProgressionDebugOverlay");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.DontSave;
        go.AddComponent<ProgressionDebugOverlay>();
#endif
    }

    /// <summary>
    /// 필요한 서비스와 사용자 설정을 로드합니다.
    /// </summary>
    private void Awake()
    {
        _db = ServiceRegistry.Get<IDatabase>();
        _perk = ServiceRegistry.Get<IPerkService>();
        _mod = ServiceRegistry.Get<IModifierService>();
        // 사용자 스케일 로드
        _userScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScalePrefsKey, 1f), 0.5f, 4f);
    }

    /// <summary>
    /// 단축키나 멀티터치 입력으로 오버레이 토글을 감지합니다.
    /// </summary>
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // PC/시뮬레이터: 특정 UI 텍스트(Txt_Title) 5회 클릭 시 토글
        TryHandleTitleClickToggle();

        // 모바일: 3손가락 동시 터치로 오버레이 열기/토글
        if (Input.touchCount >= 3)
        {
            // 모든 터치가 화면에 닿아있는 동안 타이머 증가
            bool allTouching = true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var ph = Input.touches[i].phase;
                if (ph == TouchPhase.Canceled || ph == TouchPhase.Ended) { allTouching = false; break; }
            }
            if (allTouching) _multiTouchTimer += Time.unscaledDeltaTime;
        }
        else
        {
            // 손을 떼는 순간 판정
            if (_multiTouchTimer >= MultiTouchHoldSeconds)
            {
                _visible = !_visible; // 토글 방식: 닫혀 있으면 열리고, 열려 있으면 닫힘
            }
            _multiTouchTimer = 0f;
        }
#endif
    }

    /// <summary>
    /// 디버그 오버레이 UI를 그립니다.
    /// </summary>
    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_visible) return;
        GUI.depth = 0;

        // 안전영역 + DPI 기반 스케일 계산
        float dpiScale = Screen.dpi > 0 ? Screen.dpi / 160f : Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
        const float MinApplied = 0.75f; // 더 키우고 싶으면 1.0으로
        const float MaxApplied = 5.0f;  // 상한을 5x로 상향
        float rawScale = dpiScale * _userScale;
        float scale = Mathf.Clamp(rawScale, MinApplied, MaxApplied);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        // 노치/펀치홀 보호를 위해 SafeArea 기준으로 약간의 여백을 둡니다.
        var sa = Screen.safeArea;
        float marginX = sa.xMin / scale + 10f;
        float marginY = sa.yMin / scale + 10f;
        var area = new Rect(marginX, marginY, 420, 580);
        GUILayout.BeginArea(area, GUI.skin.window);
        // 헤더 + 닫기 버튼 (폭이 좁아져도 항상 보이도록 첫 줄에 배치)
        GUILayout.BeginHorizontal();
        GUILayout.Label("[Progression Debug]");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", GUILayout.Width(80))) _visible = false;
        GUILayout.EndHorizontal();

        // 프로필 포인트 관리
        var profile = DatabaseManager.Instance.LoadProfile(_profileId);
        if (profile == null)
        {
            profile = new PlayerProfile { ProfileId = _profileId, CreatedAtUtc = System.DateTime.UtcNow.ToString("o"), AppVersion = Application.version };
            DatabaseManager.Instance.SaveProfile(profile);
        }
        GUILayout.Label($"Profile: {_profileId} | Points: {profile.UnspentPerkPoints}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+5 pts", GUILayout.Width(80))) { DatabaseManager.Instance.AddPerkPoints(_profileId, 5); }
        if (GUILayout.Button("+10 pts", GUILayout.Width(80))) { DatabaseManager.Instance.AddPerkPoints(_profileId, 10); }
        if (GUILayout.Button("Reset pts", GUILayout.Width(80))) { profile.UnspentPerkPoints = 0; DatabaseManager.Instance.SaveProfile(profile); }
        GUILayout.EndHorizontal();

        // Perk 구매 기능은 메인 메뉴 Perks 화면에서 제공하므로 디버그 오버레이에서는 노출하지 않습니다.
        if (_perk == null) { _perk = ServiceRegistry.Get<IPerkService>(); }
        GUILayout.Space(6);

        // 업적 간편 테스트
        GUILayout.Space(6);
        GUILayout.Label("Achievements quick test");
        if (GUILayout.Button("Unlock ACH_FIRST_WIN (+1pt)"))
        {
            ServiceRegistry.Get<IAchievementService>()?.UnlockDirect("ACH_FIRST_WIN", 1);
        }

        // STARTING_GOLD 스냅샷 미리보기
        GUILayout.Space(8);
        GUILayout.Label("Preview: STARTING_GOLD");
        float baseGold = 300f;
        var agg = _perk != null ? _perk.ComputeAggregatesForProfile(_profileId) : null;
        float flat = 0f, pct = 0f;
        if (agg != null && agg.TryGetValue("STARTING_GOLD", out var tup)) { flat = tup.flat; pct = tup.percent; }
        float preview = (baseGold + flat) * (1f + pct);
        GUILayout.Label($"Base={baseGold}, Flat={flat}, %={pct * 100f:F1} → {Mathf.Round(preview)}");

        // 스냅샷을 활용해 빠르게 새 런을 생성합니다.
        if (GUILayout.Button("Create New Run with Snapshot"))
        {
            var runId = System.Guid.NewGuid().ToString("N");
            var run = new CurrentRun
            {
                RunId = runId, ProfileId = _profileId,
                CompanionId = string.Empty,
                Act = 1, Floor = 0, NodeIndex = 0,
                Gold = Mathf.RoundToInt(preview),
                CurrentHp = 80, MaxHpBase = 80, EnergyMax = 3,
                CreatedAtUtc = System.DateTime.UtcNow.ToString("o"),
                UpdatedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };
            _perk?.ComputeRunSnapshotAndPersist(_profileId, runId);
            _mod?.RebindRun(runId);
            ServiceRegistry.Get<IDatabase>()?.UpsertCurrentRun(run);
            PlayerPrefs.SetString("lastRunId", runId);
            PlayerPrefs.Save();
            Debug.Log($"[ProgressionDebug] New run created: {runId} with starting gold {run.Gold}");
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        // UI 크기 조절 (1줄)
        if (GUILayout.Button("A-", GUILayout.Width(40))) { _userScale = Mathf.Clamp(_userScale * 0.9f, 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        if (GUILayout.Button("A+", GUILayout.Width(40))) { _userScale = Mathf.Clamp(_userScale * 1.1f, 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        if (GUILayout.Button("150%", GUILayout.Width(50))) { _userScale = Mathf.Clamp(1.5f / Mathf.Max(0.1f, dpiScale), 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        if (GUILayout.Button("200%", GUILayout.Width(50))) { _userScale = Mathf.Clamp(2.0f / Mathf.Max(0.1f, dpiScale), 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        if (GUILayout.Button("300%", GUILayout.Width(50))) { _userScale = Mathf.Clamp(3.0f / Mathf.Max(0.1f, dpiScale), 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        if (GUILayout.Button("400%", GUILayout.Width(50))) { _userScale = Mathf.Clamp(4.0f / Mathf.Max(0.1f, dpiScale), 0.5f, 8f); PlayerPrefs.SetFloat(ScalePrefsKey, _userScale); PlayerPrefs.Save(); }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        // 적용/원시 스케일 및 클램프 상태 표기 + 토글 안내 (2줄)
        string clampTag = rawScale < MinApplied ? " (min clamp)" : rawScale > MaxApplied ? " (max clamp)" : "";
        GUILayout.Label($"Applied x{scale:0.00} / Raw x{rawScale:0.00}{clampTag}");
        GUILayout.Label("재열기: F10(PC) / 3손가락 터치(모바일)");

        // --- 위험 구역: 파괴적인 테스트 도구 ---
        GUILayout.Space(10);
        GUILayout.Label("[Danger Zone] 테스트용 데이터 삭제");
        _dangerExpanded = GUILayout.Toggle(_dangerExpanded, _dangerExpanded ? "접기" : "펼치기", GUILayout.Width(80));
        if (_dangerExpanded)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            // 현재 런 데이터 삭제
            GUILayout.Label("현재 런 데이터 삭제 (Run-only)");
            if (GUILayout.Button("Delete Current Run", GUILayout.Height(24)))
            {
                TryDeleteCurrentRun();
            }
            GUILayout.Space(6);

            // 전체 삭제 옵션
            GUILayout.Label("전체 저장 데이터 삭제 (DB + 옵션: PlayerPrefs)");
            GUILayout.BeginHorizontal();
            _alsoDeletePrefs = GUILayout.Toggle(_alsoDeletePrefs, "PlayerPrefs도 삭제", GUILayout.Width(160));
            _reloadMainMenuAfterWipe = GUILayout.Toggle(_reloadMainMenuAfterWipe, "삭제 후 메인 메뉴", GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("확인 입력: 'DELETE' 입력 후 실행하세요");
            _dangerConfirm = GUILayout.TextField(_dangerConfirm, GUILayout.Width(200));
            using (new GuiColorScope(_dangerConfirm == "DELETE" ? Color.white : new Color(1f, 0.6f, 0.6f)))
            {
                if (GUILayout.Button("Delete ALL Data", GUILayout.Height(30)))
                {
                    if (_dangerConfirm == "DELETE")
                    {
                        WipeAllData(_alsoDeletePrefs);
                        _dangerConfirm = string.Empty;
                        if (_reloadMainMenuAfterWipe)
                        {
                            TryReloadMainMenu();
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[ProgressionDebug] DELETE 확인 입력이 필요합니다.");
                    }
                }
            }

            if (!string.IsNullOrEmpty(_lastWipeLog))
            {
                GUILayout.Space(4);
                GUILayout.Label(_lastWipeLog);
            }

            // DB 파일 위치 안내
            GUILayout.Space(4);
            GUILayout.Label($"DB 위치: {Application.persistentDataPath}/game_save.db");

            GUILayout.EndVertical();
        }
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
#endif
    }

    // --- 특정 UI 요소를 클릭해 숨김 토글 ---
    private const string TitleObjectName = "Txt_Title"; // 클릭 타겟 이름
    private const int ClicksToToggle = 5;                // 누적 클릭 수
    private const float ClickWindowSeconds = 1.5f;       // 누적 허용 시간 간격
    private int _titleClickCount;
    private float _lastTitleClickAt;

    /// <summary>
    /// 특정 UI 요소를 빠르게 클릭했을 때 오버레이를 토글합니다.
    /// </summary>
    private void TryHandleTitleClickToggle()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        var es = EventSystem.current;
        if (es == null) return;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        if (hits == null || hits.Count == 0) return;

        bool hitTitle = false;
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            if (go != null && go.name == TitleObjectName)
            {
                hitTitle = true;
                break;
            }
        }
        if (!hitTitle) return;

        float now = Time.unscaledTime;
        if (now - _lastTitleClickAt <= ClickWindowSeconds) _titleClickCount++;
        else _titleClickCount = 1;
        _lastTitleClickAt = now;

        if (_titleClickCount >= ClicksToToggle)
        {
            _titleClickCount = 0;
            _visible = !_visible;
        }
    }

    /// <summary>
    /// 현재 런과 관련된 저장 데이터를 삭제합니다.
    /// </summary>
    private void TryDeleteCurrentRun()
    {
        try
        {
            var runId = PlayerPrefs.GetString("lastRunId", "");
            if (!string.IsNullOrEmpty(runId))
            {
                // 방어적으로 상점과 이벤트 세션을 먼저 정리합니다.
                DatabaseManager.Instance.DeleteActiveShopSession(runId);
                DatabaseManager.Instance.DeleteActiveEventSession(runId);
                // 런 관련 행을 삭제합니다.
                DatabaseManager.Instance.DeleteCurrentRun(runId);
                Debug.Log($"[ProgressionDebug] Current run deleted: {runId}");
            }
            PlayerPrefs.DeleteKey("lastRunId");
            PlayerPrefs.DeleteKey("selectedCompanionId");
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressionDebug] DeleteCurrentRun failed: {e.Message}");
        }
    }

    /// <summary>
    /// DB와 관련 PlayerPrefs 키를 삭제해 진행도를 초기화합니다.
    /// </summary>
    private void WipeAllData(bool alsoPrefs)
    {
        var sb = new System.Text.StringBuilder();
        try { ServiceRegistry.Get<IAchievementService>()?.Flush(); } catch { }

        // 파일 잠금을 해제하기 위해 DB 연결을 닫습니다.
        try { DatabaseManager.Instance.Close(); } catch { }

        var dir = Application.persistentDataPath;
        var candidates = new System.Collections.Generic.List<string>();
        try
        {
            // 기본 경로의 파일을 우선 삭제합니다.
            candidates.Add(Path.Combine(dir, "game_save.db"));
            candidates.Add(Path.Combine(dir, "game_save.db.bak"));
            candidates.Add(Path.Combine(dir, "game_save.db-wal"));
            candidates.Add(Path.Combine(dir, "game_save.db-shm"));
            // 동일 패턴의 잔여 파일도 모두 제거합니다.
            foreach (var f in Directory.GetFiles(dir, "game_save.db*"))
                if (!candidates.Contains(f)) candidates.Add(f);
        }
        catch { }

        int deleted = 0;
        foreach (var f in candidates)
        {
            try
            {
                if (File.Exists(f)) { File.Delete(f); deleted++; sb.AppendLine($"Deleted: {f}"); }
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"Failed delete: {f} ({e.Message})");
            }
        }

        // 필요 시 관련 PlayerPrefs 키만 선별적으로 삭제합니다.
        if (alsoPrefs)
        {
            try
            {
                PlayerPrefs.DeleteKey("lastRunId");
                PlayerPrefs.DeleteKey("selectedCompanionId");
                PlayerPrefs.DeleteKey(ScalePrefsKey);
                PlayerPrefs.Save();
                sb.AppendLine("PlayerPrefs keys cleared (lastRunId, selectedCompanionId, dbg.overlay.scale)");
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"PlayerPrefs clear failed: {e.Message}");
            }
        }

        // 스키마를 재생성하기 위해 다시 연결합니다.
        try { DatabaseManager.Instance.Connect(); sb.AppendLine("DB reconnected and schema ensured."); }
        catch (System.Exception e) { sb.AppendLine($"Reconnect failed: {e.Message}"); }

        _lastWipeLog = $"[Wipe] files deleted={deleted}\n" + sb.ToString();
        Debug.Log($"[ProgressionDebug] Full data wipe complete. Deleted files={deleted}\n{_lastWipeLog}");
    }

    /// <summary>
    /// 전체 데이터를 삭제한 뒤 메인 메뉴 씬을 재로드합니다.
    /// </summary>
    private void TryReloadMainMenu()
    {
        try
        {
            // 개발/테스트 편의: 메뉴로 복귀. 씬 이름은 프로젝트 표준을 따릅니다.
            SceneManager.LoadScene("Main Menu");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProgressionDebug] Failed to load Main Menu: {e.Message}");
        }
    }
}

// GUI.color를 임시로 변경하기 위한 간단한 도우미입니다.
internal readonly struct GuiColorScope : System.IDisposable
{
    private readonly Color _prev;
    public GuiColorScope(Color c)
    {
        _prev = GUI.color;
        GUI.color = c;
    }
    public void Dispose() => GUI.color = _prev;
}

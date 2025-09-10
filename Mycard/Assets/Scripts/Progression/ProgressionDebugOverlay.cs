using UnityEngine;
using Game.Save;
using System.Linq;

// Runtime debug overlay for testing progression without clearing the game each time.
// Toggle with F10 (Editor or Development builds).
[DefaultExecutionOrder(10000)]
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var go = new GameObject("ProgressionDebugOverlay");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.DontSave;
        go.AddComponent<ProgressionDebugOverlay>();
#endif
    }

    private void Awake()
    {
        _db = ServiceRegistry.Get<IDatabase>();
        _perk = ServiceRegistry.Get<IPerkService>();
        _mod = ServiceRegistry.Get<IModifierService>();
        // 1) 개발 빌드에서는 기본 ON (foolproof)
        if (Debug.isDebugBuild) _visible = true;
        // 2) 사용자 스케일 로드
        _userScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScalePrefsKey, 1f), 0.5f, 4f);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // PC/에뮬레이터: F10 토글
        if (Input.GetKeyDown(KeyCode.F10)) _visible = !_visible;

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

        // Profile points
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

        // Achievements quick test
        GUILayout.Space(6);
        GUILayout.Label("Achievements quick test");
        if (GUILayout.Button("Unlock ACH_FIRST_WIN (+1pt)"))
        {
            ServiceRegistry.Get<IAchievementService>()?.UnlockDirect("ACH_FIRST_WIN", 1);
        }

        // Snapshot preview for STARTING_GOLD
        GUILayout.Space(8);
        GUILayout.Label("Preview: STARTING_GOLD");
        float baseGold = 300f;
        var agg = _perk != null ? _perk.ComputeAggregatesForProfile(_profileId) : null;
        float flat = 0f, pct = 0f;
        if (agg != null && agg.TryGetValue("STARTING_GOLD", out var tup)) { flat = tup.flat; pct = tup.percent; }
        float preview = (baseGold + flat) * (1f + pct);
        GUILayout.Label($"Base={baseGold}, Flat={flat}, %={pct * 100f:F1} → {Mathf.Round(preview)}");

        // Start new run fast with snapshot
        if (GUILayout.Button("Create New Run with Snapshot"))
        {
            var runId = System.Guid.NewGuid().ToString("N");
            var run = new CurrentRun
            {
                RunId = runId, ProfileId = _profileId,
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
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
#endif
    }
}

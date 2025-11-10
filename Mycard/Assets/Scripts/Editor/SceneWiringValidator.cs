using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// 배틀 씬의 필수 컴포넌트 연결 상태를 검사하는 에디터 전용 유틸리티입니다.
/// </summary>
public static class SceneWiringValidator
{
    // 프로젝트의 실제 배틀 씬 경로
    private const string BattleScenePath = "Assets/Scenes/Battle_android.unity";

    /// <summary>
    /// 현재 열려 있는 씬을 저장한 뒤 배틀 씬을 로드해 배선 검사를 실행합니다.
    /// </summary>
    [MenuItem("Tools/Validate/Validate Battle Scene Wiring")]
    public static void ValidateSceneFromMenu()
    {
        string original = EditorSceneManager.GetActiveScene().path;
        try
        {
            if (!File.Exists(BattleScenePath))
            {
                GameLog.Error($"[검증 실패] 배틀 씬 경로를 찾을 수 없습니다: {BattleScenePath}");
                return;
            }
            EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            bool ok = RunChecks();
            if (ok) GameLog.Info("✅ Battle Scene 검증 성공");
            else GameLog.Warn("❌ Battle Scene 검증 실패 — Console 로그를 확인하세요.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(original) && original != BattleScenePath)
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }
    }

    // CI 진입점: -executeMethod SceneWiringValidator.CIScan
    /// <summary>
    /// CI 파이프라인에서 호출되어 검사 실패 시 프로세스를 종료합니다.
    /// </summary>
    public static void CIScan()
    {
        EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        if (!RunChecks() && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// <summary>
    /// 배틀 씬 내 필수 오브젝트와 레퍼런스를 검증합니다.
    /// </summary>
    private static bool RunChecks()
    {
        bool ok = true;

        var bootstrap = Object.FindObjectOfType<BattleSceneBootstrap>();
        ok &= Assert(bootstrap != null, "BattleSceneBootstrap이 씬에 없습니다.");
        if (bootstrap != null)
        {
            var so = new SerializedObject(bootstrap);
            var cardPrefabProp = so.FindProperty("_cardPrefab");
            ok &= Assert(cardPrefabProp != null && cardPrefabProp.objectReferenceValue != null,
                "BattleSceneBootstrap._cardPrefab이 할당되지 않았습니다.", bootstrap);
        }

        ok &= Assert(Object.FindObjectOfType<HandServiceBinder>() != null,
            "HandServiceBinder 컴포넌트를 씬에서 찾을 수 없습니다.");

        var es = Object.FindObjectOfType<EventSystem>();
        ok &= Assert(es != null, "EventSystem이 씬에 없습니다.");
        if (es != null)
        {
            ok &= Assert(es.GetComponent<BaseInputModule>() != null,
                "EventSystem에 입력 모듈(StandaloneInputModule 또는 InputSystemUIInputModule)이 없습니다.", es);
        }

        var cam = Camera.main;
        ok &= Assert(cam != null, "MainCamera 태그가 지정된 카메라가 없습니다.");
        if (cam != null)
        {
            bool hasRaycaster = cam.GetComponent<PhysicsRaycaster>() != null
                || cam.GetComponent<Physics2DRaycaster>() != null
                || (cam.GetComponentInParent<Canvas>()?.GetComponent<GraphicRaycaster>() != null);
            ok &= Assert(hasRaycaster,
                "Main Camera 또는 부모 Canvas에 PhysicsRaycaster/Physics2DRaycaster/GraphicRaycaster 중 하나가 필요합니다.", cam);
        }

        return ok;
    }

    /// <summary>
    /// 검증 실패 시 에디터 콘솔에 오류를 남깁니다.
    /// </summary>
    private static bool Assert(bool cond, string msg, Object ctx = null)
    {
        if (!cond) GameLog.Error($"[씬 연결 검증 실패] {msg}", ctx);
        return cond;
    }
}


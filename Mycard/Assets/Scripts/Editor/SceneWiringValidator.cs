using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;

public static class SceneWiringValidator
{
    // 프로젝트의 실제 배틀 씬 경로
    private const string BattleScenePath = "Assets/Scenes/Battle_android.unity";

    [MenuItem("Tools/Validate/Validate Battle Scene Wiring")]
    public static void ValidateSceneFromMenu()
    {
        string original = EditorSceneManager.GetActiveScene().path;
        try
        {
            if (!File.Exists(BattleScenePath))
            {
                Debug.LogError($"[검증 실패] 배틀 씬 경로를 찾을 수 없습니다: {BattleScenePath}");
                return;
            }
            EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            bool ok = RunChecks();
            if (ok) Debug.Log("✅ Battle Scene 검증 성공");
            else Debug.LogWarning("❌ Battle Scene 검증 실패 — Console 로그를 확인하세요.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(original) && original != BattleScenePath)
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }
    }

    // CI 진입점: -executeMethod SceneWiringValidator.CIScan
    public static void CIScan()
    {
        EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        if (!RunChecks() && Application.isBatchMode) EditorApplication.Exit(1);
    }

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

    private static bool Assert(bool cond, string msg, Object ctx = null)
    {
        if (!cond) Debug.LogError($"[씬 연결 검증 실패] {msg}", ctx);
        return cond;
    }
}


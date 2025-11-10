using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// V2: 정규식 + 확장자(.cs, .unity, .prefab) 기반 탐지. CI 연동 지원.
/// 정의 파일(DeckController.cs) 및 주석은 기본적으로 제외해 오탐을 줄입니다.
/// </summary>
public static class LegacyApiScanner
{
    // 금지 패턴(정규식 그룹)
    // 주의: verbatim string(@)에서는 역슬래시는 그대로 쓰고, 쌍따옴표는 ""로 이스케이프합니다.
    private static readonly string[] BannedGroups =
    {
        // 직접 접근
        @"DeckController\s*\.\s*instance",
        @"FindObjectOfType<\s*DeckController\s*>",
        @"GetComponent<\s*DeckController\s*>",
        // 삭제된/레거시 API
        @"SaveCurrentRun\s*\(",
        // 잠재적 문자열 호출 패턴(필요 시 유지)
        "\"DrawCard\""
    };

    // 스캔 대상 확장자
    private static readonly string[] Exts = { "*.cs", "*.unity", "*.prefab" };

    // 무시 경로(포함 시 스킵)
    private static readonly string[] IgnoreFragments = { "/Library/", "/Packages/", "/Logs/", "/Obj/" };

    // 정의 파일 화이트리스트(정의 자체는 허용)
    private static readonly string[] DefinitionAllowlist =
    {
        "Assets/Scripts/Deck/DeckController.cs"
    };

    /// <summary>
    /// 에디터 메뉴에서 호출되어 금지된 레거시 API 사용 여부를 검사합니다.
    /// </summary>
    [MenuItem("Tools/Validate/Scan for Legacy APIs")]
    public static void ScanFromMenu()
    {
        bool ok = RunScan();
        if (ok) GameLog.Info("✅ 레거시 API 스캐너: 금지된 패턴 없음");
        else GameLog.Warn("❌ 레거시 API 스캐너: 금지된 패턴 발견 (Console 확인)");
    }

    /// <summary>
    /// CI 파이프라인에서 호출되어 위반이 발견되면 비정상 종료합니다.
    /// </summary>
    public static void CIScan()
    {
        bool ok = RunScan();
        if (!ok && Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 지정된 확장자의 에셋을 순회하며 금지된 정규식 패턴이 있는지 검사합니다.
    /// </summary>
    private static bool RunScan()
    {
        bool clean = true;
        string assetsRoot = Application.dataPath;
        // 단어 경계를 강제하지 않아 문자열 패턴(예: "DrawCard")도 안정적으로 탐지
        string pattern = "(" + string.Join("|", BannedGroups) + ")";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        foreach (var ext in Exts)
        {
            var files = Directory.GetFiles(assetsRoot, ext, SearchOption.AllDirectories)
                .Where(p => !IgnoreFragments.Any(ig => p.Replace('\\','/').Contains(ig)));
            foreach (var file in files)
            {
                string rel = "Assets" + file.Replace(assetsRoot, "");
                if (DefinitionAllowlist.Any(allow => rel.Replace('\\','/') == allow))
                    continue; // 정의 파일 자체는 허용

                string content;
                try { content = File.ReadAllText(file); }
                catch { continue; }

                // C# 주석 제거(간단): 블록/라인 주석을 제거해 오탐 완화
                if (file.EndsWith(".cs"))
                {
                    // 여러 줄 주석
                    content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
                    // 한 줄 주석
                    content = Regex.Replace(content, @"(^|\n)\s*//.*", string.Empty);
                }

                if (regex.IsMatch(content))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(rel);
                    GameLog.Error($"[레거시 API 발견] 파일: {rel} — 패턴 그룹 중 하나 매칭", asset);
                    clean = false;
                }
            }
        }
        return clean;
    }
}

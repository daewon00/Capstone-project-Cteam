using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 카드 ScriptableObject 에셋의 ID 중복 및 누락을 검사하는 에디터 전용 유틸리티입니다.
/// </summary>
public static class CardCatalogValidator
{
    // 프로젝트의 카드 데이터 ScriptableObject 경로
    private const string CardSoFolder = "Assets/Resources/Cards";

    /// <summary>
    /// 메뉴에서 호출되어 전체 카드 에셋을 검증합니다.
    /// </summary>
    [MenuItem("Tools/Validate/Validate Card Catalog")]
    public static void ValidateAllCardsFromMenu()
    {
        bool ok = RunValidation();
        if (ok) GameLog.Info("✅ 카드 카탈로그 검증 성공");
        else GameLog.Warn("❌ 카드 카탈로그 검증 실패 — Console 로그를 확인하세요.");
    }

    /// <summary>
    /// CI 파이프라인에서 호출되어 검증 실패 시 배치를 종료합니다.
    /// </summary>
    public static void CIScan()
    {
        if (!RunValidation() && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// <summary>
    /// 카드 에셋을 스캔해 ID 중복이나 누락이 있는지 확인합니다.
    /// </summary>
    private static bool RunValidation()
    {
        bool allValid = true;
        var seen = new HashSet<string>();

        // CardScriptableObject 타입 수집
        string[] guids = AssetDatabase.FindAssets("t:CardScriptableObject", new[] { CardSoFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<CardScriptableObject>(path);
            if (card == null) continue;

            string id = card.CardId;
            if (string.IsNullOrEmpty(id))
            {
                GameLog.Error("[카드 검증 실패] CardId가 비어있습니다.", card);
                allValid = false;
                continue;
            }
            if (!seen.Add(id))
            {
                GameLog.Error($"[카드 검증 실패] CardId 중복: '{id}'", card);
                allValid = false;
            }
        }
        return allValid;
    }
}


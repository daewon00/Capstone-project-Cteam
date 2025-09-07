using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;

public static class CardCatalogValidator
{
    // 프로젝트의 카드 데이터 ScriptableObject 경로
    private const string CardSoFolder = "Assets/Resources/Cards";

    [MenuItem("Tools/Validate/Validate Card Catalog")]
    public static void ValidateAllCardsFromMenu()
    {
        bool ok = RunValidation();
        if (ok) Debug.Log("✅ 카드 카탈로그 검증 성공");
        else Debug.LogWarning("❌ 카드 카탈로그 검증 실패 — Console 로그를 확인하세요.");
    }

    public static void CIScan()
    {
        if (!RunValidation() && Application.isBatchMode) EditorApplication.Exit(1);
    }

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
                Debug.LogError("[카드 검증 실패] CardId가 비어있습니다.", card);
                allValid = false;
                continue;
            }
            if (!seen.Add(id))
            {
                Debug.LogError($"[카드 검증 실패] CardId 중복: '{id}'", card);
                allValid = false;
            }
        }
        return allValid;
    }
}


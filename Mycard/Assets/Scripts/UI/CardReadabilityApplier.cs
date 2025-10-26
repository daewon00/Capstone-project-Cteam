using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshPro 가독성 스타일(외곽선)을 안전하게 적용하는 유틸리티입니다.
/// 공유 머티리얼을 오염시키지 않도록 인스턴스 재질을 보장합니다.
/// </summary>
public static class CardReadabilityApplier
{
    public static void ApplyOutline(TMP_Text tmp, float width, Color color)
    {
        if (tmp == null) return;
        try
        {
            if (ReferenceEquals(tmp.fontMaterial, tmp.fontSharedMaterial))
            {
                tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
            }
            tmp.outlineWidth = Mathf.Clamp01(width);
            tmp.outlineColor = color;
        }
        catch { /* ignore */ }
    }
}


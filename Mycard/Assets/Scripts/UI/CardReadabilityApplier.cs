using TMPro;
using UnityEngine;

// Utility to safely apply TMP outline without mutating shared materials.
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
        catch { }
    }
}


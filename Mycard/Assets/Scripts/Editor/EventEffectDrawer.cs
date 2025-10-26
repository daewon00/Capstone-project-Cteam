#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EventEffect))]
public sealed class EventEffectDrawer : PropertyDrawer
{
    private static readonly GUIContent HpLabel = new("체력 변화");
    private static readonly GUIContent GoldLabel = new("골드 변화");
    private static readonly GUIContent DefaultAmountLabel = new("값");
    private static readonly GUIContent HealPercentLabel = new("회복 %");
    private static readonly GUIContent CardIdLabel = new("카드 ID");
    private static readonly GUIContent RelicIdLabel = new("유물 ID");
    private static readonly GUIContent QuantityLabel = new("수량");
    private static readonly GUIContent UpgradeLabel = new("업그레이드");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        var effectType = typeProp != null ? (EventEffectType)typeProp.enumValueIndex : default;

        int lines = 1; // type line
        if (ShouldShowAmount(effectType)) lines++;
        if (ShouldShowRefId(effectType)) lines++;
        if (ShouldShowQuantity(effectType)) lines++;
        if (ShouldShowUpgrade(effectType)) lines++;

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        int spacingCount = lines > 0 ? lines - 1 : 0;
        return lines * lineHeight + spacingCount * spacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var typeProp = property.FindPropertyRelative("type");
        var amountProp = property.FindPropertyRelative("amount");
        var refIdProp = property.FindPropertyRelative("refId");
        var quantityProp = property.FindPropertyRelative("quantity");
        var upgradeProp = property.FindPropertyRelative("upgrade");

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var line = new Rect(position.x, position.y, position.width, lineHeight);

        EditorGUI.PropertyField(line, typeProp);
        line.y += lineHeight + spacing;

        var effectType = (EventEffectType)typeProp.enumValueIndex;

        if (ShouldShowAmount(effectType))
        {
            var amountLabel = effectType switch
            {
                EventEffectType.HpDelta => HpLabel,
                EventEffectType.GoldDelta => GoldLabel,
                EventEffectType.HealPercent => HealPercentLabel,
                _ => DefaultAmountLabel
            };
            EditorGUI.PropertyField(line, amountProp, amountLabel);
            line.y += lineHeight + spacing;
        }

        if (ShouldShowRefId(effectType))
        {
            var labelContent = effectType == EventEffectType.AddRelic ? RelicIdLabel : CardIdLabel;
            EditorGUI.PropertyField(line, refIdProp, labelContent);
            line.y += lineHeight + spacing;
        }

        if (ShouldShowQuantity(effectType))
        {
            EditorGUI.PropertyField(line, quantityProp, QuantityLabel);
            line.y += lineHeight + spacing;
        }

        if (ShouldShowUpgrade(effectType))
        {
            EditorGUI.PropertyField(line, upgradeProp, UpgradeLabel);
        }

        EditorGUI.EndProperty();
    }

    private static bool ShouldShowAmount(EventEffectType type)
        => type switch
        {
            EventEffectType.ReturnToMap => false,
            EventEffectType.AddRelic => false,
            EventEffectType.TransformCard => false,
            EventEffectType.AddCard => false,
            EventEffectType.AddCurse => false,
            EventEffectType.UpgradeRandomCard => false,
            _ => true
        };

    private static bool ShouldShowRefId(EventEffectType type)
        => type is EventEffectType.AddCard or EventEffectType.AddCurse or EventEffectType.TransformCard or EventEffectType.AddRelic;

    private static bool ShouldShowQuantity(EventEffectType type)
        => type is EventEffectType.AddCard or EventEffectType.AddCurse or EventEffectType.TransformCard or EventEffectType.AddRelic or EventEffectType.UpgradeRandomCard;

    private static bool ShouldShowUpgrade(EventEffectType type)
        => type is EventEffectType.AddCard or EventEffectType.AddCurse or EventEffectType.TransformCard;
}
#endif

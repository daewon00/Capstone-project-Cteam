#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EventEffect))]
public sealed class EventEffectDrawer : PropertyDrawer
{
    private static readonly GUIContent HpLabel = new("체력 변화");
    private static readonly GUIContent GoldLabel = new("골드 변화");
    private static readonly GUIContent DefaultAmountLabel = new("값");
    private static readonly GUIContent CardIdLabel = new("카드 ID");
    private static readonly GUIContent QuantityLabel = new("추가 수량");
    private static readonly GUIContent UpgradeLabel = new("업그레이드");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        var lines = 2; // type + amount
        if (typeProp != null && (EventEffectType)typeProp.enumValueIndex == EventEffectType.AddCard)
        {
            lines += 3; // refId, quantity, upgrade
        }

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        return lines * lineHeight + (lines - 1) * spacing;
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
        var amountLabel = effectType switch
        {
            EventEffectType.HpDelta => HpLabel,
            EventEffectType.GoldDelta => GoldLabel,
            _ => DefaultAmountLabel
        };
        EditorGUI.PropertyField(line, amountProp, amountLabel);

        if (effectType == EventEffectType.AddCard)
        {
            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, refIdProp, CardIdLabel);

            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, quantityProp, QuantityLabel);

            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, upgradeProp, UpgradeLabel);
        }

        EditorGUI.EndProperty();
    }
}
#endif

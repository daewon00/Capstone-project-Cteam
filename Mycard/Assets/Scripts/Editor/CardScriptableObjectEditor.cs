#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardScriptableObject))]
public class CardScriptableObjectEditor : Editor
{
    private SerializedProperty _upgradeSettings;
    private SerializedProperty _upgradeEnabled;
    private SerializedProperty _upgradeCost;
    private SerializedProperty _upgradeAttack;
    private SerializedProperty _upgradeHealth;

    private void OnEnable()
    {
        _upgradeSettings = serializedObject.FindProperty("upgradeSettings");
        if (_upgradeSettings != null)
        {
            _upgradeEnabled = _upgradeSettings.FindPropertyRelative("enabled");
            _upgradeCost = _upgradeSettings.FindPropertyRelative("manaCost");
            _upgradeAttack = _upgradeSettings.FindPropertyRelative("attackPower");
            _upgradeHealth = _upgradeSettings.FindPropertyRelative("health");
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((CardScriptableObject)target), typeof(MonoScript), false);
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("cardName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("CardId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("actionDescription"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cardLore"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("manaCost"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackPower"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentHealth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bgSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rarity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("effects"), new GUIContent("효과 목록"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("removeAfterCombat"));

        if (_upgradeSettings != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("강화 설정", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_upgradeEnabled, new GUIContent("강화 가능", "체크하면 강화 수치를 개별적으로 입력할 수 있습니다. 기본값은 카드의 현재 스탯입니다."));

            using (new EditorGUI.DisabledScope(!_upgradeEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(_upgradeCost, new GUIContent("강화 코스트", "강화 후 적용될 코스트입니다. 0 미만으로 내려가면 자동으로 0으로 고정됩니다."));
                EditorGUILayout.PropertyField(_upgradeAttack, new GUIContent("강화 공격력", "강화 후 공격력입니다."));
                EditorGUILayout.PropertyField(_upgradeHealth, new GUIContent("강화 체력", "강화 후 체력입니다."));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

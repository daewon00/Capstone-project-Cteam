using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyEncounterConfig))]
public class EnemyEncounterConfigEditor : Editor
{
    private SerializedProperty _deckProp;

    private void OnEnable()
    {
        _deckProp = serializedObject.FindProperty("덱카드목록");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("카드 미리보기", EditorStyles.boldLabel);

        if (_deckProp != null && _deckProp.isArray)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < _deckProp.arraySize; i++)
            {
                var element = _deckProp.GetArrayElementAtIndex(i);
                var card = element.objectReferenceValue as CardScriptableObject;
                if (card == null)
                {
                    EditorGUILayout.LabelField($"[{i}] (비어 있음)");
                    continue;
                }

                    string cardName = string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName;
                    string desc = string.IsNullOrEmpty(card.actionDescription) ? "(설명 없음)" : card.actionDescription;
                    EditorGUILayout.LabelField($"[{i}] {cardName}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"    설명 : {desc}");
                    EditorGUILayout.LabelField($"    코스트 : {card.manaCost}   공격력 : {card.attackPower}   체력 : {card.currentHealth}");
            }
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}

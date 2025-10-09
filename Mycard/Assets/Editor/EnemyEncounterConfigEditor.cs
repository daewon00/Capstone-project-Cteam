using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyEncounterConfig))]
public class EnemyEncounterConfigEditor : Editor
{
    private SerializedProperty _deckProp;
    private ReorderableList _deckList;
    private int _objectPickerIndex = -1;

    private readonly Dictionary<int, Color> _rarityColors = new()
    {
        { 0, new Color(0.7f, 0.7f, 0.7f) },
        { 1, new Color(0.6f, 0.8f, 1.0f) },
        { 2, new Color(0.6f, 0.95f, 0.6f) },
        { 3, new Color(1.0f, 0.8f, 0.4f) },
        { 4, new Color(1.0f, 0.6f, 0.6f) }
    };

    private void OnEnable()
    {
        _deckProp = serializedObject.FindProperty("덱카드목록");
        _deckList = new ReorderableList(serializedObject, _deckProp, true, true, true, true);

        _deckList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "카드 목록 (이름 / 코스트 / 공격력 / 체력 / 효과)");
        };

        _deckList.drawElementCallback = DrawDeckElement;
        _deckList.elementHeightCallback = GetElementHeight;

        _deckList.onAddCallback = list =>
        {
            _deckProp.arraySize++;
            var element = _deckProp.GetArrayElementAtIndex(_deckProp.arraySize - 1);
            element.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            _objectPickerIndex = _deckProp.arraySize - 1;
            EditorGUIUtility.ShowObjectPicker<CardScriptableObject>(null, false, string.Empty, 0);
        };

        _deckList.onRemoveCallback = list =>
        {
            if (list.index >= 0 && list.index < _deckProp.arraySize)
            {
                _deckProp.DeleteArrayElementAtIndex(list.index);
                serializedObject.ApplyModifiedProperties();
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (_deckList != null)
        {
            _deckList.DoLayoutList();
        }

        HandleObjectPicker();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDeckElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (_deckProp == null || index >= _deckProp.arraySize) return;
        var element = _deckProp.GetArrayElementAtIndex(index);
        var card = element.objectReferenceValue as CardScriptableObject;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float padding = 2f;
        float y = rect.y + padding;

        Rect fieldRect = new Rect(rect.x, y, rect.width, lineHeight);
        y += lineHeight + 2f;

        EditorGUI.BeginChangeCheck();
        var newCard = EditorGUI.ObjectField(fieldRect, card, typeof(CardScriptableObject), false) as CardScriptableObject;
        if (EditorGUI.EndChangeCheck())
        {
            element.objectReferenceValue = newCard;
            serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            return;
        }

        if (card == null)
        {
            Rect emptyRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.HelpBox(emptyRect, "카드를 선택하세요.", MessageType.Info);
            return;
        }

        DrawCardInfo(rect.x, ref y, rect.width, lineHeight, card);
    }

    private float GetElementHeight(int index)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float height = lineHeight + 4f; // object field + spacing
        height += lineHeight + 2f;      // 이름
        height += lineHeight + 2f;      // 상세 정보

        if (_deckProp != null && index < _deckProp.arraySize)
        {
            var element = _deckProp.GetArrayElementAtIndex(index);
            var card = element.objectReferenceValue as CardScriptableObject;
            if (card != null && !string.IsNullOrEmpty(card.actionDescription))
            {
                GUIStyle style = EditorStyles.wordWrappedMiniLabel;
                float width = EditorGUIUtility.currentViewWidth - 60f;
                float descHeight = style.CalcHeight(new GUIContent(card.actionDescription), width);
                height += descHeight + 2f;
            }
        }

        height += 4f; // bottom padding
        return height;
    }

    private void DrawCardInfo(float x, ref float y, float width, float lineHeight, CardScriptableObject card)
    {
        string name = string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName;
        string desc = string.IsNullOrEmpty(card.actionDescription) ? "(설명 없음)" : card.actionDescription;
        int rarity = Mathf.Clamp((int)card.Rarity, 0, 4);
        if (!_rarityColors.TryGetValue(rarity, out Color color))
            color = new Color(0.8f, 0.8f, 0.8f);

        Rect nameRect = new Rect(x, y, width, lineHeight);
        using (new EditorGUI.IndentLevelScope())
        {
            Color prev = GUI.color;
            GUI.color = color;
            EditorGUI.LabelField(nameRect, name, EditorStyles.boldLabel);
            GUI.color = prev;
        }
        y += lineHeight + 2f;

        int effectCount = card.Effects != null ? card.Effects.Count : 0;
        bool hasEffect = effectCount > 0;
        string effectText = hasEffect ? $"효과 {effectCount}개" : "효과 없음";
        string details = $"코스트 {card.manaCost} | 공격력 {card.attackPower} | 체력 {card.currentHealth} | {effectText}";
        Rect detailRect = new Rect(x, y, width, lineHeight);
        EditorGUI.LabelField(detailRect, details, EditorStyles.miniLabel);
        y += lineHeight + 2f;

        if (!string.IsNullOrEmpty(card.actionDescription))
        {
            GUIStyle style = EditorStyles.wordWrappedMiniLabel;
            GUIContent content = new GUIContent(card.actionDescription);
            float descHeight = style.CalcHeight(content, width);
            Rect descRect = new Rect(x, y, width, descHeight);
            EditorGUI.LabelField(descRect, content, style);
            y += descHeight + 2f;
        }
    }

    private void HandleObjectPicker()
    {
        if (_objectPickerIndex < 0) return;

        Event e = Event.current;
        if (e.commandName == "ObjectSelectorUpdated" || e.commandName == "ObjectSelectorClosed")
        {
            Object picked = EditorGUIUtility.GetObjectPickerObject();
            if (picked is CardScriptableObject card && _objectPickerIndex < _deckProp.arraySize)
            {
                var element = _deckProp.GetArrayElementAtIndex(_objectPickerIndex);
                element.objectReferenceValue = card;
                serializedObject.ApplyModifiedProperties();
            }

            if (e.commandName == "ObjectSelectorClosed")
            {
                _objectPickerIndex = -1;
            }
        }
    }
}

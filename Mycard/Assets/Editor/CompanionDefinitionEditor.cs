using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CompanionDefinition))]
public class CompanionDefinitionEditor : Editor
{
    private SerializedProperty _startingCardIdsProp;
    private ReorderableList _cardList;
    private readonly Dictionary<string, CardScriptableObject> _cardLookup = new();
    private readonly Dictionary<int, Color> _rarityColors = new()
    {
        { 0, new Color(0.75f, 0.75f, 0.75f) },   // Common
        { 1, new Color(0.50f, 0.75f, 1.00f) },   // Rare
        { 2, new Color(0.60f, 0.95f, 0.60f) },   // Heroic
        { 3, new Color(1.00f, 0.80f, 0.40f) },   // Legendary
        { 4, new Color(1.00f, 0.60f, 0.60f) }
    };

    private GUIStyle _descriptionStyle;

    private void OnEnable()
    {
        _startingCardIdsProp = serializedObject.FindProperty("StartingCardIds");
        RebuildCardLookup();

        _descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            richText = false
        };

        _cardList = new ReorderableList(serializedObject, _startingCardIdsProp, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "시작 카드 목록"),
            drawElementCallback = DrawCardElement,
            elementHeightCallback = GetElementHeight,
            onAddCallback = list =>
            {
                int newIndex = _startingCardIdsProp.arraySize;
                _startingCardIdsProp.InsertArrayElementAtIndex(newIndex);
                _startingCardIdsProp.GetArrayElementAtIndex(newIndex).stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
            },
            onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < _startingCardIdsProp.arraySize)
                {
                    _startingCardIdsProp.DeleteArrayElementAtIndex(list.index);
                    serializedObject.ApplyModifiedProperties();
                }
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "StartingCardIds");

        EditorGUILayout.Space();
        if (_cardList != null)
        {
            _cardList.DoLayoutList();
        }

        if (GUILayout.Button("카드 목록 새로고침"))
        {
            RebuildCardLookup();
            Repaint();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCardElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (_startingCardIdsProp == null || index >= _startingCardIdsProp.arraySize)
            return;

        SerializedProperty element = _startingCardIdsProp.GetArrayElementAtIndex(index);
        string currentId = element.stringValue;
        CardScriptableObject card = GetCardById(currentId);

        float line = EditorGUIUtility.singleLineHeight;
        float y = rect.y + 2f;
        float width = rect.width;

        Rect objectRect = new Rect(rect.x, y, width, line);
        EditorGUI.BeginChangeCheck();
        CardScriptableObject pickedCard = EditorGUI.ObjectField(objectRect, "카드", card, typeof(CardScriptableObject), false) as CardScriptableObject;
        if (EditorGUI.EndChangeCheck())
        {
            element.stringValue = pickedCard != null ? pickedCard.CardId : string.Empty;
            serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            card = pickedCard;
            currentId = element.stringValue;
        }
        y += line + 2f;

        if (card == null)
        {
            Rect warnRect = new Rect(rect.x, y, width, line * 1.2f);
            EditorGUI.HelpBox(warnRect, string.IsNullOrEmpty(currentId)
                ? "카드 ID를 입력하거나 카드를 선택하세요."
                : $"CardId '{currentId}'에 해당하는 카드를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        DrawCardSummary(rect.x, ref y, width, card);
    }

    private float GetElementHeight(int index)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float height = line + 8f; // object field + padding

        if (_startingCardIdsProp != null && index < _startingCardIdsProp.arraySize)
        {
            string id = _startingCardIdsProp.GetArrayElementAtIndex(index).stringValue;
            CardScriptableObject card = GetCardById(id);
            if (card == null)
            {
                height += line * 1.6f + 6f;
            }
            else
            {
                height += line + 8f; // stats row
                if (!string.IsNullOrEmpty(card.actionDescription))
                {
                    float descHeight = _descriptionStyle.CalcHeight(new GUIContent(card.actionDescription), EditorGUIUtility.currentViewWidth - 90f);
                    height += descHeight + 8f;
                }
            }
        }
        return height;
    }

    private void DrawCardSummary(float x, ref float y, float width, CardScriptableObject card)
    {
        float line = EditorGUIUtility.singleLineHeight;
        int effectCount = card.Effects != null ? card.Effects.Count : 0;
        string detail = $"코스트 {card.manaCost} | 공격력 {card.attackPower} | 체력 {card.currentHealth} | {(effectCount > 0 ? $"효과 {effectCount}개" : "효과 없음")}";

        Color color = _rarityColors.TryGetValue((int)card.Rarity, out var col) ? col : new Color(0.8f, 0.8f, 0.8f);
        Color prev = GUI.color;
        GUI.color = color;
        Rect nameRect = new Rect(x, y, width, line);
        EditorGUI.LabelField(nameRect, $"{card.cardName} ({card.CardId})", EditorStyles.boldLabel);
        GUI.color = prev;
        y += line + 2f;

        Rect detailRect = new Rect(x, y, width, line);
        EditorGUI.LabelField(detailRect, detail, EditorStyles.miniLabel);
        y += line + 2f;

        if (!string.IsNullOrEmpty(card.actionDescription))
        {
            GUIContent content = new GUIContent(card.actionDescription);
            float descHeight = _descriptionStyle.CalcHeight(content, width);
            Rect descRect = new Rect(x, y, width, descHeight);
            EditorGUI.LabelField(descRect, content, _descriptionStyle);
            y += descHeight + 2f;
        }
    }

    private void RebuildCardLookup()
    {
        _cardLookup.Clear();
        string[] guids = AssetDatabase.FindAssets("t:CardScriptableObject");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<CardScriptableObject>(path);
            if (card == null)
                continue;
            if (string.IsNullOrEmpty(card.CardId))
                continue;
            if (!_cardLookup.ContainsKey(card.CardId))
            {
                _cardLookup.Add(card.CardId, card);
            }
        }
    }

    private CardScriptableObject GetCardById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (_cardLookup.TryGetValue(id, out var card) && card != null)
            return card;

        RebuildCardLookup();
        _cardLookup.TryGetValue(id, out card);
        return card;
    }
}

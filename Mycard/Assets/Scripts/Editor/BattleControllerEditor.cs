#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleController))]
public class BattleControllerEditor : Editor
{
    private SerializedProperty _script;
    private SerializedProperty _initialHandCount;
    private SerializedProperty _handLimit;
    private SerializedProperty _drawCardCost;

    private SerializedProperty _fallbackPlayerHealth;
    private SerializedProperty _fallbackPlayerMaxMana;
    private SerializedProperty _fallbackPlayerStartingMana;

    private SerializedProperty _fallbackEnemyHealth;
    private SerializedProperty _enemyMaxMana;
    private SerializedProperty _startingEnemyMana;

    private SerializedProperty _legacyStartingCardAmount;
    private SerializedProperty _cardsPerTurn;

    private void OnEnable()
    {
        _script = serializedObject.FindProperty("m_Script");

        _initialHandCount = serializedObject.FindProperty("_initialHandCount");
        _handLimit = serializedObject.FindProperty("_handLimit");
        _drawCardCost = serializedObject.FindProperty("_drawCardCost");

        _fallbackPlayerHealth = serializedObject.FindProperty("_fallbackPlayerHealth");
        _fallbackPlayerMaxMana = serializedObject.FindProperty("_fallbackPlayerMaxMana");
        _fallbackPlayerStartingMana = serializedObject.FindProperty("_fallbackPlayerStartingMana");

        _fallbackEnemyHealth = serializedObject.FindProperty("_fallbackEnemyHealth");
        _enemyMaxMana = serializedObject.FindProperty("enemymaxMana");
        _startingEnemyMana = serializedObject.FindProperty("startingEnemeyMana");

        _legacyStartingCardAmount = serializedObject.FindProperty("startingcardAmount");
        _cardsPerTurn = serializedObject.FindProperty("cardToDrawPerTurn");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            if (_script != null)
            {
                EditorGUILayout.PropertyField(_script);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("전투 규칙 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_initialHandCount, new GUIContent("초기 패 카드 수", "전투 시작 시 플레이어 손에 배정되는 카드 수입니다. 난이도 및 초반 전개 속도를 조절할 때 변경하세요."));
        EditorGUILayout.PropertyField(_handLimit, new GUIContent("핸드 최대 보유 수", "한 번에 손에 들 수 있는 카드의 한계치입니다. 이 값을 넘는 드로우는 무시됩니다."));
        EditorGUILayout.PropertyField(_drawCardCost, new GUIContent("수동 드로우 마나 비용", "플레이어가 드로우 버튼을 눌러 카드를 1장 뽑을 때 필요한 마나량입니다."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("플레이어 기본값 (런 데이터 없을 때)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_fallbackPlayerHealth, new GUIContent("기본 체력", "런 정보가 없거나 초기화된 상태에서 전투를 시작할 때 사용할 플레이어 체력입니다."));
        EditorGUILayout.PropertyField(_fallbackPlayerMaxMana, new GUIContent("기본 최대 마나", "런 정보가 없을 때 설정되는 플레이어 최대 마나 수치입니다."));
        EditorGUILayout.PropertyField(_fallbackPlayerStartingMana, new GUIContent("기본 시작 마나", "전투가 시작되자마자 채워지는 플레이어 현재 마나 수치입니다."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("적 기본값 (런 데이터 없을 때)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_fallbackEnemyHealth, new GUIContent("적 기본 체력", "런 정보가 없을 때 적 리더에게 부여되는 기본 체력입니다."));
        EditorGUILayout.PropertyField(_enemyMaxMana, new GUIContent("적 최대 마나", "런 정보가 없을 때 적이 가질 수 있는 최대 마나입니다."));
        EditorGUILayout.PropertyField(_startingEnemyMana, new GUIContent("적 시작 마나", "전투 시작 시 적이 즉시 사용할 수 있는 마나입니다."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("턴 드로우 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_legacyStartingCardAmount, new GUIContent("초기 드로우 카드 수(레거시)", "과거 시스템에서 사용하던 초기 드로우 수입니다. 현재는 사용하지 않지만 호환성을 위해 남겨져 있습니다."));
        EditorGUILayout.PropertyField(_cardsPerTurn, new GUIContent("턴당 자동 드로우 수", "플레이어 턴이 시작될 때 자동으로 손에 들어오는 카드 수입니다."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("추가 설정", EditorStyles.boldLabel);
        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "_initialHandCount", "_handLimit", "_drawCardCost",
            "_fallbackPlayerHealth", "_fallbackPlayerMaxMana", "_fallbackPlayerStartingMana",
            "_fallbackEnemyHealth", "enemymaxMana", "startingEnemeyMana",
            "startingcardAmount", "cardToDrawPerTurn");

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

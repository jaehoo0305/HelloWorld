using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DungeonCombat.Data
{
    /// <summary>
    /// 1레벨부터 5레벨까지, 각 레벨 단계마다 변화하는 패시브의 세부 데이터 구조입니다.
    /// 설명 텍스트 내에 {변수명:값} 형태로 기입하면, 시스템이 이를 자동으로 감지 및 파싱하여 런타임에 활용합니다.
    /// </summary>
    [Serializable]
    public class PassiveLevelData : ISerializationCallbackReceiver
    {
        [Range(1, 5)]
        [SerializeField] private int level;

        [TextArea(6, 10)]
        [SerializeField] private string levelDesc; // 직접 숫자를 포함해 기입 (예: "매 턴 시작 시 자기 주위 {range:3}칸 내에 중력장을 {count:1}개 설치한다.")

        // 런타임 최적화를 위해 직렬화 및 캐싱되는 실시간 파라미터 리스트
        [HideInInspector][SerializeField] private List<string> keys = new List<string>();
        [HideInInspector][SerializeField] private List<float> values = new List<float>();

        // 외부 접근용 프로퍼티
        public int Level => level;
        public string RawLevelDesc => levelDesc;

        /// <summary>
        /// 캐싱된 파라미터 목록에서 특정 수치값을 안전하게 가져옵니다.
        /// </summary>
        public float GetValue(string key, float defaultValue = 0f)
        {
            int index = keys.IndexOf(key.ToLower().Trim());
            if (index != -1 && index < values.Count)
            {
                return values[index];
            }
            return defaultValue;
        }

        /// <summary>
        /// 격자 맵 범위 연산을 위해 추출한 range 값 자동 매핑 프로퍼티
        /// </summary>
        public int Range => Mathf.RoundToInt(GetValue("range", 1f));

        /// <summary>
        /// 중력장 등의 설치 개수 연산을 위해 추출한 count 값 자동 매핑 프로퍼티
        /// </summary>
        public int SpawnCount => Mathf.RoundToInt(GetValue("count", 1f));

        // --- 유니티 직렬화 주기 시 텍스트에서 데이터 실시간 추출 (OnValidate 등과 연동) ---
        public void OnBeforeSerialize()
        {
            ParseParametersFromDescription();
        }

        public void OnAfterDeserialize() { }

        /// <summary>
        /// 정규식을 이용해 텍스트 내부의 {key:value} 패턴을 파싱하여 캐시 리스트를 구축합니다.
        /// </summary>
        public void ParseParametersFromDescription()
        {
            keys.Clear();
            values.Clear();

            if (string.IsNullOrEmpty(levelDesc)) return;

            // 정규식 패턴: {알파벳이름:숫자} -> 예: {range:3}, {count:2}, {gold:20}, {slow:3}
            MatchCollection matches = Regex.Matches(levelDesc, @"\{([a-zA-Z0-9_]+):([\d\.-]+)\}");

            foreach (Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    string key = match.Groups[1].Value.ToLower().Trim();
                    if (float.TryParse(match.Groups[2].Value, out float value))
                    {
                        int existingIndex = keys.IndexOf(key);
                        if (existingIndex != -1)
                        {
                            values[existingIndex] = value;
                        }
                        else
                        {
                            keys.Add(key);
                            values.Add(value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 플레이어 UI 화면에 띄워주기 위해 {key:value} 패턴을 순수 숫자(value) 형태로만 정제해 줍니다.
        /// </summary>
        public string GetFormattedDescription()
        {
            if (string.IsNullOrEmpty(levelDesc)) return string.Empty;

            // {key:value} 패턴을 실시간으로 내부의 "value" 문자열로만 일괄 치환합니다.
            return Regex.Replace(levelDesc, @"\{[a-zA-Z0-9_]+:([\d\.-]+)\}", "$1");
        }

        // 에디터 뷰 가시성용 디버그 헬퍼 리스트 제공
        public List<string> DebugKeys => keys;
        public List<float> DebugValues => values;
    }

    [CreateAssetMenu(fileName = "Passive_", menuName = "Dungeon/Passive Data", order = 3)]
    public class PassiveDataSO : ScriptableObject
    {
        [Header("[ 기본 정보 ]")]
        [SerializeField] private int serialNumber;
        [SerializeField] private string passiveName;

        [Header("[ 5단계 레벨 데이터 리스트 ]")]
        [Tooltip("1레벨부터 5레벨까지 순서대로 데이터를 채워 넣습니다.")]
        [SerializeField] private List<PassiveLevelData> levels = new List<PassiveLevelData>();

        // 외부 접근용 프로퍼티
        public int SerialNumber => serialNumber;
        public string PassiveName => passiveName;
        public IReadOnlyList<PassiveLevelData> Levels => levels;

        /// <summary>
        /// 현재 레벨(0~5)에 부합하는 패시브 실시간 데이터를 안전하게 반환합니다.
        /// </summary>
        public PassiveLevelData GetLevelData(int currentLevel)
        {
            if (currentLevel <= 0 || currentLevel > levels.Count)
            {
                return null;
            }
            return levels[currentLevel - 1];
        }

        /// <summary>
        /// 텍스트 가공을 완벽히 끝마친 최종 출력 설명글을 반환합니다.
        /// </summary>
        public string GetFormattedDescription(int currentLevel)
        {
            PassiveLevelData levelData = GetLevelData(currentLevel);
            if (levelData == null)
            {
                return "미해금 상태입니다.";
            }
            return levelData.GetFormattedDescription();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 인스펙터 값이 수정될 때마다 실시간으로 정규식 추출 파싱을 미리 트리거합니다.
            if (levels == null) return;
            foreach (var level in levels)
            {
                level.ParseParametersFromDescription();
            }
        }
#endif
    }
}

// --- 기획자 편의 및 실시간 추출 변수 가이드라인 제공을 위한 에디터 스크립트 ---
#if UNITY_EDITOR
namespace DungeonCombat.Data
{
    using UnityEditor;

    [CustomPropertyDrawer(typeof(PassiveLevelData))]
    public class PassiveLevelDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty levelProp = property.FindPropertyRelative("level");
            SerializedProperty descProp = property.FindPropertyRelative("levelDesc");

            string elementTitle = $"Level {levelProp.intValue} 설정";
            if (levelProp.intValue == 0) elementTitle = "새로운 레벨 (값을 기입하세요)";

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                elementTitle,
                true
            );

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 1. 레벨 값
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), levelProp);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 2. 가이드라인 박스 표시
                Rect helpRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight * 2f));
                EditorGUI.HelpBox(helpRect, "설명란에 {range:3}, {count:2}, {gold:20} 처럼 입력하면 시스템이 감지하여 실시간 패시브 데이터로 자동 활용합니다.", MessageType.Info);
                yOffset += (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;

                // 3. 설명 라벨 표시
                Rect descLabelRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                EditorGUI.LabelField(descLabelRect, "텍스트 기반 일체형 툴팁 설명", EditorStyles.boldLabel);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 4. 설명 TextArea 영역
                float descHeight = EditorGUIUtility.singleLineHeight * 5f;
                Rect descTextRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, descHeight));
                descProp.stringValue = EditorGUI.TextArea(descTextRect, descProp.stringValue);
                yOffset += descHeight + EditorGUIUtility.standardVerticalSpacing;

                // 5. 텍스트로부터 파싱되어 추출된 결과물 실시간 미리보기 리스트 출력
                Rect headerRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                EditorGUI.LabelField(headerRect, "[ 실시간 추출된 시스템 스탯 목록 ]", EditorStyles.boldLabel);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 가상의 타겟 구조로부터 List 값 추출하여 읽기전용으로 표시
                PassiveLevelData targetData = GetTargetObject(property);
                if (targetData != null && targetData.DebugKeys != null && targetData.DebugKeys.Count > 0)
                {
                    for (int i = 0; i < targetData.DebugKeys.Count; i++)
                    {
                        Rect valRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                        string keyName = targetData.DebugKeys[i];
                        float val = targetData.DebugValues[i];

                        string suffix = "";
                        if (keyName == "range") suffix = " (고정 범위)";
                        else if (keyName == "count") suffix = " (설치 개수)";

                        EditorGUI.LabelField(valRect, $"  ■ {keyName}{suffix}  =  {val}");
                        yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
                else
                {
                    Rect valRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                    EditorGUI.LabelField(valRect, "  (추출된 변수가 없습니다. 포맷에 맞추어 적어보세요!)", EditorStyles.miniLabel);
                    yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private PassiveLevelData GetTargetObject(SerializedProperty property)
        {
            string path = property.propertyPath;
            object obj = property.serializedObject.targetObject;

            if (path.Contains("["))
            {
                int indexStart = path.IndexOf("[") + 1;
                int indexEnd = path.IndexOf("]");
                if (int.TryParse(path.Substring(indexStart, indexEnd - indexStart), out int index))
                {
                    PassiveDataSO so = obj as PassiveDataSO;
                    if (so != null && so.Levels != null && index < so.Levels.Count)
                    {
                        return so.Levels[index];
                    }
                }
            }
            return null;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            // 고정 행: Foldout(1) + Level(1) + HelpBox(2) + DescLabel(1) + TextArea(5) + StatsHeader(1) = 11 행
            int fixedRows = 11;
            float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * fixedRows;

            // 실시간 추출된 파라미터 개수에 따른 가변 높이 추가
            PassiveLevelData targetData = GetTargetObject(property);
            int dynamicRows = 1;
            if (targetData != null && targetData.DebugKeys != null && targetData.DebugKeys.Count > 0)
            {
                dynamicRows = targetData.DebugKeys.Count;
            }

            height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * dynamicRows;
            height += EditorGUIUtility.standardVerticalSpacing * 2;
            return height;
        }
    }
}
#endif
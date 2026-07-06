using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DungeonCombat.Data
{
    /// <summary>
    /// 1레벨부터 5레벨까지, 각 레벨 단계마다 변화하는 스킬의 세부 데이터입니다.
    /// 설명 텍스트 내에 {변수명:값} 형태로 적으면, 시스템이 이를 자동으로 감지 및 파싱하여 가로/세로 범위, 데미지 등으로 활용합니다.
    /// </summary>
    [Serializable]
    public class SkillLevelData : ISerializationCallbackReceiver
    {
        [Range(1, 5)]
        [SerializeField] private int level;

        [TextArea(6, 10)]
        [SerializeField] private string levelDesc; // 직접 숫자를 포함해 기입 (예: "{rangex:3}x{rangey:5} 크기로 끌어당기며 최초 피해 {dmg:250}%를...")

        // 런타임 최적화를 위해 직렬화 및 캐싱되는 추출 변수 리스트
        [HideInInspector][SerializeField] private List<string> keys = new List<string>();
        [HideInInspector][SerializeField] private List<float> values = new List<float>();

        // 외부 접근용 프로퍼티
        public int Level => level;
        public string RawLevelDesc => levelDesc;

        /// <summary>
        /// 캐싱된 파라미터 딕셔너리에서 특정 값을 가져옵니다. (상태이상 수치 등 자율 확장 가능)
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
        /// 격자 맵 범위 연산을 위한 물리 스탯 자동 매핑 프로퍼티 (가로/세로 지원)
        /// </summary>
        public int RangeX => Mathf.RoundToInt(GetValue("rangex", GetValue("range", 1f))); // {rangex:3} 또는 {range:3} 감지
        public int RangeY => Mathf.RoundToInt(GetValue("rangey", GetValue("range", 1f))); // {rangey:5} 또는 {range:5} 감지

        /// <summary>
        /// 단일 정수형 범위를 원할 때 사용하는 하위 호환용 프로퍼티
        /// </summary>
        public int Range => Mathf.RoundToInt(GetValue("range", GetValue("rangex", 1f)));

        /// <summary>
        /// 전투 데미지 난수 기준 배율 계산 프로퍼티
        /// </summary>
        public float DamageModifier => GetValue("dmg", 100f) / 100f; // {dmg:250} 입력 시 2.5f 반환

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

            return Regex.Replace(levelDesc, @"\{[a-zA-Z0-9_]+:([\d\.-]+)\}", "$1");
        }

        public List<string> DebugKeys => keys;
        public List<float> DebugValues => values;
    }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Dungeon/Skill Data", order = 2)]
    public class SkillDataSO : ScriptableObject
    {
        [Header("[ 기본 정보 ]")]
        [SerializeField] private int serialNumber;
        [SerializeField] private string skillName;

        [Header("[ 고정 전투 메커니즘 ]")]
        [SerializeField] private TargetType targetType;
        [SerializeField] private int requiredSP; // 사용 SP 수치는 모든 레벨 공통 고정이므로 상위로 이전 완료
        [SerializeField] private bool isEndsTurn = true;

        [Header("[ 5단계 레벨 데이터 리스트 ]")]
        [Tooltip("1레벨부터 5레벨까지 순서대로 데이터를 채워 넣습니다.")]
        [SerializeField] private List<SkillLevelData> levels = new List<SkillLevelData>();

        [Header("[ 궁극기 및 Z스킬 강화 정보 ]")]
        [SerializeField] private bool isUltimate;
        [SerializeField] private SkillDataSO enhancedSkillAsset;

        [TextArea(2, 4)]
        [Tooltip("UI 툴팁 버튼을 켰을 때 유저에게 보여줄 한글 강화 조건 설명입니다.")]
        [SerializeField] private string enhanceConditionDesc;

        [Tooltip("시스템 코드에서 해당 스킬의 특수 강화 로직을 구별해내기 위한 식별용 문자열 키입니다.")]
        [SerializeField] private string enhanceLogicKey;

        // --- 외부 접근용 읽기 전용 프로퍼티 ---
        public int SerialNumber => serialNumber;
        public string SkillName => skillName;
        public TargetType TargetType => targetType;
        public int RequiredSP => requiredSP;
        public bool IsEndsTurn => isEndsTurn;
        public IReadOnlyList<SkillLevelData> Levels => levels;
        public bool IsUltimate => isUltimate;
        public SkillDataSO EnhancedSkillAsset => enhancedSkillAsset;
        public string EnhanceConditionDesc => enhanceConditionDesc;
        public string EnhanceLogicKey => enhanceLogicKey;

        /// <summary>
        /// 현재 레벨(0~5)에 부합하는 가변 세부 데이터를 안전하게 반환합니다.
        /// </summary>
        public SkillLevelData GetLevelData(int currentLevel)
        {
            if (currentLevel <= 0 || currentLevel > levels.Count)
            {
                return null;
            }
            return levels[currentLevel - 1];
        }

        /// <summary>
        /// 텍스트 가공을 완벽히 끝마친 최종 출력 설명글을 반환합니다.
        /// 기획 요구 명세에 부합하도록 강화(Ultimate)형 스킬의 경우 본문 하단에 개행 2번 후 '조건:' 명세를 추가합니다.
        /// </summary>
        public string GetFormattedDescription(int currentLevel)
        {
            SkillLevelData levelData = GetLevelData(currentLevel);
            if (levelData == null)
            {
                return "미해금 상태입니다.";
            }

            string baseDesc = levelData.GetFormattedDescription();

            // 만약 궁극/Z-Skill이거나 조건 명세가 들어있다면, 요구 조건 텍스트를 개행 2번하여 이쁘게 붙여 줍니다.
            if (!string.IsNullOrEmpty(enhanceConditionDesc))
            {
                baseDesc += $"\n\n조건: {enhanceConditionDesc}";
            }

            return baseDesc;
        }

        // --- [수술적 추가] 스킬의 다형적 발사 흐름을 처리하기 위한 가상 실행 메서드 훅 ---

        /// <summary>
        /// 스킬이 최종 타격지에 발사될 때의 구체적인 연출 및 로직 처리를 전담합니다.
        /// 개별 투사체나 기획 전용 코드가 완수되면 마지막에 onComplete 콜백을 당겨 줍니다.
        /// </summary>
        public virtual void Execute(DungeonCombat.Combat.PlayerUnit caster, Vector2Int targetCoord, int level, Action onComplete)
        {
            // 기본 스킬들의 경우, 특별한 연출 없이 즉시 완료 콜백을 쏘아줍니다.
            onComplete?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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

    [CustomPropertyDrawer(typeof(SkillLevelData))]
    public class SkillLevelDataDrawer : PropertyDrawer
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

                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), levelProp);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                Rect helpRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight * 2f));
                EditorGUI.HelpBox(helpRect, "설명란에 {rangex:3}, {rangey:5}, {dmg:250}, {slow:3} 처럼 입력하면 시스템이 감지하여 실시간 전투 데이터로 자동 활용합니다.", MessageType.Info);
                yOffset += (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;

                Rect descLabelRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                EditorGUI.LabelField(descLabelRect, "텍스트 기반 일체형 툴팁 설명", EditorStyles.boldLabel);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                float descHeight = EditorGUIUtility.singleLineHeight * 5f;
                Rect descTextRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, descHeight));
                descProp.stringValue = EditorGUI.TextArea(descTextRect, descProp.stringValue);
                yOffset += descHeight + EditorGUIUtility.standardVerticalSpacing;

                Rect headerRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                EditorGUI.LabelField(headerRect, "[ 실시간 추출된 시스템 스탯 목록 ]", EditorStyles.boldLabel);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                SkillLevelData targetData = GetTargetObject(property);
                if (targetData != null && targetData.DebugKeys != null && targetData.DebugKeys.Count > 0)
                {
                    for (int i = 0; i < targetData.DebugKeys.Count; i++)
                    {
                        Rect valRect = EditorGUI.IndentedRect(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight));
                        string keyName = targetData.DebugKeys[i];
                        float val = targetData.DebugValues[i];

                        string suffix = "";
                        if (keyName == "range") suffix = " (고정 Range)";
                        else if (keyName == "rangex") suffix = " (가로 Range)";
                        else if (keyName == "rangey") suffix = " (세로 Range)";
                        else if (keyName == "dmg") suffix = $" (Dmg 배율: {val / 100f}배)";
                        else if (keyName == "slow") suffix = " (둔화 지속 턴)";

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

        private SkillLevelData GetTargetObject(SerializedProperty property)
        {
            string path = property.propertyPath;
            object obj = property.serializedObject.targetObject;

            if (path.Contains("["))
            {
                int indexStart = path.IndexOf("[") + 1;
                int indexEnd = path.IndexOf("]");
                if (int.TryParse(path.Substring(indexStart, indexEnd - indexStart), out int index))
                {
                    SkillDataSO so = obj as SkillDataSO;
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

            int fixedRows = 11;
            float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * fixedRows;

            SkillLevelData targetData = GetTargetObject(property);
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
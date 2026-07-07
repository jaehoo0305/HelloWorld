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
        [SerializeField] private string levelDesc; // 직접 숫자를 포함해 기입 (예: "{range:7}칸 거리까지 투사체를 날려 피해(Dmg {dmg:80}%)를...")

        // 런타임 최적화를 위해 직렬화 및 캐싱되는 추출 변수 리스트
        [HideInInspector][SerializeField] private List<string> keys = new List<string>();
        [HideInInspector][SerializeField] private List<float> values = new List<float>();

        // 외부 접근용 프로퍼티
        public int Level => level;
        public string RawLevelDesc => levelDesc;

        /// <summary>
        /// 캐싱된 파라미터 딕셔너리에서 특정 값을 가져옵니다. (안전 가드 추가)
        /// </summary>
        public float GetValue(string key, float defaultValue = 0f)
        {
            // [자가 수복 완료]: 유니티 직렬화 주기 중 null이 넘어올 경우의 강철 가드 배치
            if (keys == null || values == null) return defaultValue;

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
        public int RangeX => Mathf.RoundToInt(GetValue("rangex", GetValue("range", 1f)));
        public int RangeY => Mathf.RoundToInt(GetValue("rangey", GetValue("range", 1f)));

        /// <summary>
        /// 단일 정수형 범위를 원할 때 사용하는 하위 호환용 프로퍼티 (스킬 고유 최대 사거리 반환)
        /// </summary>
        public int Range => Mathf.RoundToInt(GetValue("range", GetValue("rangex", 1f)));

        /// <summary>
        /// 전투 데미지 난수 기준 배율 계산 프로퍼티
        /// </summary>
        public float DamageModifier => GetValue("dmg", 100f) / 100f;

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
            if (keys == null) keys = new List<string>();
            if (values == null) values = new List<float>();

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

    /// <summary>
    /// 모든 스킬 에셋의 공통 부모 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "Dungeon/Skill Data", order = 2)]
    public class SkillDataSO : ScriptableObject
    {
        [Header("[ 기본 정보 ]")]
        [SerializeField] private int serialNumber;
        [SerializeField] private string skillName;

        [Header("[ 고정 전투 메커니즘 ]")]
        [SerializeField] private TargetType targetType;
        [SerializeField] private int requiredSP;
        [SerializeField] private bool isEndsTurn = true;

        [Header("[ 빈 지면 타겟팅 여부 ]")]
        [Tooltip("적 유닛이 없는 빈 땅이나 빈 타일에도 조준하여 쏠 수 있도록 허용합니까? (장판 설치 스킬 필수)")]
        [SerializeField] private bool canTargetEmptyGround = false;

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
        public bool CanTargetEmptyGround => canTargetEmptyGround;
        public IReadOnlyList<SkillLevelData> Levels => levels;
        public bool IsUltimate => isUltimate;
        public SkillDataSO EnhancedSkillAsset => enhancedSkillAsset;
        public string EnhanceConditionDesc => enhanceConditionDesc;
        public string EnhanceLogicKey => enhanceLogicKey;

        public SkillLevelData GetLevelData(int currentLevel)
        {
            if (currentLevel <= 0 || currentLevel > levels.Count) return null;
            return levels[currentLevel - 1];
        }

        /// <summary>
        /// 텍스트 가공을 완벽히 끝마친 최종 출력 설명글을 반환합니다.
        /// </summary>
        public string GetFormattedDescription(int currentLevel)
        {
            SkillLevelData levelData = GetLevelData(currentLevel);
            if (levelData == null) return "미해금 상태입니다.";

            string baseDesc = levelData.GetFormattedDescription();

            // [수정 완료]: 오직 인게임에서 Z스킬 모드가 켜졌을 때만 정확히 개행 2번 후 조건을 붙여 UI에 표출합니다.
            if (DungeonCombat.Combat.BattleUIController.IsZSkillModeActive)
            {
                if (!string.IsNullOrEmpty(enhanceConditionDesc))
                {
                    baseDesc += $"\n\n조건: {enhanceConditionDesc}";
                }
                else if (!string.IsNullOrEmpty(enhanceLogicKey))
                {
                    baseDesc += $"\n\n조건: {GetEnhanceConditionKoreanText(enhanceLogicKey)}";
                }
                else if (DungeonCombat.Combat.SkillCaster.Instance != null)
                {
                    string cachedCond = DungeonCombat.Combat.SkillCaster.Instance.GetActiveSkillEnhanceConditionText();
                    if (!string.IsNullOrEmpty(cachedCond))
                    {
                        baseDesc += $"\n\n조건: {cachedCond}";
                    }
                }
            }

            return baseDesc;
        }

        private string GetEnhanceConditionKoreanText(string key)
        {
            switch (key.ToLower().Trim())
            {
                case "isa_cond_masscollapse":
                    return "중력장 최소 하나 존재, 소모한 누적 SP 10개";
                case "isa_cond_heatdeath":
                    return "중력장 최소 하나 존재, 4라운드 이상 경과";
                default:
                    return "강화 특수 조건 필요";
            }
        }

        public virtual void Execute(DungeonCombat.Combat.PlayerUnit caster, Vector2Int targetCoord, int level, Action onComplete)
        {
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
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

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
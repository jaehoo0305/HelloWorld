using System.Collections.Generic;
using UnityEngine;

namespace DungeonCombat.Data
{
    [CreateAssetMenu(fileName = "Character_", menuName = "Dungeon/Character Data", order = 1)]
    public class CharacterDataSO : ScriptableObject
    {
        [Header("[ 기본 정보 ]")]
        [SerializeField] private int serialNumber;
        [SerializeField] private string characterName;
        [SerializeField] private PositionType positionType;

        [Header("[ 전투 5대 스탯 ]")]
        [SerializeField] private int maxHP = 100;
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [Range(0f, 1f)]
        [SerializeField] private float defenseCap = 0.8f; // 다키스트 던전식 데미지 감소 상한선
        [SerializeField] private int speed;
        [Range(0f, 1f)]
        [SerializeField] private float critRate = 0.05f;

        [Header("[ 보스 전용 규칙 ]")]
        [Min(1)]
        [SerializeField] private int actionCountPerRound = 1; // 보스일 경우 한 라운드 행동 횟수 증가

        [Header("[ 보유 패시브 & 스킬 풀 ]")]
        [SerializeField] private PassiveDataSO passiveSkill; // 보완된 패시브 데이터 참조 (이름, 설명, 로직 타입을 통틀어 관리)
        [SerializeField] private List<SkillDataSO> learnableSkills = new List<SkillDataSO>();

        // 외부 접근용 프로퍼티
        public int SerialNumber => serialNumber;
        public string CharacterName => characterName;
        public PositionType PositionType => positionType;
        public int MaxHP => maxHP;
        public int Attack => attack;
        public int Defense => defense;
        public float DefenseCap => defenseCap;
        public int Speed => speed;
        public float CritRate => critRate;
        public int ActionCountPerRound => actionCountPerRound;
        public PassiveDataSO PassiveSkill => passiveSkill;
        public IReadOnlyList<SkillDataSO> LearnableSkills => learnableSkills;
    }
}   
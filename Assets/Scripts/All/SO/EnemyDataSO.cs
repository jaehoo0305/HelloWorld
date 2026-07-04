using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCombat.Data
{
    /// <summary>
    /// 적의 종족을 분류하는 열거형입니다.
    /// </summary>
    public enum EnemyRace
    {
        Slime,      // 슬라임
        Beast,      // 야수
        Undead,     // 언데드
        Humanoid,   // 수인 및 인간형
        Golem,      // 골렘
        Demon       // 악마
    }

    /// <summary>
    /// 적의 인공지능 성향을 결정하는 열거형입니다.
    /// </summary>
    public enum EnemyAIType
    {
        General,    // 일반 AI (공격 중심)
        Defensive,  // 방어 중심 AI
        Evasive     // 도망 중심 AI
    }

    /// <summary>
    /// 적이 아군 중 누구를 먼저 공격할지 고르는 타겟팅 필터 규칙입니다.
    /// </summary>
    public enum TargetPriorityType
    {
        LowestCurrentHP,    // 현재 체력이 가장 낮은 대상
        HighestMaxHP,       // 최대 체력이 가장 높은 대상
        Nearest,            // 거리가 가장 가까운 대상
        Farthest,           // 거리가 가장 먼 대상 (기존 far 대응)
        Random              // 무작위 대상
    }

    /// <summary>
    /// 적의 전반적인 전투 역할군 성향입니다.
    /// </summary>
    public enum CombatRoleType
    {
        Melee,      // 근거리 위주
        Ranged,     // 원거리 위주
        Support     // 서포팅 위주
    }

    /// <summary>
    /// 적 캐릭터의 스탯, 이동 범위, AI 분류 및 스킬 정보를 관리하는 ScriptableObject 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "Dungeon/Enemy Data", order = 4)]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("[ 기본 정보 ]")]
        [SerializeField] private int serialNumber;
        [SerializeField] private string enemyName;
        [SerializeField] private EnemyRace race;

        [Header("[ 전투 스탯 ]")]
        [SerializeField] private int maxHP = 50;
        [SerializeField] private int attack = 10;
        [SerializeField] private int defense = 5;
        [SerializeField] private int speed = 10;

        [Header("[ 이동 및 행동 제한 ]")]
        [Min(1)]
        [SerializeField] private int maxMoveDistance = 3; // 턴당 최대 이동 가능 타일 수

        [Header("[ 인공지능 및 타겟팅 설정 ]")]
        [SerializeField] private EnemyAIType aiType = EnemyAIType.General;
        [SerializeField] private TargetPriorityType targetPriority = TargetPriorityType.LowestCurrentHP;
        [SerializeField] private CombatRoleType combatRole = CombatRoleType.Melee;

        [Header("[ 보유 패시브 ]")]
        [SerializeField] private PassiveDataSO passiveSkill;

        [Header("[ 사용 가능 기술 풀 ]")]
        [SerializeField] private List<SkillDataSO> usableSkills = new List<SkillDataSO>();

        // 외부 런타임 연산에서 데이터를 안전하게 참조하기 위한 읽기 전용 프로퍼티입니다.
        public int SerialNumber => serialNumber;
        public string EnemyName => enemyName;
        public EnemyRace Race => race;
        public int MaxHP => maxHP;
        public int Attack => attack;
        public int Defense => defense;
        public int Speed => speed;
        public int MaxMoveDistance => maxMoveDistance;
        public EnemyAIType AIType => aiType;
        public TargetPriorityType TargetPriority => targetPriority;
        public CombatRoleType CombatRole => combatRole;

        public PassiveDataSO PassiveSkill => passiveSkill;
        public IReadOnlyList<SkillDataSO> UsableSkills => usableSkills;
    }
}
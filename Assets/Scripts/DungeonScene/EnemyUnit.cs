using System;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전투 씬에 배치된 몬스터 오브젝트에 부착되는 실시간 전투 정보 관리 컴포넌트입니다.
    /// </summary>
    public class EnemyUnit : BattleUnit
    {
        [Header("[ 기반 데이터 에셋 ]")]
        [SerializeField] private EnemyDataSO enemyData;

        [Header("[ 방어력 관련 설정 ]")]
        [Range(0f, 1f)]
        [SerializeField] private float defenseCap = 0.8f;

        // --- 추상 프로퍼티 구현 ---
        public override int MaxHP => enemyData != null ? enemyData.MaxHP : 50;
        public override int Speed => enemyData != null ? enemyData.Speed : 10;
        public override string UnitName => enemyData != null ? enemyData.EnemyName : "Enemy";

        // 체력이 높은 단두대급 정예/보스 몬스터 판단
        public override bool IsBoss => enemyData != null && enemyData.MaxHP >= 150;
        public override int ActionCountPerRound => IsBoss ? 2 : 1;
        public override PassiveDataSO PassiveSkill => enemyData != null ? enemyData.PassiveSkill : null;
        public override int PassiveLevel => 1; // 몬스터 패시브의 기본 레벨 규격

        // --- 적 AI 상태 값 (이동 및 행동 처리용) ---
        public bool HasMovedThisTurn { get; set; }
        public bool HasAttackedThisTurn { get; set; }

        public EnemyDataSO EnemyData => enemyData;
        public float DefenseCap => defenseCap;

        private void Start()
        {
            InitializeUnit();
        }

        public void InitializeUnit()
        {
            if (enemyData == null)
            {
                Debug.LogError($"[EnemyUnit] '{gameObject.name}' 오브젝트에 EnemyDataSO가 등록되어 있지 않습니다!");
                return;
            }

            CurrentHP = enemyData.MaxHP;
            HasMovedThisTurn = false;
            HasAttackedThisTurn = false;

            TriggerAllEvents();
        }

        public override void TakeDamage(int rawDamage)
        {
            if (CurrentHP <= 0) return;

            float reductionPercent = Mathf.Min(enemyData.Defense * 0.01f, defenseCap);
            int blockedDamage = Mathf.RoundToInt(rawDamage * reductionPercent);
            int finalDamage = Mathf.Max(1, rawDamage - blockedDamage);

            CurrentHP = Mathf.Max(0, CurrentHP - finalDamage);
            Debug.Log($"[전투] 적 {UnitName} 피격! 방어 차감 후 실제 피해: {finalDamage} (남은 체력: {CurrentHP})");

            InvokeHPChanged(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
            {
                InvokeDeath();
            }
        }

        public void ResetTurnState()
        {
            HasMovedThisTurn = false;
            HasAttackedThisTurn = false;
        }
    }
}
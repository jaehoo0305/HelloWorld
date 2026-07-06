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

        /// <summary>
        /// 둔화 상태일 때는 몬스터의 속도가 25% 감소(최소 1 감소)하여 다음 라운드 턴 배치 계산에 쓰입니다.
        /// </summary>
        public override int Speed
        {
            get
            {
                int baseSpeed = enemyData != null ? enemyData.Speed : 10;
                if (IsSlowed)
                {
                    // 25% 감소하되, 기획에 따라 최소 1 감소 보장
                    int reduction = Mathf.Max(1, Mathf.RoundToInt(baseSpeed * 0.25f));
                    return Mathf.Max(1, baseSpeed - reduction);
                }
                return baseSpeed;
            }
        }

        public override string UnitName => enemyData != null ? enemyData.EnemyName : "Enemy";

        public override bool IsBoss => enemyData != null && enemyData.MaxHP >= 150;
        public override int ActionCountPerRound => IsBoss ? 2 : 1;
        public override PassiveDataSO PassiveSkill => enemyData != null ? enemyData.PassiveSkill : null;
        public override int PassiveLevel => 1;

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

            // [취약 규칙 추가 적용]: 취약 상태라면 받는 피해가 25% 증가 (최소 1 증가)
            if (IsVulnerable)
            {
                int extraDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * 0.25f));
                rawDamage += extraDamage;
            }

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

        /// <summary>
        /// 몬스터의 턴 시작 시 둔화 디버프 지속 턴 수를 삭감하고 이펙트 소멸 주기를 가동시킵니다.
        /// </summary>
        public override void RecoverSPOnTurnStart()
        {
            base.RecoverSPOnTurnStart();
            TickStatusEffects();
        }

        public void ResetTurnState()
        {
            HasMovedThisTurn = false;
            HasAttackedThisTurn = false;
        }
    }
}
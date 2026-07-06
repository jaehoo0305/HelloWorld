using System;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전장에 배치되는 모든 전투 개체(아군 및 적군)의 최상위 추상 클래스입니다.
    /// 상태이상(둔화, 속박, 취약) 데이터 및 비주얼 이펙트 수명주기를 공통 관리합니다.
    /// </summary>
    public abstract class BattleUnit : MonoBehaviour
    {
        [Header("[ 그리드 배치 설정 ]")]
        [Tooltip("이 유닛이 전장에서 시작할 가상 격자 좌표(X, Y)입니다.")]
        [SerializeField] private Vector2Int initialGridPosition;

        public int CurrentHP { get; protected set; }
        public Vector2Int InitialGridPosition => initialGridPosition;

        // --- 공통 추상 프로퍼티 (매니저 및 UI가 참조할 핵심 데이터) ---
        public abstract int MaxHP { get; }
        public abstract int Speed { get; }
        public abstract string UnitName { get; }
        public abstract bool IsBoss { get; }
        public abstract int ActionCountPerRound { get; }
        public abstract PassiveDataSO PassiveSkill { get; }
        public abstract int PassiveLevel { get; }

        // --- 상태이상(Status Effects) 지속시간 및 상태 관리 ---
        public int SlowDuration { get; protected set; }
        public int BindDuration { get; protected set; }
        public int VulnerableDuration { get; protected set; }

        public bool IsSlowed => SlowDuration > 0;
        public bool IsBound => BindDuration > 0;
        public bool IsVulnerable => VulnerableDuration > 0;

        // 실시간 생성되어 부모-자식으로 따라다닐 상태이상 비주얼 게임오브젝트
        protected GameObject activeSlowVisual;
        protected GameObject activeBindVisual;
        protected GameObject activeVulnerableVisual;

        // --- 공통 핵심 이벤트 ---
        public event Action<int, int> OnHPChanged;
        public event Action OnDeath;

        // --- 플레이어 UI 연동용 가상 자원 이벤트 (에러 방지를 위한 가상 메서드 처리) ---
        public virtual event Action<int, int, int> OnSPChanged;
        public virtual event Action<int, int> OnOverheatChanged;
        public virtual int CurrentSP => 0;
        public virtual int CurrentBankSP => 0;
        public virtual int CurrentOverheat => 0;

        public abstract void TakeDamage(int rawDamage);

        protected void InvokeHPChanged(int current, int max)
        {
            OnHPChanged?.Invoke(current, max);
        }

        protected void InvokeDeath()
        {
            OnDeath?.Invoke();
        }

        public virtual void RecoverSPOnTurnStart() { }

        public virtual void TriggerAllEvents()
        {
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        // --- 상태이상 공통 부여 API (비주얼 생성 포함) ---

        /// <summary>
        /// 유닛에게 둔화 디버프를 부여하고 연출용 오라 파티클을 발밑에 생성합니다.
        /// </summary>
        public virtual void ApplySlow(int duration, GameObject visualPrefab = null)
        {
            SlowDuration = Mathf.Max(SlowDuration, duration);
            if (visualPrefab != null && activeSlowVisual == null)
            {
                activeSlowVisual = Instantiate(visualPrefab, transform.position, Quaternion.identity, transform);
            }
            Debug.Log($"[{UnitName}] 둔화 {duration}턴 부여 완료.");
        }

        /// <summary>
        /// 유닛에게 속박 디버프를 부여하고 연출용 파티클을 생성합니다.
        /// </summary>
        public virtual void ApplyBind(int duration, GameObject visualPrefab = null)
        {
            BindDuration = Mathf.Max(BindDuration, duration);
            if (visualPrefab != null && activeBindVisual == null)
            {
                activeBindVisual = Instantiate(visualPrefab, transform.position, Quaternion.identity, transform);
            }
            Debug.Log($"[{UnitName}] 속박 {duration}턴 부여 완료.");
        }

        /// <summary>
        /// 유닛에게 취약 디버프를 부여하고 연출용 파티클을 생성합니다.
        /// </summary>
        public virtual void ApplyVulnerable(int duration, GameObject visualPrefab = null)
        {
            VulnerableDuration = Mathf.Max(VulnerableDuration, duration);
            if (visualPrefab != null && activeVulnerableVisual == null)
            {
                activeVulnerableVisual = Instantiate(visualPrefab, transform.position, Quaternion.identity, transform);
            }
            Debug.Log($"[{UnitName}] 취약 {duration}턴 부여 완료.");
        }

        /// <summary>
        /// 매 턴 시작 시 호출되어 상태이상의 지속시간을 차감하고 수명이 다한 비주얼을 소멸시킵니다.
        /// </summary>
        protected virtual void TickStatusEffects()
        {
            if (SlowDuration > 0)
            {
                SlowDuration--;
                if (SlowDuration <= 0 && activeSlowVisual != null)
                {
                    Destroy(activeSlowVisual);
                }
            }

            if (BindDuration > 0)
            {
                BindDuration--;
                if (BindDuration <= 0 && activeBindVisual != null)
                {
                    Destroy(activeBindVisual);
                }
            }

            if (VulnerableDuration > 0)
            {
                VulnerableDuration--;
                if (VulnerableDuration <= 0 && activeVulnerableVisual != null)
                {
                    Destroy(activeVulnerableVisual);
                }
            }
        }
    }
}
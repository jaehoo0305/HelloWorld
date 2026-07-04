using System;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전장에 배치되는 모든 전투 개체(아군 및 적군)의 최상위 추상 클래스입니다.
    /// </summary>
    public abstract class BattleUnit : MonoBehaviour
    {
        public int CurrentHP { get; protected set; }

        // --- 공통 추상 프로퍼티 (매니저 및 UI가 참조할 핵심 데이터) ---
        public abstract int MaxHP { get; }
        public abstract int Speed { get; }
        public abstract string UnitName { get; }
        public abstract bool IsBoss { get; }
        public abstract int ActionCountPerRound { get; }
        public abstract PassiveDataSO PassiveSkill { get; }
        public abstract int PassiveLevel { get; }

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
    }
}
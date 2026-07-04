using System;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 캐릭터가 장착할 고유 스킬 선택 영역을 나타내는 열거형입니다.
    /// </summary>
    public enum UniqueSkillSelection
    {
        UniqueSkill1,
        UniqueSkill2
    }

    /// <summary>
    /// 플레이어가 직접 조종하는 아군 영웅 캐릭터의 실시간 전투 자원 관리 클래스입니다.
    /// </summary>
    public class PlayerUnit : BattleUnit
    {
        [Header("[ 기반 데이터 에셋 ]")]
        [SerializeField] private CharacterDataSO characterData;

        [Header("[ 실시간 인게임 레벨 및 스킬 세팅 ]")]
        [SerializeField] private UniqueSkillSelection equippedUniqueSkill = UniqueSkillSelection.UniqueSkill1;
        [Range(1, 5)][SerializeField] private int currentPassiveLevel = 1;
        [Range(1, 5)][SerializeField] private int currentSkill1Level = 1;
        [Range(1, 5)][SerializeField] private int currentSkill2Level = 1;

        // --- 추상 프로퍼티 구현 ---
        public override int MaxHP => characterData != null ? characterData.MaxHP : 100;
        public override int Speed => characterData != null ? characterData.Speed : 10;
        public override string UnitName => characterData != null ? characterData.CharacterName : "Hero";
        public override bool IsBoss => characterData != null && characterData.PositionType == PositionType.Boss;
        public override int ActionCountPerRound => characterData != null ? characterData.ActionCountPerRound : 1;
        public override PassiveDataSO PassiveSkill => characterData != null ? characterData.PassiveSkill : null;
        public override int PassiveLevel => currentPassiveLevel;

        // --- 플레이어 전용 가변 자원 ---
        private int currentSP;
        private int currentBankSP;
        private int currentOverheat;

        public override int CurrentSP => currentSP;
        public override int CurrentBankSP => currentBankSP;
        public override int CurrentOverheat => currentOverheat;

        public CharacterDataSO CharacterData => characterData;
        public int Skill1Level => currentSkill1Level;
        public int Skill2Level => currentSkill2Level;

        public override event Action<int, int, int> OnSPChanged;
        public override event Action<int, int> OnOverheatChanged;

        public SkillDataSO EquippedUniqueSkill => (equippedUniqueSkill == UniqueSkillSelection.UniqueSkill1)
            ? characterData.UniqueSkill1
            : characterData.UniqueSkill2;

        public int EquippedUniqueSkillLevel => (equippedUniqueSkill == UniqueSkillSelection.UniqueSkill1)
            ? currentSkill1Level
            : currentSkill2Level;

        private void Start()
        {
            InitializeUnit();
        }

        public void InitializeUnit()
        {
            if (characterData == null)
            {
                Debug.LogError($"[PlayerUnit] '{gameObject.name}' 오브젝트에 CharacterDataSO가 등록되어 있지 않습니다!");
                return;
            }

            CurrentHP = characterData.MaxHP;
            currentSP = CombatConfig.TURN_START_SP_RECOVERY;
            currentBankSP = 0;
            currentOverheat = 0;

            TriggerAllEvents();
        }

        public override void TakeDamage(int rawDamage)
        {
            if (CurrentHP <= 0) return;

            float reductionPercent = Mathf.Min(characterData.Defense * 0.01f, characterData.DefenseCap);
            int blockedDamage = Mathf.RoundToInt(rawDamage * reductionPercent);
            int finalDamage = Mathf.Max(1, rawDamage - blockedDamage);

            CurrentHP = Mathf.Max(0, CurrentHP - finalDamage);
            Debug.Log($"[전투] {UnitName} 피격! 방어 차감 후 실제 피해: {finalDamage} (남은 체력: {CurrentHP})");

            InvokeHPChanged(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
            {
                InvokeDeath();
            }
        }

        public bool ConsumeSP(int amount)
        {
            int totalAvailableSP = currentSP + currentBankSP;
            if (totalAvailableSP < amount)
            {
                Debug.LogWarning($"[전투] {UnitName}의 SP가 부족하여 스킬을 사용할 수 없습니다.");
                return false;
            }

            int remainingCost = amount;
            if (currentBankSP >= remainingCost)
            {
                currentBankSP -= remainingCost;
                remainingCost = 0;
            }
            else
            {
                remainingCost -= currentBankSP;
                currentBankSP = 0;
                currentSP -= remainingCost;
            }

            OnSPChanged?.Invoke(currentSP, currentBankSP, CombatConfig.MAX_SP);

            int overheatGain = amount * CombatConfig.OVERHEAT_PER_SP;
            AddOverheat(overheatGain);

            return true;
        }

        public override void RecoverSPOnTurnStart()
        {
            int spRecovery = CombatConfig.TURN_START_SP_RECOVERY;

            if (currentSP + spRecovery > CombatConfig.MAX_SP)
            {
                int overflownSP = (currentSP + spRecovery) - CombatConfig.MAX_SP;
                currentSP = CombatConfig.MAX_SP;
                currentBankSP = Mathf.Min(currentBankSP + overflownSP, CombatConfig.MAX_BANK_SP);
            }
            else
            {
                currentSP += spRecovery;
            }

            OnSPChanged?.Invoke(currentSP, currentBankSP, CombatConfig.MAX_SP);
        }

        public void AddOverheat(int amount)
        {
            currentOverheat = Mathf.Min(currentOverheat + amount, CombatConfig.MAX_OVERHEAT);
            OnOverheatChanged?.Invoke(currentOverheat, CombatConfig.MAX_OVERHEAT);

            if (currentOverheat >= CombatConfig.MAX_OVERHEAT)
            {
                Debug.LogWarning($"[시스템] {UnitName} 과열 상태 돌입!");
            }
        }

        public void ReduceOverheat(int amount)
        {
            currentOverheat = Mathf.Max(0, currentOverheat - amount);
            OnOverheatChanged?.Invoke(currentOverheat, CombatConfig.MAX_OVERHEAT);
        }

        public override void TriggerAllEvents()
        {
            base.TriggerAllEvents();
            OnSPChanged?.Invoke(currentSP, currentBankSP, CombatConfig.MAX_SP);
            OnOverheatChanged?.Invoke(currentOverheat, CombatConfig.MAX_OVERHEAT);
        }
    }
}
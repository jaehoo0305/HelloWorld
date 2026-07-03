using System;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전투 씬에 배치된 캐릭터 및 몬스터 오브젝트에 부착되는 실시간 전투 정보 관리 컴포넌트입니다.
    /// ScriptableObject 데이터를 실시간 인스펙터 스탯과 런타임 변수로 가공하고 제어합니다.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        [Header("[ 기반 데이터 에셋 ]")]
        [SerializeField] private CharacterDataSO characterData;

        [Header("[ 실시간 인게임 레벨 설정 ]")]
        [Range(1, 5)][SerializeField] private int currentPassiveLevel = 1;
        [Range(1, 5)][SerializeField] private int currentSkill1Level = 1;
        [Range(1, 5)][SerializeField] private int currentSkill2Level = 1;

        // --- 실시간 런타임 가변 데이터 (속성으로 캡슐화) ---
        public int CurrentHP { get; private set; }
        public int CurrentSP { get; private set; }
        public int CurrentBankSP { get; private set; }
        public int CurrentOverheat { get; private set; }

        // --- UI 및 외부 매니저 연동을 위한 변경 감지 이벤트 액션 ---
        public event Action<int, int> OnHPChanged;             // (현재 체력, 최대 체력)
        public event Action<int, int, int> OnSPChanged;        // (현재 SP, 현재 이월SP, 최대 SP)
        public event Action<int, int> OnOverheatChanged;       // (현재 과열치, 최대 과열치)
        public event Action OnDeath;                           // 사망 시 발생

        // 원본 데이터 참조용 프로퍼티
        public CharacterDataSO CharacterData => characterData;
        public int PassiveLevel => currentPassiveLevel;
        public int Skill1Level => currentSkill1Level;
        public int Skill2Level => currentSkill2Level;

        private void Start()
        {
            InitializeUnit();
        }

        /// <summary>
        /// 데이터 SO를 기반으로 전투 시작 시의 모든 실시간 생명/자원 수치를 초기화합니다.
        /// </summary>
        public void InitializeUnit()
        {
            if (characterData == null)
            {
                Debug.LogError($"[BattleUnit] '{gameObject.name}' 오브젝트에 CharacterDataSO가 등록되어 있지 않습니다!");
                return;
            }

            // 실시간 스탯 초기 세팅
            CurrentHP = characterData.MaxHP;
            CurrentSP = CombatConfig.TURN_START_SP_RECOVERY; // 시작 시 일정 SP 부여
            CurrentBankSP = 0;
            CurrentOverheat = 0;

            // 초기 수치 UI 갱신 유도
            TriggerAllEvents();
        }

        /// <summary>
        /// 캐릭터가 적에게 공격을 받았을 때 피해량을 정교하게 계산하여 체력을 차감합니다.
        /// (다키스트 던전식 방어력 피해 감소 공식 적용)
        /// </summary>
        /// <param name="rawDamage">방어력 적용 전의 기본 피해 수치</param>
        public void TakeDamage(int rawDamage)
        {
            if (CurrentHP <= 0) return;

            // 다키스트 던전식 방어 계산: 방어력(Defense) 수치를 백분율로 환산하되, 설정된 감소 상한선(DefenseCap)을 최대 한계로 잡음
            float reductionPercent = Mathf.Min(characterData.Defense * 0.01f, characterData.DefenseCap);
            int blockedDamage = Mathf.RoundToInt(rawDamage * reductionPercent);

            // 최소 1의 데미지는 무조건 받도록 보정
            int finalDamage = Mathf.Max(1, rawDamage - blockedDamage);

            CurrentHP = Mathf.Max(0, CurrentHP - finalDamage);
            Debug.Log($"[전투] {characterData.CharacterName} 피격! 원래 피해: {rawDamage} -> 방어 차감 후 실제 피해: {finalDamage} (남은 체력: {CurrentHP})");

            OnHPChanged?.Invoke(CurrentHP, characterData.MaxHP);

            if (CurrentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 스킬 시전 등으로 인해 SP를 소모하고, 소모한 SP에 비례하여 과열 수치를 실시간으로 누적합니다.
        /// </summary>
        /// <param name="amount">소모할 SP 수치</param>
        public bool ConsumeSP(int amount)
        {
            // 사용하려는 SP가 부족한지 검사
            int totalAvailableSP = CurrentSP + CurrentBankSP;
            if (totalAvailableSP < amount)
            {
                Debug.LogWarning($"[전투] {characterData.CharacterName}의 SP가 부족하여 스킬을 사용할 수 없습니다.");
                return false;
            }

            // 1. 소모 처리 (이월 은행 SP를 먼저 사용하고 부족분을 기본 SP에서 차감)
            int remainingCost = amount;
            if (CurrentBankSP >= remainingCost)
            {
                CurrentBankSP -= remainingCost;
                remainingCost = 0;
            }
            else
            {
                remainingCost -= CurrentBankSP;
                CurrentBankSP = 0;
                CurrentSP -= remainingCost;
            }

            OnSPChanged?.Invoke(CurrentSP, CurrentBankSP, CombatConfig.MAX_SP);

            // 2. 사용한 SP 비례 과열 게이지 누적 규칙 연동 (사용한 SP 하나 당 2 상승)
            int overheatGain = amount * CombatConfig.OVERHEAT_PER_SP;
            AddOverheat(overheatGain);

            return true;
        }

        /// <summary>
        /// 아군의 턴이 시작될 때 SP를 회복해 주며, 이월 가능한 최대 저장소를 넘어서면 이월 SP(Bank)로 전환합니다.
        /// </summary>
        public void RecoverSPOnTurnStart()
        {
            int spRecovery = CombatConfig.TURN_START_SP_RECOVERY;

            // 기존 SP와 회복 SP의 합이 최대 한계치를 돌파하는 경우
            if (CurrentSP + spRecovery > CombatConfig.MAX_SP)
            {
                int overflownSP = (CurrentSP + spRecovery) - CombatConfig.MAX_SP;
                CurrentSP = CombatConfig.MAX_SP;

                // 넘치는 양은 은행 저장소(Bank)로 이월하되 최대 이월량(MAX_BANK_SP) 한도를 넘을 수 없음
                CurrentBankSP = Mathf.Min(CurrentBankSP + overflownSP, CombatConfig.MAX_BANK_SP);
            }
            else
            {
                CurrentSP += spRecovery;
            }

            OnSPChanged?.Invoke(CurrentSP, CurrentBankSP, CombatConfig.MAX_SP);
        }

        /// <summary>
        /// 과열 게이지를 축적시키며, 최대 과열치에 도달하는 순간에 대한 리미터 제어를 담당합니다.
        /// </summary>
        public void AddOverheat(int amount)
        {
            CurrentOverheat = Mathf.Min(CurrentOverheat + amount, CombatConfig.MAX_OVERHEAT);
            OnOverheatChanged?.Invoke(CurrentOverheat, CombatConfig.MAX_OVERHEAT);

            if (CurrentOverheat >= CombatConfig.MAX_OVERHEAT)
            {
                Debug.LogWarning($"[시스템] {characterData.CharacterName} 과열 상태 돌입! (과열 게이지: 100%)");
                // 추후 이곳에서 과열 패널티 디버프나 피해 로직을 활성화할 수 있습니다.
            }
        }

        /// <summary>
        /// 힐러 등의 스킬 효과로 인해 과열도를 저하시킬 때 사용합니다.
        /// </summary>
        public void ReduceOverheat(int amount)
        {
            CurrentOverheat = Mathf.Max(0, CurrentOverheat - amount);
            OnOverheatChanged?.Invoke(CurrentOverheat, CombatConfig.MAX_OVERHEAT);
        }

        /// <summary>
        /// 유닛의 체력이 0에 도달하여 전장에서 사망 상태가 될 때 실행됩니다.
        /// </summary>
        private void Die()
        {
            Debug.Log($"[사망] {characterData.CharacterName}가 전장에서 사망했습니다.");
            OnDeath?.Invoke();
            // 사망 시 전투 타일에서 오브젝트 제거 연동 코드 작성 가능
        }

        /// <summary>
        /// UI나 외부 매니저가 중간에 바인딩했을 때 현재 상태를 일제히 동기화해 주기 위한 헬퍼입니다.
        /// </summary>
        public void TriggerAllEvents()
        {
            if (characterData == null) return;
            OnHPChanged?.Invoke(CurrentHP, characterData.MaxHP);
            OnSPChanged?.Invoke(CurrentSP, CurrentBankSP, CombatConfig.MAX_SP);
            OnOverheatChanged?.Invoke(CurrentOverheat, CombatConfig.MAX_OVERHEAT);
        }
    }
}
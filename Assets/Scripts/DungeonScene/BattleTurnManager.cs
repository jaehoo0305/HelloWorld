using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 단일 턴 슬롯을 나타내는 구조체입니다.
    /// 보스의 다중 행동 분할 처리를 위해 유닛과 세부 행동 번호를 함께 관리합니다.
    /// </summary>
    public struct TurnSlot
    {
        public BattleUnit Unit;
        public int ActionIndex; // 보스의 경우 0, 1, 2... 형태로 몇 번째 행동인지 구분

        public TurnSlot(BattleUnit unit, int actionIndex)
        {
            Unit = unit;
            ActionIndex = actionIndex;
        }
    }

    /// <summary>
    /// 전투의 라운드 진행, 속도(Speed) 기반 턴 정렬, 보스 다중 행동 및 딜러 추가 턴을 제어하는 핵심 매니저입니다.
    /// </summary>
    public class BattleTurnManager : MonoBehaviour
    {
        [Header("[ 전투 참가 유닛 목록 ]")]
        [SerializeField] private List<BattleUnit> allUnits = new List<BattleUnit>();

        // --- 실시간 전투 상태 변수 ---
        public int CurrentRound { get; private set; } = 0;
        public BattleUnit CurrentTurnUnit { get; private set; }

        // 현재 라운드에서 행동 대기 중인 턴 큐
        private List<TurnSlot> turnQueue = new List<TurnSlot>();
        private int currentQueueIndex = -1;

        // --- UI에서 실시간 턴 대기열을 읽어갈 수 있도록 Getter 속성 제공 ---
        public IReadOnlyList<TurnSlot> TurnQueue => turnQueue;
        public int CurrentQueueIndex => currentQueueIndex;

        // --- 외부 연동용 전투 진행 이벤트 ---
        public event Action<int> OnRoundStarted;              // 라운드 시작 이벤트 (현재 라운드 번호)
        public event Action<BattleUnit> OnTurnStarted;        // 특정 유닛의 턴 시작 이벤트
        public event Action<BattleUnit> OnTurnEnded;          // 특정 유닛의 턴 종료 이벤트
        public event Action OnBattleEnded;                     // 전투 종료 이벤트

        /// <summary>
        /// 새로운 전투를 시작하며 유닛 목록을 초기화하고 1라운드를 트리거합니다.
        /// </summary>
        public void StartBattle(List<BattleUnit> units)
        {
            allUnits = new List<BattleUnit>(units);
            CurrentRound = 0;

            Debug.Log("[전투 시작] 전장에 모든 유닛이 배치되었습니다. 전투를 개시합니다!");

            // 모든 유닛의 사망 이벤트를 구독하여 전투 종료 판정을 실시간으로 진행
            foreach (var unit in allUnits)
            {
                unit.OnDeath += () => CheckBattleEndCondition();
            }

            StartNewRound();
        }

        /// <summary>
        /// 새로운 라운드를 개시합니다. 속도 스탯 기반으로 행동 큐를 재정렬합니다.
        /// </summary>
        public void StartNewRound()
        {
            CurrentRound++;
            turnQueue.Clear();
            currentQueueIndex = -1;

            Debug.Log($"\n================== [ ROUND {CurrentRound} 시작 ] ==================");

            // 1. 살아있는 모든 유닛을 속도(Speed) 내림차순으로 정렬
            var livingUnits = allUnits
                .Where(u => u.CurrentHP > 0)
                .OrderByDescending(u => u.CharacterData.Speed)
                .ToList();

            // 2. 턴 큐 구축
            // 일반 유닛은 1번, 보스 유닛은 설정된 행동 횟수(actionCountPerRound)만큼 턴 슬롯을 추가 생성
            foreach (var unit in livingUnits)
            {
                int actionCount = unit.CharacterData.ActionCountPerRound;
                for (int i = 0; i < actionCount; i++)
                {
                    turnQueue.Add(new TurnSlot(unit, i));
                }
            }

            OnRoundStarted?.Invoke(CurrentRound);

            // 라운드 시작 직후 첫 번째 턴 실행
            MoveToNextTurn();
        }

        /// <summary>
        /// 큐에서 다음 순서의 유닛을 꺼내어 턴을 넘겨줍니다.
        /// </summary>
        public void MoveToNextTurn()
        {
            // 모든 유닛이 행동을 마쳤다면 다음 라운드로 전환
            currentQueueIndex++;
            if (currentQueueIndex >= turnQueue.Count)
            {
                StartNewRound();
                return;
            }

            TurnSlot nextSlot = turnQueue[currentQueueIndex];

            // 대기 시간 도중 해당 유닛이 사망한 경우 스킵하고 다음 턴으로 전환
            if (nextSlot.Unit == null || nextSlot.Unit.CurrentHP <= 0)
            {
                MoveToNextTurn();
                return;
            }

            CurrentTurnUnit = nextSlot.Unit;
            Debug.Log($"[턴 시작] {CurrentTurnUnit.CharacterData.CharacterName}의 턴 (행동 번호: {nextSlot.ActionIndex + 1})");

            // 턴이 시작될 때 아군의 기력(SP)을 기본적으로 회복하고 한도를 초과하면 은행(Bank)으로 이월
            CurrentTurnUnit.RecoverSPOnTurnStart();

            OnTurnStarted?.Invoke(CurrentTurnUnit);
        }

        /// <summary>
        /// 현재 유닛의 행동이 완전히 끝났음을 알리고 턴 종료 연산을 수행합니다.
        /// </summary>
        public void EndCurrentTurn()
        {
            if (CurrentTurnUnit == null) return;

            Debug.Log($"[턴 종료] {CurrentTurnUnit.CharacterData.CharacterName}의 턴이 종료되었습니다.");

            OnTurnEnded?.Invoke(CurrentTurnUnit);
            CurrentTurnUnit = null;

            // 잠시 대기 후 혹은 즉시 다음 턴으로 진행
            MoveToNextTurn();
        }

        /// <summary>
        /// [딜러 전용 룰] 킬 달성 등의 조건 만족 시, 현재 진행 중인 큐 바로 다음에 추가 턴 슬롯을 강제로 끼워 넣습니다.
        /// </summary>
        public void GrantExtraTurn(BattleUnit unit)
        {
            if (unit == null || unit.CurrentHP <= 0) return;

            Debug.Log($"[추가 행동 활성화] 딜러 {unit.CharacterData.CharacterName}가 특수 조건으로 즉시 추가 턴을 얻었습니다!");

            // 현재 진행 중인 인덱스 바로 뒤에 새로운 턴 슬롯을 즉시 주입 (새치기 규칙)
            TurnSlot extraSlot = new TurnSlot(unit, 99); // 99는 보너스 추가 턴 식별용 임의 인덱스
            turnQueue.Insert(currentQueueIndex + 1, extraSlot);
        }

        /// <summary>
        /// 아군 전멸 혹은 적 전멸 상태를 판단하여 전투 종료를 트리거합니다.
        /// </summary>
        private void CheckBattleEndCondition()
        {
            // PositionType이 Boss이거나 적 진영 시스템 구분이 구현되었을 때 피아 구분을 명확히 고도화할 수 있습니다.
            // 여기서는 단순 데모 작동을 위해 전장의 유닛 중 한쪽 세력이 모두 죽었는지 검사합니다.
            bool isBossOrEnemyDead = allUnits
                .Where(u => u.CharacterData.PositionType == PositionType.Boss)
                .All(u => u.CurrentHP <= 0);

            bool isPlayerPartyDead = allUnits
                .Where(u => u.CharacterData.PositionType != PositionType.Boss)
                .All(u => u.CurrentHP <= 0);

            if (isBossOrEnemyDead)
            {
                Debug.Log("================== [전투 승리! 적이 모두 무력화되었습니다.] ==================");
                OnBattleEnded?.Invoke();
            }
            else if (isPlayerPartyDead)
            {
                Debug.Log("================== [전투 패배... 아군 파티가 전멸했습니다.] ==================");
                OnBattleEnded?.Invoke();
            }
        }
    }
}
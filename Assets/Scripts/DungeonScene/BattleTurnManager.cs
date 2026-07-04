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

        public int CurrentRound { get; private set; } = 0;
        public BattleUnit CurrentTurnUnit { get; private set; }

        private List<TurnSlot> turnQueue = new List<TurnSlot>();
        private int currentQueueIndex = -1;

        public IReadOnlyList<TurnSlot> TurnQueue => turnQueue;
        public int CurrentQueueIndex => currentQueueIndex;

        public event Action<int> OnRoundStarted;
        public event Action<BattleUnit> OnTurnStarted;
        public event Action<BattleUnit> OnTurnEnded;
        public event Action OnBattleEnded;

        public void StartBattle(List<BattleUnit> units)
        {
            allUnits = new List<BattleUnit>(units);
            CurrentRound = 0;

            Debug.Log("[전투 시작] 전장에 모든 유닛이 배치되었습니다. 전투를 개시합니다!");

            foreach (var unit in allUnits)
            {
                unit.OnDeath += () => CheckBattleEndCondition();
            }

            StartNewRound();
        }

        public void StartNewRound()
        {
            CurrentRound++;
            turnQueue.Clear();
            currentQueueIndex = -1;

            Debug.Log($"\n================== [ ROUND {CurrentRound} 시작 ] ==================");

            // 1. 추상화 프로퍼티인 Speed를 통해 아군과 적 유닛을 한 번에 속도순 내림차순 정렬
            var livingUnits = allUnits
                .Where(u => u.CurrentHP > 0)
                .OrderByDescending(u => u.Speed)
                .ToList();

            // 2. 턴 큐 구축
            foreach (var unit in livingUnits)
            {
                int actionCount = unit.ActionCountPerRound;
                for (int i = 0; i < actionCount; i++)
                {
                    turnQueue.Add(new TurnSlot(unit, i));
                }
            }

            OnRoundStarted?.Invoke(CurrentRound);
            MoveToNextTurn();
        }

        public void MoveToNextTurn()
        {
            currentQueueIndex++;
            if (currentQueueIndex >= turnQueue.Count)
            {
                StartNewRound();
                return;
            }

            TurnSlot nextSlot = turnQueue[currentQueueIndex];

            if (nextSlot.Unit == null || nextSlot.Unit.CurrentHP <= 0)
            {
                MoveToNextTurn();
                return;
            }

            CurrentTurnUnit = nextSlot.Unit;
            Debug.Log($"[턴 시작] {CurrentTurnUnit.UnitName}의 턴 (행동 번호: {nextSlot.ActionIndex + 1})");

            // 턴 개시 시 기력 회복(아군인 경우에만 PlayerUnit 가상 함수 재정의 로직 작동)
            CurrentTurnUnit.RecoverSPOnTurnStart();

            OnTurnStarted?.Invoke(CurrentTurnUnit);
        }

        public void EndCurrentTurn()
        {
            if (CurrentTurnUnit == null) return;

            Debug.Log($"[턴 종료] {CurrentTurnUnit.UnitName}의 턴이 종료되었습니다.");

            OnTurnEnded?.Invoke(CurrentTurnUnit);
            CurrentTurnUnit = null;

            MoveToNextTurn();
        }

        public void GrantExtraTurn(BattleUnit unit)
        {
            if (unit == null || unit.CurrentHP <= 0) return;

            Debug.Log($"[추가 행동 활성화] 딜러 {unit.UnitName}가 특수 조건으로 즉시 추가 턴을 얻었습니다!");

            TurnSlot extraSlot = new TurnSlot(unit, 99);
            turnQueue.Insert(currentQueueIndex + 1, extraSlot);
        }

        private void CheckBattleEndCondition()
        {
            // 추상화된 IsBoss 값 및 헬퍼 구분을 통한 전멸 조건 확인
            bool isBossOrEnemyDead = allUnits
                .Where(u => u.IsBoss)
                .All(u => u.CurrentHP <= 0);

            bool isPlayerPartyDead = allUnits
                .Where(u => !u.IsBoss)
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
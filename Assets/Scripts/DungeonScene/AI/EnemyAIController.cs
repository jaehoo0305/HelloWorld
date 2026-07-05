using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 적의 턴일 때 해당 적의 AI 성향(EnemyAIType)을 분석하여,
    /// 알맞은 AI 행동 전략(Strategy)을 동적으로 실행하고 제어하는 실시간 AI 컨트롤러입니다.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        [Header("[ 핵심 매니저 연결 ]")]
        [SerializeField] private BattleGridManager gridManager;
        [SerializeField] private BattleTurnManager turnManager;

        private bool isExecutingAITurn = false;

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<BattleGridManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();

            if (turnManager != null)
            {
                turnManager.OnTurnStarted += OnTurnStartedHandler;
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= OnTurnStartedHandler;
            }
        }

        private void OnTurnStartedHandler(BattleUnit activeUnit)
        {
            // 현재 턴을 가진 유닛이 적군(EnemyUnit)이라면 AI 전략 기동을 가동합니다.
            if (activeUnit is EnemyUnit enemyUnit)
            {
                if (!isExecutingAITurn)
                {
                    StartCoroutine(CoExecuteEnemyAITurn(enemyUnit));
                }
            }
        }

        /// <summary>
        /// 적의 AI 성향에 따라 알맞은 IEnemyAIBehavior 전략 구현체를 동적으로 바인딩해 동작을 실행합니다.
        /// </summary>
        private IEnumerator CoExecuteEnemyAITurn(EnemyUnit enemy)
        {
            isExecutingAITurn = true;
            enemy.ResetTurnState();

            // 적군 SO 데이터에 기록된 AI 성향 감지
            EnemyAIType aiType = enemy.EnemyData.AIType;
            IEnemyAIBehavior behavior = null;

            // 전략 패턴 맵핑 제어
            switch (aiType)
            {
                case EnemyAIType.General:
                    behavior = GetOrCreateGeneralBehavior();
                    break;

                case EnemyAIType.Defensive:
                    // 추후 DefensiveAIBehavior 구현 시 확장하여 교체할 수 있는 구조적 쉘터
                    Debug.LogWarning($"[AI] {aiType} 성향이 지정되었으나 DefensiveAIBehavior 스크립트가 없으므로 일반 공격형(General)으로 자동 보정합니다.");
                    behavior = GetOrCreateGeneralBehavior();
                    break;

                case EnemyAIType.Evasive:
                    // 추후 EvasiveAIBehavior 구현 시 확장하여 교체할 수 있는 구조적 쉘터
                    Debug.LogWarning($"[AI] {aiType} 성향이 지정되었으나 EvasiveAIBehavior 스크립트가 없으므로 일반 공격형(General)으로 자동 보정합니다.");
                    behavior = GetOrCreateGeneralBehavior();
                    break;
            }

            if (behavior != null)
            {
                // 다형성을 활용한 실시간 연산 전략 실행 개시
                yield return StartCoroutine(behavior.ExecuteBehavior(enemy, gridManager, turnManager));
            }
            else
            {
                Debug.LogError($"[AI] 적 {enemy.UnitName}의 AI 구동 전략 컴포넌트가 세팅되지 않았습니다. 즉시 턴을 마칩니다.");
                EndAITurn();
            }

            isExecutingAITurn = false;
        }

        private IEnemyAIBehavior GetOrCreateGeneralBehavior()
        {
            var general = GetComponent<GeneralAIBehavior>();
            if (general == null)
            {
                general = gameObject.AddComponent<GeneralAIBehavior>();
            }
            return general;
        }

        private void EndAITurn()
        {
            isExecutingAITurn = false;
            if (turnManager != null)
            {
                turnManager.EndCurrentTurn();
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 공격 중심 성향(General)의 적 AI 행동 로직을 구현한 구체적인 전략 클래스입니다.
    /// 타겟팅 우선순위에 근거해 아군을 선정하고, A* 최적 경로를 추적하여 다가가 타격합니다.
    /// </summary>
    public class GeneralAIBehavior : MonoBehaviour, IEnemyAIBehavior
    {
        [Header("[ 연출용 딜레이 설정 ]")]
        [Tooltip("행동 결정을 마치고 움직이기 시작할 때까지 뜸들이는 대기 시간입니다.")]
        [SerializeField] private float decisionThinkDelay = 0.8f;
        [Tooltip("공격을 완료하고 다음 턴으로 소유권을 완전히 양도하기 전까지의 대기 시간입니다.")]
        [SerializeField] private float attackEndDelay = 1.0f;

        public IEnumerator ExecuteBehavior(EnemyUnit enemy, BattleGridManager gridManager, BattleTurnManager turnManager)
        {
            // 1. 행동 개시 전 잠시 연출 대기
            yield return new WaitForSeconds(decisionThinkDelay);

            // 2. 타겟 우선순위에 따른 아군 표적 선정
            PlayerUnit target = SelectBestTarget(enemy, gridManager);
            if (target == null)
            {
                Debug.LogWarning($"[AI-General] 적 {enemy.UnitName}의 차례이나, 공격할 수 있는 살아있는 아군이 없습니다.");
                turnManager.EndCurrentTurn();
                yield break;
            }

            // 3. 사용 가능한 기술 및 사거리 선정
            SkillDataSO chosenSkill = SelectBestSkill(enemy);
            int skillRange = 1;
            if (chosenSkill != null)
            {
                SkillLevelData lvData = chosenSkill.GetLevelData(1);
                skillRange = lvData != null ? lvData.Range : 1;
            }

            Vector2Int enemyGridPos = gridManager.GetUnitCoordinate(enemy);
            Vector2Int targetGridPos = gridManager.GetUnitCoordinate(target);

            Debug.Log($"[AI-General] {enemy.UnitName}의 선정 타겟: {target.UnitName} | 결정 기술: {chosenSkill?.SkillName ?? "기본 타격"} (사거리: {skillRange})");

            // 4. 사거리를 고려한 최적의 이동 목표 좌표 계산
            Vector2Int bestMovePos = CalculateOptimalPosition(enemyGridPos, targetGridPos, skillRange, enemy.EnemyData.MaxMoveDistance, enemy, gridManager);

            // 5. A* 최적 경로 탐색
            List<Vector2Int> astarPath = gridManager.FindPath(enemyGridPos, bestMovePos, enemy);

            if (astarPath != null && astarPath.Count > 0)
            {
                // 이번 차례에 허용된 최대 이동량만큼의 스텝 수 계산 (예: 3칸)
                int allowedSteps = Mathf.Min(astarPath.Count, enemy.EnemyData.MaxMoveDistance);

                for (int i = 0; i < allowedSteps; i++)
                {
                    Vector2Int nextCoord = astarPath[i];
                    bool moveResult = gridManager.TryMoveUnitOneStep(enemy, nextCoord);

                    if (moveResult)
                    {
                        // Lerp 애니메이션 완료 시간 대기
                        float animationTime = 1f / gridManager.MoveSpeed;
                        yield return new WaitForSeconds(animationTime + 0.05f);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // ★ 중요: 지정된 이번 턴의 최대 보폭(3칸) 이동 시퀀스가 완벽히 종결된 시점에 락을 걸어 중복 연사를 방지합니다.
            enemy.HasMovedThisTurn = true;

            // 6. 최종 위치 기준 사거리 검사 및 공격 프로세스 진행
            enemyGridPos = gridManager.GetUnitCoordinate(enemy);
            int finalDistance = Mathf.Abs(targetGridPos.x - enemyGridPos.x) + Mathf.Abs(targetGridPos.y - enemyGridPos.y);

            if (finalDistance <= skillRange)
            {
                enemy.HasAttackedThisTurn = true;

                int rawDmg = enemy.EnemyData.Attack;
                if (chosenSkill != null)
                {
                    SkillLevelData levelData = chosenSkill.GetLevelData(1);
                    float modifier = levelData != null ? levelData.DamageModifier : 1.0f;
                    rawDmg = Mathf.RoundToInt(rawDmg * modifier);
                }

                Debug.Log($"[AI 공격-General] {enemy.UnitName}가 {target.UnitName}에게 '{chosenSkill?.SkillName ?? "기본 타격"}' 시전! (데미지: {rawDmg})");
                target.TakeDamage(rawDmg);
            }
            else
            {
                Debug.Log($"[AI-General] 공격 범위 미달: 타겟 {target.UnitName}이 사거리 밖입니다. (거리: {finalDistance} / 요구 사거리: {skillRange})");
            }

            // 7. 공격 연출 대기 후 턴 종료
            yield return new WaitForSeconds(attackEndDelay);
            turnManager.EndCurrentTurn();
        }

        private PlayerUnit SelectBestTarget(EnemyUnit enemy, BattleGridManager gridManager)
        {
            PlayerUnit[] livingPlayers = FindObjectsByType<PlayerUnit>(FindObjectsSortMode.None)
                .Where(p => p.CurrentHP > 0)
                .ToArray();

            if (livingPlayers == null || livingPlayers.Length == 0) return null;

            Vector2Int enemyCoord = gridManager.GetUnitCoordinate(enemy);
            TargetPriorityType priority = enemy.EnemyData.TargetPriority;

            switch (priority)
            {
                case TargetPriorityType.LowestCurrentHP:
                    return livingPlayers.OrderBy(p => p.CurrentHP).First();

                case TargetPriorityType.HighestMaxHP:
                    return livingPlayers.OrderByDescending(p => p.MaxHP).First();

                case TargetPriorityType.Nearest:
                    return livingPlayers.OrderBy(p =>
                        Mathf.Abs(gridManager.GetUnitCoordinate(p).x - enemyCoord.x) +
                        Mathf.Abs(gridManager.GetUnitCoordinate(p).y - enemyCoord.y)
                    ).First();

                case TargetPriorityType.Farthest:
                    return livingPlayers.OrderByDescending(p =>
                        Mathf.Abs(gridManager.GetUnitCoordinate(p).x - enemyCoord.x) +
                        Mathf.Abs(gridManager.GetUnitCoordinate(p).y - enemyCoord.y)
                    ).First();

                case TargetPriorityType.Random:
                default:
                    int rnd = Random.Range(0, livingPlayers.Length);
                    return livingPlayers[rnd];
            }
        }

        private SkillDataSO SelectBestSkill(EnemyUnit enemy)
        {
            if (enemy.EnemyData.UsableSkills == null || enemy.EnemyData.UsableSkills.Count == 0) return null;
            return enemy.EnemyData.UsableSkills[0];
        }

        private Vector2Int CalculateOptimalPosition(Vector2Int enemyPos, Vector2Int targetPos, int skillRange, int maxMove, EnemyUnit self, BattleGridManager gridManager)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int dx = -skillRange; dx <= skillRange; dx++)
            {
                for (int dy = -skillRange; dy <= skillRange; dy++)
                {
                    int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (manhattanDist != skillRange) continue;

                    Vector2Int potentialPos = targetPos + new Vector2Int(dx, dy);

                    if (gridManager.IsTileWalkableAndFree(potentialPos) || potentialPos == enemyPos)
                    {
                        candidates.Add(potentialPos);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                // ★ 중요: 초장거리 타겟 추적을 위해 .Where(Count <= maxMove) 제약 필터를 완벽 분리했습니다.
                // 이제 10칸 밖에 있어도 타겟 주위의 공격 포인트 지점을 향해 3칸씩 저돌적으로 전진합니다.
                var validPaths = candidates
                    .Select(c => new { Pos = c, Path = gridManager.FindPath(enemyPos, c, self) })
                    .Where(item => item.Path != null)
                    .OrderBy(item => item.Path.Count)
                    .ToList();

                if (validPaths.Count > 0)
                {
                    return validPaths.First().Pos;
                }
            }

            return targetPos;
        }
    }
}
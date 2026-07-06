using DungeonCombat.Combat;
using DungeonCombat.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 아이사 고유 일반 스킬: '시공간 왜곡'의 순간이동 및 추가 턴 로직을 전담하는 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_SpaceTimeDistortion", menuName = "Dungeon/Skills/Space Time Distortion", order = 3)]
    public class SpaceTimeDistortionSkillSO : Data.SkillDataSO
    {
        public override void Execute(PlayerUnit caster, Vector2Int targetCoord, int level, Action onComplete)
        {
            BattleGridManager gridManager = FindFirstObjectByType<BattleGridManager>();
            if (gridManager == null || BattleFieldEffectManager.Instance == null)
            {
                Debug.LogError("[SpaceTimeDistortion] Missing core manager instances.");
                onComplete?.Invoke();
                return;
            }

            // 지정한 타일에 중력장 장판이 활성화되어 있는지 물리 검증
            if (!BattleFieldEffectManager.Instance.HasEffectAt(targetCoord, "GravityField"))
            {
                Debug.LogWarning("[SpaceTimeDistortion] Target tile does not contain a GravityField.");
                onComplete?.Invoke();
                return;
            }

            Vector2Int currentCoord = gridManager.GetUnitCoordinate(caster);

            // 그리드 매니저 내부 데이터베이스 점유 정보 리플렉션 스왑 보정
            UpdateGridPositionReflection(gridManager, caster, currentCoord, targetCoord);

            // 트랜스폼 위치 즉시 순간이동 동기화
            caster.transform.position = gridManager.GetWorldPosition(targetCoord);

            Debug.Log($"[SpaceTimeDistortion] {caster.UnitName} teleported from {currentCoord} to {targetCoord}.");
            onComplete?.Invoke();
        }

        private void UpdateGridPositionReflection(BattleGridManager gridManager, BattleUnit unit, Vector2Int from, Vector2Int to)
        {
            var occupiedUnitsField = typeof(BattleGridManager).GetField("occupiedUnits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var unitPositionsField = typeof(BattleGridManager).GetField("unitPositions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (occupiedUnitsField != null && unitPositionsField != null)
            {
                var occupiedUnits = (Dictionary<Vector2Int, BattleUnit>)occupiedUnitsField.GetValue(gridManager);
                var unitPositions = (Dictionary<BattleUnit, Vector2Int>)unitPositionsField.GetValue(gridManager);

                occupiedUnits.Remove(from);
                occupiedUnits[to] = unit;
                unitPositions[unit] = to;
            }
        }
    }
}
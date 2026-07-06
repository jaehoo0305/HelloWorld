using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 아이사 고유 스킬 1 강화판 (궁극 능력): '질량 축퇴'를 발동시키는 실시간 연출 및 계산 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_MassCollapse", menuName = "Dungeon/Skills/Mass Collapse", order = 2)]
    public class MassCollapseSkillSO : SkillDataSO
    {
        [Header("[ 질량 축퇴 전용 비주얼 에셋 ]")]
        [Tooltip("궁극기 시전 시 아군 발밑 혹은 전신에 깔릴 충전 연출 프리팹입니다.")]
        [SerializeField] private GameObject castChargePrefab;
        [Tooltip("끌어당김이 끝나고 중심에서 터질 초대형 질량 붕괴 폭발 프리팹입니다.")]
        [SerializeField] private GameObject implosionPrefab;
        [Tooltip("끌려오는 유닛들에게 부착할 먼지/중력선 자취 연출 프리팹입니다.")]
        [SerializeField] private GameObject pullTrailPrefab;
        [Tooltip("3턴 동안 맵 중심에 남아 피해와 둔화를 줄 지속 필드 프리팹입니다. (MassCollapseFieldEffect 컴포넌트 부착 필수)")]
        [SerializeField] private GameObject massCollapseFieldPrefab;
        [Tooltip("디버프 적용 시 적 발밑에 깔아줄 둔화 오라 프리팹입니다.")]
        [SerializeField] private GameObject slowOuraPrefab;

        [Header("[ 연출 수치 설정 ]")]
        [SerializeField] private float pullSlideDuration = 0.4f; // 끌려오는 슬라이딩 시간
        [SerializeField] private float explosionDelay = 0.2f;    // 폭발 전 대기 시간

        /// <summary>
        /// 궁극 능력: 중력장 하나를 지정하고 나머지 중력장을 모두 희생한다. 
        /// 3턴 동안 지정한 중력장을 중심 기준으로 3x3 크기 범위로 모두를 끌어당기며 최초 피해를 준다.
        /// </summary>
        public override void Execute(PlayerUnit caster, Vector2Int targetCoord, int level, Action onComplete)
        {
            BattleGridManager gridManager = FindFirstObjectByType<BattleGridManager>();
            if (gridManager == null || BattleFieldEffectManager.Instance == null)
            {
                Debug.LogError("[질량 축퇴] 필수 매니저 클래스가 누락되어 시전을 취소합니다.");
                onComplete?.Invoke();
                return;
            }

            // 시전자의 코루틴 시스템을 빌려 타임라인 연출 시퀀스 가동
            caster.StartCoroutine(CoExecuteSequence(caster, targetCoord, level, gridManager, onComplete));
        }

        private IEnumerator CoExecuteSequence(PlayerUnit caster, Vector2Int center, int level, BattleGridManager gridManager, Action onComplete)
        {
            // 0. 타겟 위치에 중력장이 깔려있는지 최종 물리 검증
            if (!BattleFieldEffectManager.Instance.HasEffectAt(center, "GravityField"))
            {
                Debug.LogWarning("[질량 축퇴] 시전 대상 타일에 활성화된 중력장이 존재하지 않습니다! 시전이 무산됩니다.");
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log($"<color=#FF3399><b>[궁극기 발동] {caster.UnitName} -> ★ 질 량 축 퇴 ★</b></color>");

            // 1. 시전 충전 연출 진행
            if (castChargePrefab != null)
            {
                GameObject chargeObj = Instantiate(castChargePrefab, caster.transform.position, Quaternion.identity);
                Destroy(chargeObj, 2f);
            }
            yield return new WaitForSeconds(0.6f);

            // 2. 다른 모든 중력장 희생 연산 진행
            List<TileFieldEffect> allMyFields = BattleFieldEffectManager.Instance.GetEffectsByOwner(caster, "GravityField");
            TileFieldEffect targetCenterField = allMyFields.FirstOrDefault(f => f.Coordinate == center);

            int sacrificeCount = 0;
            foreach (var field in allMyFields)
            {
                if (field != targetCenterField)
                {
                    BattleFieldEffectManager.Instance.RemoveEffectAt(field.Coordinate, field);
                    sacrificeCount++;
                }
            }
            Debug.Log($"[질량 축퇴] 시전 중심지를 제외한 총 {sacrificeCount}개의 중력장을 질량 축퇴 에너지로 흡수했습니다!");

            // 3. 3x3 범위 내 유닛 수집 및 "속도 반비례(오름차순)" 정렬
            List<BattleUnit> targetUnits = new List<BattleUnit>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector2Int neighbor = center + new Vector2Int(x, z);
                    BattleUnit unit = gridManager.GetUnitAt(neighbor);

                    // 피아구분 없이 타겟팅하되, 이미 중심에 있는 유닛이나 죽은 유닛은 이동 처리 제외
                    if (unit != null && unit.CurrentHP > 0 && neighbor != center)
                    {
                        targetUnits.Add(unit);
                    }
                }
            }

            // 속도가 낮을수록(속도에 반비례) 인덱스 앞으로 배치하여 끌어당김 연산 최우선 순위권 부여
            targetUnits = targetUnits.OrderBy(u => u.Speed).ToList();

            // 4. 순차적 끌어당김 연출 및 가상 그리드 데이터베이스 실시간 스왑
            List<Coroutine> activeMoveCoroutines = new List<Coroutine>();
            foreach (var unit in targetUnits)
            {
                Vector2Int currentCoord = gridManager.GetUnitCoordinate(unit);
                Vector2Int diff = center - currentCoord;

                // 중심 방향으로 직진 1칸 좌표 산출
                Vector2Int stepDir = new Vector2Int(Mathf.Clamp(diff.x, -1, 1), Mathf.Clamp(diff.y, -1, 1));
                Vector2Int targetCoord = currentCoord + stepDir;

                // 목표 타일이 정상 지형이며 빈 격자인 경우에만 안전하게 스왑 진행
                if (gridManager.IsWalkable(targetCoord) && gridManager.GetUnitAt(targetCoord) == null)
                {
                    // 리플렉션을 통해 그리드 매니저의 private 점유 정보 테이블 가공
                    UpdateGridPositionReflection(gridManager, unit, currentCoord, targetCoord);

                    // 화면 슬라이딩 비행 연출 가동
                    activeMoveCoroutines.Add(caster.StartCoroutine(CoAnimateUnitPull(unit, gridManager.GetWorldPosition(targetCoord))));
                }
            }

            // 모든 이동 연출이 끝나길 대기
            foreach (var co in activeMoveCoroutines)
            {
                yield return co;
            }
            yield return new WaitForSeconds(explosionDelay);

            // 5. 폭발 이펙트 소환 및 최초 광역 대미지 폭파 처리
            if (implosionPrefab != null)
            {
                Vector3 centerWorldPos = gridManager.GetWorldPosition(center) + Vector3.up * 0.5f;
                GameObject implosionObj = Instantiate(implosionPrefab, centerWorldPos, Quaternion.identity);
                Destroy(implosionObj, 3f);
            }

            // 최종 데미지 공식 대입: 최초 피해(Dmg 250% + 희생 중력장 당 10%)
            SkillLevelData lvlData = GetLevelData(level);
            float baseDmgMod = (lvlData != null) ? lvlData.DamageModifier : 2.5f; // 기본 {dmg:250} -> 2.5배
            float finalDmgMultiplier = baseDmgMod + (sacrificeCount * 0.10f);

            int baseAttack = caster.CharacterData != null ? caster.CharacterData.Attack : 35;
            int finalExplosionDamage = Mathf.RoundToInt(baseAttack * finalDmgMultiplier);

            // 3x3 폭발 충격파 적용 대상 유닛 재검출 (끌려와서 뭉친 적들 대거 타격)
            List<BattleUnit> explosionTargets = new List<BattleUnit>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector2Int target = center + new Vector2Int(x, z);
                    BattleUnit unit = gridManager.GetUnitAt(target);
                    if (unit != null && unit.CurrentHP > 0)
                    {
                        explosionTargets.Add(unit);
                    }
                }
            }

            foreach (var target in explosionTargets)
            {
                Debug.Log($"[질량 축퇴 타격] {caster.UnitName} -> {target.UnitName}에게 {finalExplosionDamage} (배율: {finalDmgMultiplier * 100}%)의 강력한 축퇴 피해를 입혔습니다.");
                target.TakeDamage(finalExplosionDamage);
            }

            // 6. 3턴 유지되는 지속 필드 효과(MassCollapseField) 배치
            if (massCollapseFieldPrefab != null)
            {
                // 기존 중심 타일의 1회성 GravityField를 걷어내고, 3턴 만료형의 상위 필드로 리플레이스 교체
                if (targetCenterField != null)
                {
                    BattleFieldEffectManager.Instance.RemoveEffectAt(center, targetCenterField);
                }

                BattleFieldEffectManager.Instance.SpawnFieldEffect(
                    massCollapseFieldPrefab,
                    center,
                    "MassCollapseField",
                    caster,
                    3
                );

                // 생성된 지속 필드 스크립트에 파라미터 전달
                TileFieldEffect spawnedEffect = BattleFieldEffectManager.Instance.GetEffectsByOwner(caster, "MassCollapseField")
                    .FirstOrDefault(e => e.Coordinate == center);

                if (spawnedEffect is MassCollapseFieldEffect collapseField)
                {
                    collapseField.SetupFieldParameters(sacrificeCount, baseAttack, slowOuraPrefab);
                }
            }

            yield return new WaitForSeconds(0.4f);

            // 7. 궁극기 발사 시퀀스 완전히 마감 후 다음 턴 이관
            onComplete?.Invoke();
        }

        private IEnumerator CoAnimateUnitPull(BattleUnit unit, Vector3 targetWorldPos)
        {
            Vector3 startPos = unit.transform.position;
            float elapsed = 0f;

            GameObject trailObj = null;
            if (pullTrailPrefab != null)
            {
                trailObj = Instantiate(pullTrailPrefab, unit.transform.position, Quaternion.identity, unit.transform);
            }

            while (elapsed < pullSlideDuration)
            {
                elapsed += Time.deltaTime;
                unit.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / pullSlideDuration);
                yield return null;
            }

            unit.transform.position = targetWorldPos;
            if (trailObj != null)
            {
                Destroy(trailObj, 1f);
            }
        }

        /// <summary>
        /// 리플렉션을 이용해 유닛 이동에 맞게 그리드 점유 정보 테이블을 꼬임 없이 완벽하게 조율 보정합니다.
        /// </summary>
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
using UnityEngine;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 질량 축퇴 중심 타일에 생성되어 3턴간 머무르며 주변 유닛들에게 거리 비례 압축 데미지와 둔화를 부여하는 고유 장판 클래스입니다.
    /// </summary>
    public class MassCollapseFieldEffect : TileFieldEffect
    {
        private int sacrificeCount;
        private int casterBaseAttack;
        private GameObject slowVisualPrefab;

        /// <summary>
        /// 시전 시 집계되었던 희생 중력장 개수와 시전자의 기초 공격력 수치를 이전받아 셋업합니다.
        /// </summary>
        public void SetupFieldParameters(int sacrificeCount, int casterBaseAttack, GameObject slowVisualPrefab)
        {
            this.sacrificeCount = sacrificeCount;
            this.casterBaseAttack = casterBaseAttack;
            this.slowVisualPrefab = slowVisualPrefab;
        }

        /// <summary>
        /// 어떤 유닛이 자기 턴을 시작할 때, 중심으로부터의 거리 비례 압축 데미지 및 둔화를 발동시킵니다.
        /// </summary>
        public override void OnUnitTurnStart(BattleUnit unit)
        {
            if (unit == null || unit.CurrentHP <= 0) return;

            BattleGridManager gridManager = FindFirstObjectByType<BattleGridManager>();
            if (gridManager == null) return;

            Vector2Int unitCoord = gridManager.GetUnitCoordinate(unit);

            // Chebyshev 거리를 연산해 3x3 바운딩 박스 타일 내 범위 인원인지 대조
            int dx = Mathf.Abs(unitCoord.x - Coordinate.x);
            int dy = Mathf.Abs(unitCoord.y - Coordinate.y);
            int dist = Mathf.Max(dx, dy);

            if (dist <= 1)
            {
                // [둔화 1턴 부여]
                unit.ApplySlow(1, slowVisualPrefab);

                // [중심 비례 데미지 연산]: 중심에 가까울수록 (Dmg 10% + 희생 중력장 당 1%) 고유 압축 데미지 처리
                float turnStartDmgMod = 0.10f + (sacrificeCount * 0.01f);

                // 거리별 피해 감쇄: 중심(dist=0)이면 100%, 인접(dist=1)이면 75% 피해만 적용
                float distanceMultiplier = (dist == 0) ? 1.0f : 0.75f;
                int finalDotDamage = Mathf.RoundToInt(casterBaseAttack * turnStartDmgMod * distanceMultiplier);

                Debug.Log($"<color=#9933FF>[질량 고밀도 기믹]</color> {unit.UnitName}가 {Coordinate} 질량 축퇴 궤도 내에서 턴을 시작해 둔화 상태가 되었으며, {finalDotDamage}의 압축 피해를 받았습니다.");
                unit.TakeDamage(finalDotDamage);
            }
        }
    }
}
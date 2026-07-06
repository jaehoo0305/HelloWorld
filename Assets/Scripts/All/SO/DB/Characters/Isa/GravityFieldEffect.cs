using UnityEngine;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 아이사의 중력장 설치물에 장착되어 밟은 적을 센싱 및 둔화 디버프를 트리거하는 물리 장판 컴포넌트입니다.
    /// </summary>
    public class GravityFieldEffect : TileFieldEffect
    {
        [Header("[ 상태이상 연출 파티클 ]")]
        [Tooltip("적에게 적용될 둔화 오라/디버프 파티클 프리팹을 여기에 장착해 주세요.")]
        [SerializeField] private GameObject slowParticlePrefab;

        public override void Initialize(Vector2Int coord, string effectKey, BattleUnit owner, int duration)
        {
            base.Initialize(coord, effectKey, owner, duration);
            Debug.Log($"[중력장 형성] ({coord.x}, {coord.y}) 좌표에 중력 물리 구역이 안착되었습니다.");
        }

        /// <summary>
        /// 적이 이 중력장을 밟거나 정지할 때 자동 호출되어 수치 감쇄 둔화 효과를 트리거합니다.
        /// </summary>
        public override void OnUnitStepOn(BattleUnit unit)
        {
            if (unit == null || unit.CurrentHP <= 0) return;

            // 설치자(Owner)가 아군이고 장판을 밟은 유닛이 적군(EnemyUnit)일 때 작동
            if (Owner is PlayerUnit && unit is EnemyUnit enemy)
            {
                ApplySlowDebuff(enemy, 1);
            }
        }

        private void ApplySlowDebuff(EnemyUnit target, int turnCount)
        {
            // --- [수술적 수정] 기획에 맞춰 실시간 둔화 적용 및 준비된 디버프 파티클 스폰 ---
            target.ApplySlow(turnCount, slowParticlePrefab);

            Debug.Log($"<color=#99FF33>[중력장 디버프]</color> 적 {target.UnitName}가 중력장을 지나가며 {turnCount}턴 동안 속도가 절반으로 감소하는 둔화 상태가 되었습니다.");
        }
    }
}
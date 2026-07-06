using System.Collections.Generic;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 아이사 전용 고유 패시브 '중력 요동'을 처리하는 기획형 데이터/로직 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Passive_GravityFluctuation", menuName = "Dungeon/Passives/Gravity Fluctuation", order = 1)]
    public class GravityFluctuationPassiveSO : PassiveDataSO
    {
        [Header("[ 중력 요동 전용 프리팹 ]")]
        [Tooltip("GravityFieldEffect 컴포넌트가 장착된 3D 중력장 파티클 프리팹을 등록하세요.")]
        [SerializeField] private GameObject gravityFieldPrefab;

        /// <summary>
        /// 아이사의 턴이 돌아올 때마다 주변 N칸 내 무작위 빈 타일에 중력장을 실시간 생성합니다.
        /// </summary>
        public override void OnTurnStart(BattleUnit owner, int passiveLevel)
        {
            if (gravityFieldPrefab == null)
            {
                Debug.LogWarning($"[패시브] {owner.UnitName}의 중력 요동 중력장 프리팹이 할당되어 있지 않습니다.");
                return;
            }

            BattleGridManager gridManager = FindFirstObjectByType<BattleGridManager>();
            if (gridManager == null || BattleFieldEffectManager.Instance == null) return;

            // 정규식 파서로부터 기 기입된 설명글 {range:3} 및 {count:1} 값을 추출하여 유연하게 대입
            PassiveLevelData lvlData = GetLevelData(passiveLevel);
            int scanRange = (lvlData != null) ? lvlData.Range : 3;
            int maxSpawnCount = (lvlData != null) ? lvlData.SpawnCount : 1;

            Vector2Int ownerCoord = gridManager.GetUnitCoordinate(owner);
            List<Vector2Int> validTiles = new List<Vector2Int>();

            // 1. 자신 주위 scanRange칸 (맨해튼 거리 <= scanRange) 내 탐색 구역 필터링
            for (int x = -scanRange; x <= scanRange; x++)
            {
                for (int y = -scanRange; y <= scanRange; y++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) > scanRange) continue;
                    if (x == 0 && y == 0) continue; // 자신 발밑은 패시브 자동 설치 구역에서 배제

                    Vector2Int targetCoord = ownerCoord + new Vector2Int(x, y);

                    // 이동 가능하며 아직 중력장 효과가 중복 설치되지 않은 빈 격자 선별
                    if (gridManager.IsWalkable(targetCoord) &&
                        !BattleFieldEffectManager.Instance.HasEffectAt(targetCoord, "GravityField"))
                    {
                        validTiles.Add(targetCoord);
                    }
                }
            }

            // 2. 수집된 빈 격자 중 maxSpawnCount 개수만큼 랜덤 선택해 중력장 생성 (지속시간 무한 = 0)
            int spawnedCount = 0;
            while (validTiles.Count > 0 && spawnedCount < maxSpawnCount)
            {
                int randIndex = Random.Range(0, validTiles.Count);
                Vector2Int spawnCoord = validTiles[randIndex];
                validTiles.RemoveAt(randIndex);

                BattleFieldEffectManager.Instance.SpawnFieldEffect(
                    gravityFieldPrefab,
                    spawnCoord,
                    "GravityField",
                    owner,
                    0
                );

                spawnedCount++;
                Debug.Log($"[중력 요동 패시브] {owner.UnitName} 주변 격자 {spawnCoord}에 새로운 중력장을 설치했습니다.");
            }

            if (spawnedCount == 0)
            {
                Debug.Log($"[중력 요동 패시브] {owner.UnitName} 주위 {scanRange}칸 내에 중력장을 새로 배치할 공간이 없습니다.");
            }
        }
    }
}
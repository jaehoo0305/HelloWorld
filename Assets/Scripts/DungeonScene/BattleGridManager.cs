using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 3D 지형과 3D 콜라이더를 물리적으로 자동 스캔하여, 옥토패스 트래블러 스타일의
    /// 가상 격자 맵 정보 및 높이(Y축) 자동 보정을 제어하는 3D/2D 하이브리드 그리드 매니저입니다.
    /// </summary>
    public class BattleGridManager : MonoBehaviour
    {
        [Header("[ 1. 가상 그리드 스케일 설정 ]")]
        [Tooltip("그리드의 기점(0, 0)이 될 월드 좌표 오리진 포인트입니다.")]
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;
        [Tooltip("가상 격자의 가로(X) 및 세로(Y) 칸 개수 영역 크기입니다.")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(15, 15);
        [Tooltip("캐릭터가 한 보폭당 이동할 실제 물리적 격자 거리 크기입니다. (타일 애셋 크기와 독립적 조율 가능)")]
        [SerializeField] private float cellSize = 1.5f;

        [Header("[ 2. 물리 센서 스캔 필터 ]")]
        [Tooltip("캐릭터가 밟고 서 있을 수 있는 Walkable 3D 바닥 지형의 레이어를 연결하세요.")]
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("캐릭터가 지나갈 수 없는 벽, 장벽, 기둥 등의 장애물 레이어를 연결하세요.")]
        [SerializeField] private LayerMask obstacleLayer;
        [Tooltip("지형 높이를 탐색하기 위해 하늘에서 레이를 쏘아내릴 높이 오프셋입니다.")]
        [SerializeField] private float scanHeightOffset = 10f;

        // 실제 씬에 타일오브젝트를 두지 않는 대신, 이동 가능한 논리적 좌표들만 해시셋으로 보관
        private HashSet<Vector2Int> walkableGrid = new HashSet<Vector2Int>();

        // 3D 지형 경사로나 단차 높이에 맞춰 캐릭터 Y값을 스냅하기 위한 각 격자별 높이 맵 데이터베이스
        private Dictionary<Vector2Int, float> tileHeights = new Dictionary<Vector2Int, float>();

        // 실시간 유닛 점유 정보 관리 데이터베이스
        private Dictionary<Vector2Int, BattleUnit> occupiedUnits = new Dictionary<Vector2Int, BattleUnit>();
        private Dictionary<BattleUnit, Vector2Int> unitPositions = new Dictionary<BattleUnit, Vector2Int>();

        [Header("[ 이동 속도 세팅 ]")]
        [SerializeField] private float moveSpeed = 5f;

        public event Action<BattleUnit, Vector2Int> OnUnitMoveStart;
        public event Action<BattleUnit, Vector2Int> OnUnitMoveEnd;

        private void Awake()
        {
            occupiedUnits.Clear();
            unitPositions.Clear();

            // 게임 시작 시 3D 씬 내부의 콜라이더 지형을 실시간 격자로 정밀 자동 스캔합니다.
            ScanGridLevel();
        }

        /// <summary>
        /// 3D 지형의 레이아웃과 콜라이더를 분석하여 논리 격자 및 높이를 실시간 스캔 및 연산하는 통합 스캐너입니다.
        /// </summary>
        public void ScanGridLevel()
        {
            walkableGrid.Clear();
            tileHeights.Clear();

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    Vector2Int coord = new Vector2Int(x, z);

                    // 가상 격자 좌표를 기준 월드 위치로 가공
                    Vector3 cellWorldCenter = GetRawWorldPosition(coord);
                    Vector3 rayStart = cellWorldCenter + Vector3.up * scanHeightOffset;

                    // 1. 위에서 아래로 레이캐스트를 발사하여 groundLayer(땅)가 존재하는지 탐지합니다.
                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, scanHeightOffset * 2f, groundLayer))
                    {
                        float groundHeight = hit.point.y;
                        Vector3 floorPoint = new Vector3(cellWorldCenter.x, groundHeight, cellWorldCenter.z);

                        // 2. 해당 지점에 장애물 콜라이더(obstacleLayer)가 물리적으로 중첩되어 차단하고 있는지 체크합니다.
                        // cellSize 크기보다 약간 작은 오버랩 박스로 정밀 콜라이더 간섭 탐색 진행
                        Vector3 halfExtents = new Vector3((cellSize * 0.9f) / 2f, 1f, (cellSize * 0.9f) / 2f);
                        bool isBlocked = Physics.CheckBox(floorPoint + Vector3.up * 1f, halfExtents, Quaternion.identity, obstacleLayer);

                        if (!isBlocked)
                        {
                            // 땅이 존재하고 장애물이 없다면 이동 가능한 안전 격자로 등록
                            walkableGrid.Add(coord);
                            tileHeights[coord] = groundHeight;
                        }
                    }
                }
            }

            Debug.Log($"[그리드 스캐너] 스캔 완료! 총 {walkableGrid.Count}개의 유효 이동 격자가 발견되었습니다.");
        }

        /// <summary>
        /// 높이 스냅 보정을 제외한 논리 그리드 중심점 기준 월드 위치를 계산합니다.
        /// </summary>
        private Vector3 GetRawWorldPosition(Vector2Int coordinate)
        {
            return gridOrigin + new Vector3(coordinate.x * cellSize, 0f, coordinate.y * cellSize);
        }

        /// <summary>
        /// 특정 논리 격자의 3D 경사/단차가 보정된 최종 목적지 월드 좌표를 반환합니다.
        /// </summary>
        public Vector3 GetWorldPosition(Vector2Int coordinate)
        {
            float y = tileHeights.TryGetValue(coordinate, out float height) ? height : gridOrigin.y;
            Vector3 rawPos = GetRawWorldPosition(coordinate);
            return new Vector3(rawPos.x, y, rawPos.z);
        }

        /// <summary>
        /// 유닛의 초기 배치 좌표를 설정하고 스캔 높이에 맞춰 Y축 스냅 정렬 처리를 실행합니다.
        /// </summary>
        public void SetUnitInitialPosition(BattleUnit unit, Vector2Int coordinate)
        {
            if (unit == null) return;

            if (occupiedUnits.ContainsKey(coordinate))
            {
                Debug.LogWarning($"[그리드] {coordinate} 자리에 이미 다른 유닛이 존재해 배치할 수 없습니다.");
                return;
            }

            if (walkableGrid.Contains(coordinate))
            {
                unit.transform.position = GetWorldPosition(coordinate);
                unitPositions[unit] = coordinate;
                occupiedUnits[coordinate] = unit;
            }
        }

        /// <summary>
        /// 특정 유닛을 상하좌우 인접한 목표 가상 좌표로 1칸 이동시킵니다.
        /// </summary>
        public bool TryMoveUnitOneStep(BattleUnit unit, Vector2Int targetCoordinate)
        {
            if (unit == null) return false;
            if (!unitPositions.ContainsKey(unit))
            {
                Debug.LogWarning($"[그리드] {unit.UnitName}의 현재 위치 정보가 그리드 시스템에 기록되어 있지 않습니다.");
                return false;
            }

            Vector2Int currentCoords = unitPositions[unit];

            // 1. 상하좌우 십자 방향 인접 타일 검증 (대각선 불가, 맨해튼 거리 1 검증)
            int distance = Mathf.Abs(targetCoordinate.x - currentCoords.x) + Mathf.Abs(targetCoordinate.y - currentCoords.y);
            if (distance != 1)
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 {targetCoordinate}는 현재 위치 {currentCoords}와 인접(십자 방향 1칸)해 있지 않습니다.");
                return false;
            }

            // 2. 가상 스캔 맵 상에 등록된 유효 이동 가능 구역인지 체크 (장애물 및 영역 이탈 자동 차단)
            if (!walkableGrid.Contains(targetCoordinate))
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 좌표 {targetCoordinate}는 스캔되지 않은 비활성 영역이거나 벽 장애물 지역입니다.");
                return false;
            }

            // 3. 유닛 충돌 판정 (다른 아군/적군이 이미 서 있는지 검증)
            if (occupiedUnits.ContainsKey(targetCoordinate))
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 {targetCoordinate} 타일에는 이미 다른 유닛이 점유하고 있습니다.");
                return false;
            }

            // 4. 유닛 타입별 행동/비용 자원 검증
            if (unit is PlayerUnit playerUnit)
            {
                if (!playerUnit.ConsumeSP(1))
                {
                    Debug.LogWarning($"[그리드] 이동 실패: 아군 {playerUnit.UnitName}의 잔여 기력(SP)이 부족합니다.");
                    return false;
                }
            }
            else if (unit is EnemyUnit enemyUnit)
            {
                if (enemyUnit.HasMovedThisTurn)
                {
                    Debug.LogWarning($"[그리드] 이동 실패: 적군 {enemyUnit.UnitName}은 이미 이번 턴의 최대 이동력을 소진했습니다.");
                    return false;
                }
            }

            // 모든 검증 통과 후 실시간 좌표 이전 및 월드 좌표 이동 시작
            StartCoroutine(CoAnimateMovement(unit, currentCoords, targetCoordinate, GetWorldPosition(targetCoordinate)));
            return true;
        }

        private IEnumerator CoAnimateMovement(BattleUnit unit, Vector2Int startCoords, Vector2Int endCoords, Vector3 targetWorldPos)
        {
            OnUnitMoveStart?.Invoke(unit, endCoords);

            // 점유 정보 교체 처리
            occupiedUnits.Remove(startCoords);
            occupiedUnits[endCoords] = unit;
            unitPositions[unit] = endCoords;

            if (unit is EnemyUnit enemyUnit)
            {
                enemyUnit.HasMovedThisTurn = true;
            }

            // 3D 지차에 맞춘 물리적 높이를 보존하며 부드럽게 Lerp 이동 연출 진행
            Vector3 startPos = unit.transform.position;
            float elapsed = 0f;
            float duration = 1f / moveSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                unit.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / duration);
                yield return null;
            }

            unit.transform.position = targetWorldPos;
            OnUnitMoveEnd?.Invoke(unit, endCoords);
        }

        /// <summary>
        /// 특정 가상 격자 좌표가 통행이 허용된 빈 타일인지 여부를 판단합니다.
        /// </summary>
        public bool IsTileWalkableAndFree(Vector2Int coordinate)
        {
            return walkableGrid.Contains(coordinate) && !occupiedUnits.ContainsKey(coordinate);
        }

        /// <summary>
        /// 특정 유닛의 현재 격자 좌표를 안전하게 반환합니다.
        /// </summary>
        public Vector2Int GetUnitCoordinate(BattleUnit unit)
        {
            if (unit != null && unitPositions.TryGetValue(unit, out Vector2Int coord))
            {
                return coord;
            }
            return Vector2Int.zero;
        }

        // 인스펙터 편집 상태 기획 편의를 위한 전체 가상 스캔 범위 기즈모 표현 영역
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    Vector2Int coord = new Vector2Int(x, z);
                    Vector3 center = GetRawWorldPosition(coord);
                    Gizmos.DrawWireCube(center, new Vector3(cellSize * 0.95f, 0.1f, cellSize * 0.95f));
                }
            }
        }
    }
}
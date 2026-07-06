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

        public float MoveSpeed => moveSpeed;

        // --- [수술적 추가] 외부 시스템(SkillCaster 등)에서 참조할 격자 사이즈 정보 ---
        public Vector2Int GridSize => gridSize;

        public event Action<BattleUnit, Vector2Int> OnUnitMoveStart;
        public event Action<BattleUnit, Vector2Int> OnUnitMoveEnd;

        // --- A* 알고리즘용 내부 노드 구조체 ---
        private class AStarNode
        {
            public Vector2Int Position;
            public float G; // 시작점으로부터 이동해 온 실제 비용
            public float H; // 목적지까지 남은 가상의 예상 거리 (맨해튼 거리)
            public float F => G + 1.5f * H; // 가중치 1.5를 적용한 휴리스틱 공식 (F = G + 1.5H)
            public AStarNode Parent;

            public AStarNode(Vector2Int position, float g, float h, AStarNode parent)
            {
                Position = position;
                G = g;
                H = h;
                Parent = parent;
            }
        }

        private void Awake()
        {
            occupiedUnits.Clear();
            unitPositions.Clear();

            // 게임 시작 시 3D 씬 내부의 콜라이더 지형을 실시간 격자로 정밀 자동 스캔합니다.
            ScanGridLevel();
        }

        private void Start()
        {
            // 씬 내에 배치된 모든 전투 유닛들을 자동으로 찾아 각자가 지정한 시작 좌표에 그리드 등록을 진행합니다.
            BattleUnit[] units = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                Vector2Int spawnCoord = unit.InitialGridPosition;

                if (!walkableGrid.Contains(spawnCoord))
                {
                    Vector2Int calculatedCoord = WorldToGrid(unit.transform.position);
                    if (walkableGrid.Contains(calculatedCoord))
                    {
                        spawnCoord = calculatedCoord;
                        Debug.Log($"[그리드] {unit.UnitName}의 지정 격자 좌표가 범위 밖이므로, 월드 위치({unit.transform.position})를 역산하여 격자 {spawnCoord}에 정상 등록합니다.");
                    }
                    else
                    {
                        Debug.LogError($"[그리드] {unit.UnitName}의 현재 월드 위치가 스캔된 그리드 영역 바깥에 놓여 있습니다. 맵 오리진 범위나 Ground 지형 레이어를 확인하십시오.");
                        continue;
                    }
                }

                SetUnitInitialPosition(unit, spawnCoord);
            }
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

                    Vector3 cellWorldCenter = GetRawWorldPosition(coord);
                    Vector3 rayStart = cellWorldCenter + Vector3.up * scanHeightOffset;

                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, scanHeightOffset * 2f, groundLayer))
                    {
                        float groundHeight = hit.point.y;
                        Vector3 floorPoint = new Vector3(cellWorldCenter.x, groundHeight, cellWorldCenter.z);

                        // cellSize 크기보다 약간 작은 오버랩 박스로 정밀 콜라이더 간섭 탐색 진행
                        Vector3 halfExtents = new Vector3((cellSize * 0.9f) / 2f, 1f, (cellSize * 0.9f) / 2f);
                        bool isBlocked = Physics.CheckBox(floorPoint + Vector3.up * 1f, halfExtents, Quaternion.identity, obstacleLayer);

                        if (!isBlocked)
                        {
                            walkableGrid.Add(coord);
                            tileHeights[coord] = groundHeight;
                        }
                    }
                }
            }

            Debug.Log($"[그리드 스캐너] 스캔 완료! 총 {walkableGrid.Count}개의 유효 이동 격자가 발견되었습니다.");
        }

        /// <summary>
        /// 월드 3D 좌표를 역산하여 가장 가까운 가상 격자 좌표(X, Y)를 반환합니다.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 diff = worldPos - gridOrigin;
            int x = Mathf.RoundToInt(diff.x / cellSize);
            int y = Mathf.RoundToInt(diff.z / cellSize);
            return new Vector2Int(x, y);
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

            int distance = Mathf.Abs(targetCoordinate.x - currentCoords.x) + Mathf.Abs(targetCoordinate.y - currentCoords.y);
            if (distance != 1)
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 {targetCoordinate}는 현재 위치 {currentCoords}와 인접해 있지 않습니다.");
                return false;
            }

            if (!walkableGrid.Contains(targetCoordinate))
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 좌표 {targetCoordinate}는 스캔되지 않은 영역이거나 장애물 지역입니다.");
                return false;
            }

            if (occupiedUnits.ContainsKey(targetCoordinate))
            {
                Debug.LogWarning($"[그리드] 이동 실패: 목표 {targetCoordinate} 타일에는 이미 다른 유닛이 존재합니다.");
                return false;
            }

            if (unit is PlayerUnit playerUnit)
            {
                if (!playerUnit.ConsumeSP(1))
                {
                    Debug.LogWarning($"[그리드] 이동 실패: 아군 {playerUnit.UnitName}의 잔여 기력(SP)이 부족합니다.");
                    return false;
                }
            }

            StartCoroutine(CoAnimateMovement(unit, currentCoords, targetCoordinate, GetWorldPosition(targetCoordinate)));
            return true;
        }

        private IEnumerator CoAnimateMovement(BattleUnit unit, Vector2Int startCoords, Vector2Int endCoords, Vector3 targetWorldPos)
        {
            OnUnitMoveStart?.Invoke(unit, endCoords);

            occupiedUnits.Remove(startCoords);
            occupiedUnits[endCoords] = unit;
            unitPositions[unit] = endCoords;

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
        /// F = G + 1.5H 가중치 공식을 적용한 A* 알고리즘 기반 최단 경로 계산 메서드입니다.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, BattleUnit self)
        {
            if (!walkableGrid.Contains(end)) return null;

            List<AStarNode> openList = new List<AStarNode>();
            HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

            float startH = Mathf.Abs(end.x - start.x) + Mathf.Abs(end.y - start.y);
            openList.Add(new AStarNode(start, 0, startH, null));

            while (openList.Count > 0)
            {
                AStarNode current = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].F < current.F || (Mathf.Approximately(openList[i].F, current.F) && openList[i].H < current.H))
                    {
                        current = openList[i];
                    }
                }

                openList.Remove(current);
                closedList.Add(current.Position);

                if (current.Position == end)
                {
                    return RetracePath(current);
                }

                Vector2Int[] neighbors = {
                    new Vector2Int(0, 1),   // 상
                    new Vector2Int(0, -1),  // 하
                    new Vector2Int(-1, 0),  // 좌
                    new Vector2Int(1, 0)    // 우
                };

                foreach (var dir in neighbors)
                {
                    Vector2Int neighborPos = current.Position + dir;

                    if (closedList.Contains(neighborPos)) continue;
                    if (!walkableGrid.Contains(neighborPos)) continue;

                    if (neighborPos != end && occupiedUnits.ContainsKey(neighborPos))
                    {
                        if (occupiedUnits[neighborPos] != self)
                        {
                            continue;
                        }
                    }

                    float newG = current.G + 1f;
                    float newH = Mathf.Abs(end.x - neighborPos.x) + Mathf.Abs(end.y - neighborPos.y);

                    AStarNode existingNode = openList.Find(n => n.Position == neighborPos);
                    if (existingNode == null)
                    {
                        openList.Add(new AStarNode(neighborPos, newG, newH, current));
                    }
                    else if (newG < existingNode.G)
                    {
                        existingNode.G = newG;
                        existingNode.Parent = current;
                    }
                }
            }

            return null;
        }

        private List<Vector2Int> RetracePath(AStarNode node)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            AStarNode temp = node;
            while (temp != null)
            {
                path.Add(temp.Position);
                temp = temp.Parent;
            }
            path.Reverse();

            if (path.Count > 0)
            {
                path.RemoveAt(0);
            }
            return path;
        }

        public bool IsTileWalkableAndFree(Vector2Int coordinate)
        {
            return walkableGrid.Contains(coordinate) && !occupiedUnits.ContainsKey(coordinate);
        }

        public Vector2Int GetUnitCoordinate(BattleUnit unit)
        {
            if (unit != null && unitPositions.TryGetValue(unit, out Vector2Int coord))
            {
                return coord;
            }
            return Vector2Int.zero;
        }

        // --- [수술적 추가] 런타임에 외부 스킬 시스템이 맵 타일과 점유 정보를 물어볼 수 있도록 열어주는 API ---

        /// <summary>
        /// 해당 가상 격자 타일이 장애물이 없이 정상적으로 스캔되어 밟거나 투사체가 통과할 수 있는 공간인지 확인합니다.
        /// </summary>
        public bool IsWalkable(Vector2Int coordinate)
        {
            return walkableGrid.Contains(coordinate);
        }

        /// <summary>
        /// 지정한 가상 격자 타일 위에 점유하고 서 있는 전투 유닛(BattleUnit)을 즉시 조회하여 반환합니다.
        /// </summary>
        public BattleUnit GetUnitAt(Vector2Int coordinate)
        {
            return occupiedUnits.TryGetValue(coordinate, out BattleUnit unit) ? unit : null;
        }

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
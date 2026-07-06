using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 플레이어의 턴에 마우스 입력을 받아 사거리 시각화, 타겟 검증, 
    /// 스킬 발사 처리 및 전투 룰(SP 소모, 과열 누적, 추가 턴 획득)을 전담하는 스킬 컨트롤러입니다.
    /// </summary>
    public class SkillCaster : MonoBehaviour
    {
        public static SkillCaster Instance { get; private set; }

        [Header("[ 핵심 매니저 참조 ]")]
        [SerializeField] private BattleGridManager gridManager;
        [SerializeField] private BattleTurnManager turnManager;
        [SerializeField] private BattleUIController uiController;

        [Header("[ 스킬 타겟 검출 필터 ]")]
        [SerializeField] private LayerMask groundLayer;

        [Header("[ 사거리 가시화 설정 ]")]
        [SerializeField] private Color previewRangeColor = new Color(0f, 0.7f, 1f, 0.35f); // 1차 클릭: 부드러운 하늘색 프리뷰
        [SerializeField] private Color rangeTileColor = new Color(0f, 1f, 0.5f, 0.35f);    // 2차 클릭: 액티브 사거리
        [SerializeField] private Color splashTileColor = new Color(1f, 0.2f, 0.2f, 0.45f);   // 범위 지정 스플래시

        // 현재 시전을 시도 중인 스킬의 실시간 캐싱 데이터
        private PlayerUnit currentCaster;
        private SkillDataSO activeSkill;
        private int activeSkillLevel;

        // 시전 가능 범위 및 마우스 호버로 탐색된 스플래시 피해 범위 캐시
        private HashSet<Vector2Int> castingRangeTiles = new HashSet<Vector2Int>();
        private List<Vector2Int> activeSplashTiles = new List<Vector2Int>();
        private Vector2Int lastHoveredCoordinate = new Vector2Int(-1, -1);

        // --- [기획 변경 사항] 1차 클릭(프리뷰)과 2차 클릭(최종 조준) 구분을 위한 플래그 ---
        public bool IsPreviewMode { get; private set; } = false;

        // 스킬 시전 활성화 플래그 (이 값이 참이며 프리뷰가 아닐 때 격자 키보드 이동 입력을 통제함)
        public bool IsTargetingModeActive => activeSkill != null && !IsPreviewMode;

        public SkillDataSO ActiveSkill => activeSkill;

        public event Action OnTargetingModeStarted;
        public event Action OnTargetingModeEnded;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<BattleGridManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();
            if (uiController == null) uiController = FindFirstObjectByType<BattleUIController>();
        }

        private void Update()
        {
            if (activeSkill == null) return;

            // Z스킬 모드가 풀리거나 턴이 강제 만료되는 등 UI 상태 전환 시 타겟팅 취소 예외 처리
            if (turnManager.CurrentTurnUnit != currentCaster)
            {
                CancelTargetingMode();
                return;
            }

            // 1차 프리뷰 모드일 때의 업데이트 루프
            if (IsPreviewMode)
            {
                // 오른쪽 클릭 또는 ESC 키: 프리뷰 즉시 철회
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelTargetingMode();
                }
                return;
            }

            // 2. 2차 실시간 조작 모드 (마우스 호버링 및 방향/지점 타겟팅)
            UpdateMouseTargeting();

            // 왼쪽 클릭: 유효 타겟에 최종 스킬 시전 실행
            if (Input.GetMouseButtonDown(0))
            {
                TryExecuteActiveSkill();
            }

            // 오른쪽 클릭 또는 ESC 키: 시전 모드 즉시 탈출
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelTargetingMode();
            }
        }

        /// <summary>
        /// 스킬 단추를 1회 클릭했을 때: 툴팁 설명창과 격자 사거리를 표시하는 '프리뷰 모드'를 켭니다.
        /// </summary>
        public void PreviewSkill(PlayerUnit caster, SkillDataSO skill, int level)
        {
            if (caster == null || skill == null) return;

            currentCaster = caster;
            activeSkill = skill;
            activeSkillLevel = level;
            IsPreviewMode = true; // 프리뷰 모드 ON

            castingRangeTiles.Clear();
            activeSplashTiles.Clear();
            lastHoveredCoordinate = new Vector2Int(-1, -1);

            CalculateCastingRange();
            OnTargetingModeStarted?.Invoke();

            Debug.Log($"[스킬 프리뷰] {caster.UnitName}가 '{skill.SkillName}' 사거리({castingRangeTiles.Count}칸)를 가시화했습니다.");
        }

        /// <summary>
        /// 스킬 단추를 동일하게 2회 클릭했을 때: 마우스 포인팅 및 최종 시전을 허용하는 '타겟팅 모드'로 업그레이드합니다.
        /// </summary>
        public void SelectSkill(PlayerUnit caster, SkillDataSO skill, int level)
        {
            if (caster == null || skill == null) return;

            int requiredSP = GetDynamicRequiredSP(caster, skill);
            int totalAvailableSP = caster.CurrentSP + caster.CurrentBankSP;
            if (totalAvailableSP < requiredSP)
            {
                Debug.LogWarning($"[스킬] {caster.UnitName}의 SP 부족! (필요 기력: {requiredSP}, 소지 기력: {totalAvailableSP})");
                CancelTargetingMode();
                return;
            }

            currentCaster = caster;
            activeSkill = skill;
            activeSkillLevel = level;
            IsPreviewMode = false; // 프리뷰 모드 OFF -> 정밀 타겟팅 기동!

            CalculateCastingRange();
            OnTargetingModeStarted?.Invoke();

            Debug.Log($"<color=lime>[스킬 시전 대기]</color> '{skill.SkillName}' 최종 조준 모드 개시! 마우스를 조준하고 클릭하세요.");
        }

        public void CancelTargetingMode()
        {
            currentCaster = null;
            activeSkill = null;
            activeSkillLevel = 1;
            IsPreviewMode = false;

            castingRangeTiles.Clear();
            activeSplashTiles.Clear();
            lastHoveredCoordinate = new Vector2Int(-1, -1);

            OnTargetingModeEnded?.Invoke();
            Debug.Log("[스킬] 대상 타겟팅 지정을 전면 철회하고 일반 턴 행동 대기 상태로 전환합니다.");
        }

        private void CalculateCastingRange()
        {
            castingRangeTiles.Clear();
            if (currentCaster == null || activeSkill == null) return;

            Vector2Int casterCoord = gridManager.GetUnitCoordinate(currentCaster);
            SkillLevelData lvlData = activeSkill.GetLevelData(activeSkillLevel);

            int range = (lvlData != null) ? lvlData.Range : 3;
            SkillRangeType rangeType = SkillRangeType.Manhattan; // 기본값

            if (activeSkill.EnhanceLogicKey.Contains("Rook") || activeSkill.EnhanceLogicKey.Contains("Line"))
            {
                rangeType = SkillRangeType.Rook;
            }
            else if (activeSkill.EnhanceLogicKey.Contains("Square"))
            {
                rangeType = SkillRangeType.Square;
            }

            Vector2Int size = gridManager.GridSize;

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int target = new Vector2Int(x, z);
                    if (!gridManager.IsWalkable(target)) continue;

                    int dx = Mathf.Abs(target.x - casterCoord.x);
                    int dy = Mathf.Abs(target.y - casterCoord.y);

                    switch (rangeType)
                    {
                        case SkillRangeType.Manhattan:
                            if (dx + dy <= range)
                            {
                                castingRangeTiles.Add(target);
                            }
                            break;

                        case SkillRangeType.Rook:
                            if ((dx == 0 && dy <= range) || (dy == 0 && dx <= range))
                            {
                                castingRangeTiles.Add(target);
                            }
                            break;

                        case SkillRangeType.Square:
                            if (dx <= range && dy <= range)
                            {
                                castingRangeTiles.Add(target);
                            }
                            break;

                        case SkillRangeType.SelfOnly:
                            if (dx == 0 && dy == 0)
                            {
                                castingRangeTiles.Add(target);
                            }
                            break;
                    }
                }
            }
        }

        private void UpdateMouseTargeting()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                Vector2Int coord = gridManager.WorldToGrid(hit.point);

                if (coord != lastHoveredCoordinate)
                {
                    lastHoveredCoordinate = coord;
                    CalculateSplashArea(coord);
                }
            }
            else
            {
                lastHoveredCoordinate = new Vector2Int(-1, -1);
                activeSplashTiles.Clear();
            }
        }

        private void CalculateSplashArea(Vector2Int center)
        {
            activeSplashTiles.Clear();
            if (!castingRangeTiles.Contains(center)) return;

            SkillLevelData lvlData = activeSkill.GetLevelData(activeSkillLevel);

            int splashRadius = 0;
            if (lvlData != null)
            {
                splashRadius = Mathf.RoundToInt(lvlData.GetValue("splash", lvlData.GetValue("radius", 0f)));
            }

            SkillSplashType splashType = SkillSplashType.Single;

            if (splashRadius > 0)
            {
                if (activeSkill.EnhanceLogicKey.Contains("SplashSquare") || activeSkill.EnhanceLogicKey.Contains("Square"))
                {
                    splashType = SkillSplashType.Square;
                }
                else if (activeSkill.EnhanceLogicKey.Contains("SplashCross") || activeSkill.EnhanceLogicKey.Contains("Cross"))
                {
                    splashType = SkillSplashType.Cross;
                }
            }

            if (activeSkill.EnhanceLogicKey.Contains("All") || activeSkill.EnhanceLogicKey.Contains("Field"))
            {
                splashType = SkillSplashType.AllField;
            }

            switch (splashType)
            {
                case SkillSplashType.Single:
                    activeSplashTiles.Add(center);
                    break;

                case SkillSplashType.Square:
                    for (int x = -splashRadius; x <= splashRadius; x++)
                    {
                        for (int z = -splashRadius; z <= splashRadius; z++)
                        {
                            Vector2Int neighbor = center + new Vector2Int(x, z);
                            if (gridManager.IsWalkable(neighbor))
                            {
                                activeSplashTiles.Add(neighbor);
                            }
                        }
                    }
                    break;

                case SkillSplashType.Cross:
                    activeSplashTiles.Add(center);
                    for (int r = 1; r <= splashRadius; r++)
                    {
                        Vector2Int[] offsets = {
                            new Vector2Int(0, r),
                            new Vector2Int(0, -r),
                            new Vector2Int(-r, 0),
                            new Vector2Int(r, 0)
                        };
                        foreach (var offset in offsets)
                        {
                            Vector2Int neighbor = center + offset;
                            if (gridManager.IsWalkable(neighbor))
                            {
                                activeSplashTiles.Add(neighbor);
                            }
                        }
                    }
                    break;

                case SkillSplashType.AllField:
                    Vector2Int size = gridManager.GridSize;
                    for (int x = 0; x < size.x; x++)
                    {
                        for (int z = 0; z < size.y; z++)
                        {
                            Vector2Int target = new Vector2Int(x, z);
                            if (gridManager.IsWalkable(target))
                            {
                                activeSplashTiles.Add(target);
                            }
                        }
                    }
                    break;
            }
        }

        private void TryExecuteActiveSkill()
        {
            if (!castingRangeTiles.Contains(lastHoveredCoordinate))
            {
                Debug.LogWarning("[스킬] 유효한 사거리 영역 밖을 타겟팅했습니다.");
                return;
            }

            List<BattleUnit> validTargets = GetValidTargetsInSplash();
            if (validTargets.Count == 0 && activeSkill.TargetType != TargetType.Anyone)
            {
                Debug.LogWarning("[스킬] 범위 내에 스킬 효과를 적용받을 유효한 대상이 없습니다!");
                return;
            }

            int requiredSP = GetDynamicRequiredSP(currentCaster, activeSkill);

            if (!currentCaster.ConsumeSP(requiredSP))
            {
                Debug.LogError("[스킬] 자원 차감 실패!");
                CancelTargetingMode();
                return;
            }

            Debug.Log($"<color=#FFD700><b>[스킬 발동] {currentCaster.UnitName} -> {activeSkill.SkillName} (Lv.{activeSkillLevel})</b></color>");

            // 델리게이트 자율 시전 규칙 적용 연동 (질량 축퇴, 중력 응축 등의 커스텀 SO 호출)
            activeSkill.Execute(currentCaster, lastHoveredCoordinate, activeSkillLevel, () =>
            {
                bool shouldEndTurn = activeSkill.IsEndsTurn;

                uiController.HideAllTooltips();
                CancelTargetingMode();

                if (shouldEndTurn)
                {
                    turnManager.EndCurrentTurn();
                }
                else
                {
                    uiController.UpdateSkillButtons(currentCaster);
                }
            });
        }

        private int GetDynamicRequiredSP(PlayerUnit caster, SkillDataSO skill)
        {
            int finalCost = skill.RequiredSP;

            if (caster != null && caster.UnitName == "Isa")
            {
                Vector2Int currentCoord = gridManager.GetUnitCoordinate(caster);

                if (BattleFieldEffectManager.Instance != null &&
                    BattleFieldEffectManager.Instance.HasEffectAt(currentCoord, "GravityField"))
                {
                    finalCost = Mathf.Max(0, finalCost - 1);
                }
            }

            return finalCost;
        }

        private List<BattleUnit> GetValidTargetsInSplash()
        {
            List<BattleUnit> results = new List<BattleUnit>();

            foreach (Vector2Int tile in activeSplashTiles)
            {
                BattleUnit unit = gridManager.GetUnitAt(tile);
                if (unit == null) continue;

                bool isEnemy = unit is EnemyUnit;
                bool isAlly = unit is PlayerUnit;

                switch (activeSkill.TargetType)
                {
                    case TargetType.EnemySingle:
                    case TargetType.EnemyAll:
                        if (isEnemy) results.Add(unit);
                        break;

                    case TargetType.AllySingle:
                    case TargetType.AllyAll:
                        if (isAlly) results.Add(unit);
                        break;

                    case TargetType.Self:
                        if (unit == currentCaster) results.Add(unit);
                        break;

                    case TargetType.Anyone:
                        results.Add(unit);
                        break;
                }
            }

            return results;
        }

        private void OnDrawGizmos()
        {
            if (activeSkill == null || gridManager == null) return;

            foreach (var coord in castingRangeTiles)
            {
                Vector3 center = gridManager.GetWorldPosition(coord);
                if (IsPreviewMode)
                {
                    Gizmos.color = previewRangeColor; // 1차 선택: 하늘색 프리뷰 가이드 그리기
                }
                else if (activeSplashTiles.Contains(coord))
                {
                    Gizmos.color = splashTileColor;
                }
                else
                {
                    Gizmos.color = rangeTileColor;
                }
                Gizmos.DrawCube(center + Vector3.up * 0.05f, new Vector3(1.4f, 0.1f, 1.4f));
            }
        }
    }
}
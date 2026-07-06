using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCombat.Data;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonCombat.Combat
{
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
        [SerializeField] private Color previewRangeColor = new Color(0f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color rangeTileColor = new Color(0f, 1f, 0.5f, 0.35f);
        [SerializeField] private Color splashTileColor = new Color(1f, 0.2f, 0.2f, 0.45f);

        private Dictionary<Vector2Int, GameObject> spawnedVisualTiles = new Dictionary<Vector2Int, GameObject>();

        private PlayerUnit currentCaster;
        private SkillDataSO activeSkill;
        private int activeSkillLevel;
        private SkillDataSO originalSkillBeforeEnhancement;

        private HashSet<Vector2Int> castingRangeTiles = new HashSet<Vector2Int>();
        private List<Vector2Int> activeSplashTiles = new List<Vector2Int>();
        private Vector2Int lastHoveredCoordinate = new Vector2Int(-1, -1);
        private Vector2Int lastCasterCoordinate = new Vector2Int(-1, -1);

        public bool IsPreviewMode { get; private set; } = false;
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
            if (activeSkill == null || currentCaster == null) return;

            if (turnManager.CurrentTurnUnit != currentCaster)
            {
                CancelTargetingMode();
                return;
            }

            Vector2Int currentCasterPos = gridManager.GetUnitCoordinate(currentCaster);
            if (currentCasterPos != lastCasterCoordinate)
            {
                lastCasterCoordinate = currentCasterPos;
                CalculateCastingRange();
                CreateVisualTiles();
                lastHoveredCoordinate = new Vector2Int(-1, -1);
            }

            Vector2 mousePos = Vector2.zero;
            bool leftClicked = false;
            bool rightClicked = false;
            bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
                leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
                rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            }
            if (Keyboard.current != null)
            {
                escapePressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#else
            mousePos = Input.mousePosition;
            leftClicked = Input.GetMouseButtonDown(0);
            rightClicked = Input.GetMouseButtonDown(1);
            escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif

            if (IsPreviewMode)
            {
                if (rightClicked || escapePressed) CancelTargetingMode();
                return;
            }

            UpdateMouseTargeting(mousePos);

            if (leftClicked) TryExecuteActiveSkill();
            if (rightClicked || escapePressed) CancelTargetingMode();
        }

        public void ToggleZSkillState(bool isZActive)
        {
            if (activeSkill == null || currentCaster == null) return;

            if (isZActive)
            {
                if (activeSkill.EnhancedSkillAsset != null)
                {
                    originalSkillBeforeEnhancement = activeSkill;
                    activeSkill = activeSkill.EnhancedSkillAsset;
                }
            }
            else
            {
                if (originalSkillBeforeEnhancement != null)
                {
                    activeSkill = originalSkillBeforeEnhancement;
                }
            }

            CalculateCastingRange();
            CreateVisualTiles();
            UpdateVisualTileColors();
        }

        public string GetActiveSkillEnhanceConditionText()
        {
            if (originalSkillBeforeEnhancement != null)
            {
                if (!string.IsNullOrEmpty(originalSkillBeforeEnhancement.EnhanceConditionDesc))
                    return originalSkillBeforeEnhancement.EnhanceConditionDesc;

                if (!string.IsNullOrEmpty(originalSkillBeforeEnhancement.EnhanceLogicKey))
                {
                    switch (originalSkillBeforeEnhancement.EnhanceLogicKey.ToLower().Trim())
                    {
                        case "isa_cond_masscollapse":
                            return "중력장 최소 하나 존재, 소모한 누적 SP 10개";
                        case "isa_cond_heatdeath":
                            return "중력장 최소 하나 존재, 4라운드 이상 경과";
                    }
                }
            }
            return string.Empty;
        }

        public void PreviewSkill(PlayerUnit caster, SkillDataSO skill, int level)
        {
            if (caster == null || skill == null) return;

            currentCaster = caster;
            activeSkill = skill;
            activeSkillLevel = level;
            originalSkillBeforeEnhancement = (skill.EnhancedSkillAsset != null) ? skill : null;
            IsPreviewMode = true;
            lastCasterCoordinate = gridManager.GetUnitCoordinate(caster);

            castingRangeTiles.Clear();
            activeSplashTiles.Clear();
            lastHoveredCoordinate = new Vector2Int(-1, -1);

            CalculateCastingRange();
            CreateVisualTiles();
            OnTargetingModeStarted?.Invoke();
        }

        public void SelectSkill(PlayerUnit caster, SkillDataSO skill, int level)
        {
            if (caster == null || skill == null) return;

            if (BattleUIController.IsZSkillModeActive && skill.EnhancedSkillAsset == null && !string.IsNullOrEmpty(skill.EnhanceLogicKey))
            {
                int activeFields = BattleFieldEffectManager.Instance != null
                    ? BattleFieldEffectManager.Instance.GetEffectsByOwner(caster, "GravityField").Count
                    : 0;

                int currentRound = turnManager != null ? turnManager.CurrentRound : 1;

                bool isConditionMet = SkillEnhancementEvaluator.IsEnhancementConditionMet(
                    skill.EnhanceLogicKey,
                    caster.CumulativeSpentSP,
                    activeFields,
                    currentRound
                );

                if (!isConditionMet)
                {
                    Debug.LogWarning("[SkillCaster] Enhancement condition not met.");
                    return;
                }
            }

            int requiredSP = GetDynamicRequiredSP(caster, skill);
            if ((caster.CurrentSP + caster.CurrentBankSP) < requiredSP) return;

            currentCaster = caster;
            activeSkill = skill;
            activeSkillLevel = level;
            originalSkillBeforeEnhancement = (skill.EnhancedSkillAsset != null) ? skill : null;
            IsPreviewMode = false;
            lastCasterCoordinate = gridManager.GetUnitCoordinate(caster);

            CalculateCastingRange();
            CreateVisualTiles();
            OnTargetingModeStarted?.Invoke();
        }

        public void CancelTargetingMode()
        {
            currentCaster = null;
            activeSkill = null;
            activeSkillLevel = 1;
            IsPreviewMode = false;
            originalSkillBeforeEnhancement = null;
            lastCasterCoordinate = new Vector2Int(-1, -1);

            castingRangeTiles.Clear();
            activeSplashTiles.Clear();
            lastHoveredCoordinate = new Vector2Int(-1, -1);

            ClearVisualTiles();
            OnTargetingModeEnded?.Invoke();
            Debug.Log("[SkillCaster] Targeting canceled.");
        }

        private void CalculateCastingRange()
        {
            castingRangeTiles.Clear();
            if (currentCaster == null || activeSkill == null) return;

            Vector2Int casterCoord = gridManager.GetUnitCoordinate(currentCaster);
            SkillLevelData lvlData = activeSkill.GetLevelData(activeSkillLevel);
            int range = (lvlData != null) ? lvlData.Range : 3;

            // [수정 완료]: activeSkill.SkillName 이 null 일 경우를 안전 방어
            bool isRook = (activeSkill.SkillName != null && activeSkill.SkillName.Contains("응축")) ||
                          (activeSkill.EnhanceLogicKey != null && (activeSkill.EnhanceLogicKey.Contains("Rook") ||
                                                                   activeSkill.EnhanceLogicKey.Contains("Line") ||
                                                                   activeSkill.EnhanceLogicKey.Contains("Straight")));

            if (isRook)
            {
                Vector2Int[] rookDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in rookDirs)
                {
                    for (int r = 1; r <= range; r++)
                    {
                        Vector2Int stepCoord = casterCoord + dir * r;
                        if (!gridManager.IsWalkable(stepCoord)) break;
                        castingRangeTiles.Add(stepCoord);
                    }
                }
            }
            else
            {
                Vector2Int size = gridManager.GridSize;
                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.y; z++)
                    {
                        Vector2Int target = new Vector2Int(x, z);
                        if (!gridManager.IsWalkable(target)) continue;

                        int dx = Mathf.Abs(target.x - casterCoord.x);
                        int dy = Mathf.Abs(target.y - casterCoord.y);

                        if (dx + dy <= range) castingRangeTiles.Add(target);
                    }
                }
            }

            // [수정 완료]: 안전하게 null 조건 필터링 처리를 결합하여 연산 파이프라인 붕괴를 원천 차단
            bool isGravityConstrained = (activeSkill.EnhanceLogicKey != null && activeSkill.EnhanceLogicKey.Contains("MassCollapse")) ||
                                        (activeSkill.SkillName != null && (activeSkill.SkillName.Contains("질량") || activeSkill.SkillName.Contains("시공간 왜곡")));

            if (isGravityConstrained)
            {
                HashSet<Vector2Int> filteredTiles = new HashSet<Vector2Int>();
                foreach (var coord in castingRangeTiles)
                {
                    if (BattleFieldEffectManager.Instance != null &&
                        BattleFieldEffectManager.Instance.HasEffectAt(coord, "GravityField"))
                    {
                        filteredTiles.Add(coord);
                    }
                }
                castingRangeTiles = filteredTiles;
            }
        }

        private Vector2Int GetProjectileHitCoordinate(Vector2Int caster, Vector2Int target)
        {
            if (activeSkill == null || currentCaster == null) return target;

            bool isProjectileLine = (activeSkill.SkillName != null && activeSkill.SkillName.Contains("응축")) ||
                                    (activeSkill.EnhanceLogicKey != null && (activeSkill.EnhanceLogicKey.Contains("Rook") ||
                                                                             activeSkill.EnhanceLogicKey.Contains("Line") ||
                                                                             activeSkill.EnhanceLogicKey.Contains("Straight")));

            if (!isProjectileLine) return target;

            Vector2Int diff = target - caster;
            Vector2Int dir = new Vector2Int(Mathf.Clamp(diff.x, -1, 1), Mathf.Clamp(diff.y, -1, 1));
            if ((dir.x != 0 && dir.y != 0) || (dir.x == 0 && dir.y == 0)) return target;

            SkillLevelData lvlData = activeSkill.GetLevelData(activeSkillLevel);
            int skillMaxRange = (lvlData != null) ? lvlData.Range : 7;

            Vector2Int currentHitPos = caster;

            for (int i = 1; i <= skillMaxRange; i++)
            {
                Vector2Int nextTile = caster + dir * i;
                if (!gridManager.IsWalkable(nextTile)) break;

                currentHitPos = nextTile;

                BattleUnit unit = gridManager.GetUnitAt(nextTile);
                if (unit != null && unit is EnemyUnit && unit.CurrentHP > 0)
                {
                    return nextTile;
                }
            }
            return currentHitPos;
        }

        private void UpdateMouseTargeting(Vector2 mousePos)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                Vector2Int coord = gridManager.WorldToGrid(hit.point);
                if (coord != lastHoveredCoordinate)
                {
                    lastHoveredCoordinate = coord;
                    CalculateSplashArea(coord);
                    UpdateVisualTileColors();
                }
            }
            else
            {
                if (lastHoveredCoordinate != new Vector2Int(-1, -1))
                {
                    lastHoveredCoordinate = new Vector2Int(-1, -1);
                    activeSplashTiles.Clear();
                    UpdateVisualTileColors();
                }
            }
        }

        private void CalculateSplashArea(Vector2Int center)
        {
            activeSplashTiles.Clear();
            if (!castingRangeTiles.Contains(center)) return;

            Vector2Int casterCoord = gridManager.GetUnitCoordinate(currentCaster);
            Vector2Int realImpactCenter = GetProjectileHitCoordinate(casterCoord, center);

            SkillLevelData lvlData = activeSkill.GetLevelData(activeSkillLevel);
            int splashRadius = 0;
            if (lvlData != null)
            {
                splashRadius = Mathf.RoundToInt(lvlData.GetValue("splash", lvlData.GetValue("radius", 0f)));
            }

            if (splashRadius == 0)
            {
                activeSplashTiles.Add(realImpactCenter);
                return;
            }

            for (int x = -splashRadius; x <= splashRadius; x++)
            {
                for (int z = -splashRadius; z <= splashRadius; z++)
                {
                    Vector2Int neighbor = realImpactCenter + new Vector2Int(x, z);
                    if (gridManager.IsWalkable(neighbor)) activeSplashTiles.Add(neighbor);
                }
            }
        }

        private void CreateVisualTiles()
        {
            ClearVisualTiles();
            if (gridManager == null) return;

            Color baseColor = IsPreviewMode ? previewRangeColor : rangeTileColor;

            foreach (Vector2Int coord in castingRangeTiles)
            {
                Vector3 worldPos = gridManager.GetWorldPosition(coord);
                worldPos.y += 0.02f;

                GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tileObj.name = $"VisualRangeTile_{coord.x}_{coord.y}";
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                tileObj.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);

                Destroy(tileObj.GetComponent<Collider>());

                Renderer renderer = tileObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = baseColor;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                spawnedVisualTiles[coord] = tileObj;
            }
        }

        private void UpdateVisualTileColors()
        {
            Color baseColor = IsPreviewMode ? previewRangeColor : rangeTileColor;

            foreach (var kvp in spawnedVisualTiles)
            {
                Vector2Int coord = kvp.Key;
                GameObject tileObj = kvp.Value;
                if (tileObj == null) continue;

                Renderer renderer = tileObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = activeSplashTiles.Contains(coord) ? splashTileColor : baseColor;
                }
            }
        }

        private void ClearVisualTiles()
        {
            foreach (var kvp in spawnedVisualTiles)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            spawnedVisualTiles.Clear();
        }

        private void TryExecuteActiveSkill()
        {
            if (!castingRangeTiles.Contains(lastHoveredCoordinate)) return;

            Vector2Int casterCoord = gridManager.GetUnitCoordinate(currentCaster);
            Vector2Int realImpactCenter = GetProjectileHitCoordinate(casterCoord, lastHoveredCoordinate);

            List<BattleUnit> validTargets = new List<BattleUnit>();
            foreach (Vector2Int tile in activeSplashTiles)
            {
                BattleUnit unit = gridManager.GetUnitAt(tile);
                if (unit != null && unit is EnemyUnit && unit.CurrentHP > 0) validTargets.Add(unit);
            }

            if (validTargets.Count == 0 && !activeSkill.CanTargetEmptyGround && activeSkill.TargetType != TargetType.Anyone)
            {
                return;
            }

            int requiredSP = GetDynamicRequiredSP(currentCaster, activeSkill);
            if (!currentCaster.ConsumeSP(requiredSP)) return;

            activeSkill.Execute(currentCaster, realImpactCenter, activeSkillLevel, () =>
            {
                bool shouldEndTurn = activeSkill.IsEndsTurn;
                uiController.HideAllTooltips();
                CancelTargetingMode();

                if (shouldEndTurn) turnManager.EndCurrentTurn();
                else uiController.UpdateSkillButtons(currentCaster);
            });
        }

        private int GetDynamicRequiredSP(PlayerUnit caster, SkillDataSO skill)
        {
            int finalCost = skill.RequiredSP;
            if (caster != null && (caster.UnitName == "Isa" || caster.UnitName == "아이사" || caster.gameObject.name.Contains("Isa")))
            {
                Vector2Int currentCoord = gridManager.GetUnitCoordinate(caster);
                if (BattleFieldEffectManager.Instance != null && BattleFieldEffectManager.Instance.HasEffectAt(currentCoord, "GravityField"))
                {
                    finalCost = Mathf.Max(0, finalCost - 1);
                    Debug.Log($"[GravityField Cost Discount] {caster.UnitName} spent {finalCost} SP on {skill.SkillName}.");
                }
            }
            return finalCost;
        }

        private void OnDestroy()
        {
            ClearVisualTiles();
        }
    }
}
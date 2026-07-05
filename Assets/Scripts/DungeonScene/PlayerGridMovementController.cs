using UnityEngine;
using DungeonCombat.Data;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 플레이어의 턴일 때 WASD 또는 방향키 입력을 감지하여,
    /// BattleGridManager를 통해 십자 방향으로 1칸 이동을 명령하는 입력 핸들러 컴포넌트입니다.
    /// </summary>
    public class PlayerGridMovementController : MonoBehaviour
    {
        [Header("[ 핵심 매니저 연결 ]")]
        [SerializeField] private BattleGridManager gridManager;
        [SerializeField] private BattleTurnManager turnManager;
        [SerializeField] private BattleUIController uiController;

        // 중복 이동 입력 방지를 위한 플래그
        private bool isMoving = false;

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<BattleGridManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();
            if (uiController == null) uiController = FindFirstObjectByType<BattleUIController>();

            // 그리드 매니저의 이동 시작/종료 이벤트를 구독하여 중복 입력을 차단합니다.
            if (gridManager != null)
            {
                gridManager.OnUnitMoveStart += OnMoveStart;
                gridManager.OnUnitMoveEnd += OnMoveEnd;
            }
        }

        private void OnDestroy()
        {
            if (gridManager != null)
            {
                gridManager.OnUnitMoveStart -= OnMoveStart;
                gridManager.OnUnitMoveEnd -= OnMoveEnd;
            }
        }

        private void Update()
        {
            // 1. 매니저 참조가 누락되었거나 현재 다른 이동 연산이 처리 중이라면 입력을 무시합니다.
            if (gridManager == null || turnManager == null || isMoving) return;

            // 2. 현재 시네마틱 연출 배너가 떠 있는 상태라면 조작을 막습니다.
            if (uiController != null && uiController.IsVisualTransitionActive) return;

            // 3. 현재 전장의 주도권을 잡은 유닛이 플레이어가 직접 제어 가능한 아군인지 검사합니다.
            BattleUnit activeUnit = turnManager.CurrentTurnUnit;
            if (activeUnit == null || activeUnit is EnemyUnit) return;

            PlayerUnit playerUnit = activeUnit as PlayerUnit;
            if (playerUnit == null) return;

            // 4. 활성화된 입력 처리 장치에 맞춰 상하좌우 이동 벡터 계산
            Vector2Int moveDirection = Vector2Int.zero;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                    moveDirection = new Vector2Int(0, 1);
                else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                    moveDirection = new Vector2Int(0, -1);
                else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                    moveDirection = new Vector2Int(-1, 0);
                else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                    moveDirection = new Vector2Int(1, 0);
            }
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                moveDirection = new Vector2Int(0, 1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                moveDirection = new Vector2Int(0, -1);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                moveDirection = new Vector2Int(-1, 0);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                moveDirection = new Vector2Int(1, 0);
#endif

            // 5. 방향 입력이 감지되었다면 목표 좌표를 계산해 그리드 매니저에게 이동을 지시합니다.
            if (moveDirection != Vector2Int.zero)
            {
                Vector2Int currentCoords = gridManager.GetUnitCoordinate(playerUnit);
                Vector2Int targetCoords = currentCoords + moveDirection;

                // 이동을 시도합니다. (SP 비용 부족, 장애물 충돌, 범위 이탈 시 알아서 이동 취소 및 경고를 보냅니다)
                gridManager.TryMoveUnitOneStep(playerUnit, targetCoords);
            }
        }

        private void OnMoveStart(BattleUnit unit, Vector2Int targetCoords)
        {
            // 이동 중일 때는 추가 키보드 조작 입력을 차단합니다.
            isMoving = true;
        }

        private void OnMoveEnd(BattleUnit unit, Vector2Int targetCoords)
        {
            isMoving = false;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem; // 새로운 Input System을 위해 추가
using UnityEngine.InputSystem.Controls; // KeyControl 형식을 사용하기 위해 추가
using System.Collections;
using System.Collections.Generic; // 리스트 사용을 위해 추가

public class GridMovement : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize;       // 그리드 한 칸의 크기
    public float moveSpeed;      // 이동 속도

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayer;   // 장애물로 판정할 레이어
    public float checkRadius = 0.4f;  // 장애물 감지 범위
    public float yCheckOffset = 0.5f; // 감지 높이 보정 (캐릭터 허리 높이 정도가 적당)

    [Header("Input Buffering")]
    public float bufferWindow = 0.2f; // 입력이 버퍼에 머무는 시간
    private Vector2Int? bufferedInput; // 예약된 입력 방향
    private float bufferTimer;        // 버퍼 타이머

    [Header("State")]
    private Vector3 targetPosition;   // 다음에 이동할 목표 월드 좌표
    private bool isMoving = false;    // 현재 이동 중인지 확인
    private Vector2Int currentGridPos; // 현재 그리드 좌표 (x, z)

    // 입력된 방향들을 순서대로 저장하는 리스트
    private List<Vector2Int> inputStack = new List<Vector2Int>();

    void Start()
    {
        // 시작 위치를 그리드에 맞게 정렬
        SnapToGrid();
        targetPosition = transform.position;
    }

    void Update()
    {
        UpdateInputStack();

        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
            if (bufferTimer <= 0) bufferedInput = null;
        }

        if (!isMoving)
        {
            ProcessNextMovement();
        }
        else
        {
            MoveToTarget();
        }
    }

    private void UpdateInputStack()
    {
        if (Keyboard.current == null) return;

        CheckKey(Keyboard.current.wKey, Keyboard.current.upArrowKey, Vector2Int.up);
        CheckKey(Keyboard.current.sKey, Keyboard.current.downArrowKey, Vector2Int.down);
        CheckKey(Keyboard.current.aKey, Keyboard.current.leftArrowKey, Vector2Int.left);
        CheckKey(Keyboard.current.dKey, Keyboard.current.rightArrowKey, Vector2Int.right);
    }

    private void CheckKey(KeyControl mainKey, KeyControl arrowKey, Vector2Int dir)
    {
        if (mainKey.wasPressedThisFrame || arrowKey.wasPressedThisFrame)
        {
            bufferedInput = dir;
            bufferTimer = bufferWindow;

            if (!inputStack.Contains(dir)) inputStack.Add(dir);
        }

        if (mainKey.wasReleasedThisFrame || arrowKey.wasReleasedThisFrame)
        {
            inputStack.Remove(dir);
        }

        bool isPressed = mainKey.isPressed || arrowKey.isPressed;
        if (!isPressed && inputStack.Contains(dir)) inputStack.Remove(dir);
        else if (isPressed && !inputStack.Contains(dir)) inputStack.Add(dir);
    }

    private void ProcessNextMovement()
    {
        Vector2Int moveDir = Vector2Int.zero;
        bool hasValidInput = false;

        if (bufferedInput.HasValue)
        {
            moveDir = bufferedInput.Value;
            if (CanMove(currentGridPos + moveDir))
            {
                bufferedInput = null;
                bufferTimer = 0;
                hasValidInput = true;
            }
        }
        else if (inputStack.Count > 0)
        {
            moveDir = inputStack[inputStack.Count - 1];
            if (CanMove(currentGridPos + moveDir))
            {
                hasValidInput = true;
            }
        }

        if (hasValidInput)
        {
            currentGridPos += moveDir;
            targetPosition = GridToWorld(currentGridPos);
            isMoving = true;
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
        {
            transform.position = targetPosition;
            isMoving = false;
            ProcessNextMovement();
        }
    }

    private Vector3 GridToWorld(Vector2Int gridPos)
    {
        // Y값은 현재 캐릭터의 발 위치가 아니라 약간 위쪽(yCheckOffset)을 기준으로 감지하게 설정 가능
        return new Vector3(gridPos.x * cellSize, transform.position.y, gridPos.y * cellSize);
    }

    private void SnapToGrid()
    {
        currentGridPos.x = Mathf.RoundToInt(transform.position.x / cellSize);
        currentGridPos.y = Mathf.RoundToInt(transform.position.z / cellSize);
        transform.position = GridToWorld(currentGridPos);
    }

    private bool CanMove(Vector2Int targetGridPos)
    {
        // 감지할 중앙 위치 계산 (높이 보정 포함)
        Vector3 checkPos = GridToWorld(targetGridPos);
        checkPos.y += yCheckOffset;

        // 해당 위치에 장애물이 있는지 체크
        bool isBlocked = Physics.CheckSphere(checkPos, checkRadius, obstacleLayer);

        return !isBlocked;
    }

    // 씬 뷰에서 감지 범위를 항상 확인하기 위한 디버그 코드
    private void OnDrawGizmos()
    {
        Gizmos.color = isMoving ? Color.yellow : Color.green;
        Vector3 currentPos = transform.position;
        currentPos.y += yCheckOffset;
        Gizmos.DrawWireSphere(currentPos, checkRadius);

        // 현재 입력이 있다면 다음 갈 곳을 빨간색으로 표시
        if (inputStack.Count > 0)
        {
            Gizmos.color = Color.red;
            Vector3 nextPos = GridToWorld(currentGridPos + inputStack[inputStack.Count - 1]);
            nextPos.y += yCheckOffset;
            Gizmos.DrawWireSphere(nextPos, checkRadius);
        }
    }
}
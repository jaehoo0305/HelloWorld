using UnityEngine;
using UnityEngine.InputSystem; // 새로운 Input System을 위해 추가
using UnityEngine.InputSystem.Controls; // KeyControl 형식을 사용하기 위해 추가
using System.Collections;
using System.Collections.Generic; // 리스트 사용을 위해 추가

public class KingGridMovement : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 1f;       // 그리드 한 칸의 크기
    public float moveSpeed = 8f;      // 이동 속도

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayer;   // 장애물로 판정할 레이어 (예: "Obstacle" 레이어 생성 후 지정)
    public float checkRadius = 0.4f;  // 장애물 감지 범위 (cellSize보다 약간 작게 설정)

    [Header("Input Buffering")]
    public float bufferWindow = 0.2f; // 입력이 버퍼에 머무는 시간 (초)
    private Vector2Int? bufferedInput; // 예약된 입력 방향
    private float bufferTimer;        // 버퍼 타이머

    [Header("State")]
    private Vector3 targetPosition;   // 다음에 이동할 목표 월드 좌표
    private bool isMoving = false;    // 현재 이동 중인지 확인
    private Vector2Int currentGridPos; // 현재 그리드 좌표 (x, z)

    // 입력된 방향들을 순서대로 저장하는 리스트 (가장 뒤에 있는 것이 최신 입력)
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

        // 버퍼 타이머 관리
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
            // 이동 가능한 경우에만 버퍼를 비움 (막혔을 때는 버퍼를 유지해서 유저가 답답하지 않게 함)
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
        return new Vector3(gridPos.x * cellSize, transform.position.y, gridPos.y * cellSize);
    }

    private void SnapToGrid()
    {
        currentGridPos.x = Mathf.RoundToInt(transform.position.x / cellSize);
        currentGridPos.y = Mathf.RoundToInt(transform.position.z / cellSize);
        transform.position = GridToWorld(currentGridPos);
    }

    // [장애물 체크 핵심 함수]
    private bool CanMove(Vector2Int targetGridPos)
    {
        // 이동할 목표 지점의 월드 좌표 계산
        Vector3 checkPos = GridToWorld(targetGridPos);

        // 해당 위치에 obstacleLayer를 가진 콜라이더가 있는지 구체(Sphere) 형태로 체크
        // Physics.CheckSphere는 충돌체가 있으면 true를 반환하므로, !를 붙여 "없을 때만" true가 되게 함
        bool isBlocked = Physics.CheckSphere(checkPos, checkRadius, obstacleLayer);

        return !isBlocked;
    }

    // 에디터에서 체크 범위를 시각적으로 확인하기 위한 함수
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 nextPos = GridToWorld(currentGridPos + Vector2Int.up); // 예시로 위쪽 칸 표시
        Gizmos.DrawWireSphere(nextPos, checkRadius);
    }
}
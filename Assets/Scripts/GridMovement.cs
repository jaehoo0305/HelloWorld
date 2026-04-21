using UnityEngine;
using UnityEngine.InputSystem; // 새로운 Input System을 위해 추가
using UnityEngine.InputSystem.Controls; // KeyControl 형식을 사용하기 위해 추가
using System.Collections;
using System.Collections.Generic; // 리스트 사용을 위해 추가

public class GridMovement : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 1f;       // 그리드 한 칸의 크기
    public float moveSpeed = 8f;      // 이동 속도

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
        // 매 프레임 입력 상태를 업데이트
        UpdateInputStack();

        // 버퍼 타이머 감소
        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
            if (bufferTimer <= 0) bufferedInput = null; // 시간이 지나면 버퍼 비움
        }

        // 이동 중이 아닐 때 처리
        if (!isMoving)
        {
            ProcessNextMovement();
        }
        else
        {
            // 목표 지점까지 부드럽게 이동
            MoveToTarget();
        }
    }

    private void UpdateInputStack()
    {
        if (Keyboard.current == null) return;

        // 각 키별 방향 매핑 및 처리
        CheckKey(Keyboard.current.wKey, Keyboard.current.upArrowKey, Vector2Int.up);
        CheckKey(Keyboard.current.sKey, Keyboard.current.downArrowKey, Vector2Int.down);
        CheckKey(Keyboard.current.aKey, Keyboard.current.leftArrowKey, Vector2Int.left);
        CheckKey(Keyboard.current.dKey, Keyboard.current.rightArrowKey, Vector2Int.right);
    }

    private void CheckKey(KeyControl mainKey, KeyControl arrowKey, Vector2Int dir)
    {
        // [버퍼링 핵심] 키를 새로 눌렀을 때 버퍼에 저장
        if (mainKey.wasPressedThisFrame || arrowKey.wasPressedThisFrame)
        {
            bufferedInput = dir;
            bufferTimer = bufferWindow; // 지정된 시간 동안 이 입력을 기억함

            if (!inputStack.Contains(dir))
            {
                inputStack.Add(dir);
            }
        }

        // 키를 뗐을 때 리스트에서 제거
        if (mainKey.wasReleasedThisFrame || arrowKey.wasReleasedThisFrame)
        {
            inputStack.Remove(dir);
        }

        // 상태 실시간 동기화
        bool isPressed = mainKey.isPressed || arrowKey.isPressed;
        if (!isPressed && inputStack.Contains(dir))
        {
            inputStack.Remove(dir);
        }
        else if (isPressed && !inputStack.Contains(dir))
        {
            inputStack.Add(dir);
        }
    }

    private void ProcessNextMovement()
    {
        Vector2Int moveDir = Vector2Int.zero;
        bool hasValidInput = false;

        // 1순위: 버퍼에 담긴 예약 입력 확인
        if (bufferedInput.HasValue)
        {
            moveDir = bufferedInput.Value;
            bufferedInput = null; // 사용 후 버퍼 비움
            bufferTimer = 0;
            hasValidInput = true;
        }
        // 2순위: 현재 꾹 누르고 있는 키 확인
        else if (inputStack.Count > 0)
        {
            moveDir = inputStack[inputStack.Count - 1];
            hasValidInput = true;
        }

        if (hasValidInput)
        {
            Vector2Int nextGridPos = currentGridPos + moveDir;

            if (CanMove(nextGridPos))
            {
                currentGridPos = nextGridPos;
                targetPosition = GridToWorld(currentGridPos);
                isMoving = true;
            }
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // 목표 지점에 거의 도달했는지 확인
        if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
        {
            transform.position = targetPosition;
            isMoving = false;

            // 도착 즉시 다음 칸으로 부드럽게 이어짐 (버퍼 혹은 스택 확인)
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

    private bool CanMove(Vector2Int targetPos)
    {
        // 장애물 체크 로직
        return true;
    }
}
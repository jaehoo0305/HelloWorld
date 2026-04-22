using UnityEngine;
using UnityEngine.InputSystem; 
using UnityEngine.InputSystem.Controls;
using System.Collections;
using System.Collections.Generic;

public class KingGridMovement : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize;              // 그리드 한 칸의 크기
    public float moveSpeed;             // 이동 속도

    [Header("Input Buffering")]
    public float bufferWindow = 0.2f;   // 입력이 버퍼에 머무는 시간 (초)
    private Vector2Int? bufferedInput;  // 예약된 입력 방향
    private float bufferTimer;          // 버퍼 타이머

    [Header("State")]
    private Vector3 targetPosition;     // 다음에 이동할 목표 월드 좌표
    private bool isMoving = false;      // 현재 이동 중인지 확인
    private Vector2Int currentGridPos;  // 현재 그리드 좌표 (x, z)

    private List<Vector2Int> inputStack = new List<Vector2Int>();

    void Start()
    {
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

            if (!inputStack.Contains(dir))
            {
                inputStack.Add(dir);
            }
        }

        if (mainKey.wasReleasedThisFrame || arrowKey.wasReleasedThisFrame)
        {
            inputStack.Remove(dir);
        }

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

        if (bufferedInput.HasValue) // 1순위 - 버퍼에 담긴 예약 입력 확인
        {
            moveDir = bufferedInput.Value;
            bufferedInput = null;
            bufferTimer = 0;
            hasValidInput = true;
        }
        else if (inputStack.Count > 0) // 2순위 - 현재 꾹 누르고 있는 키 확인
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

    private bool CanMove(Vector2Int targetPos)
    {
        // 장애물 체크 로직
        return true;
    }
}
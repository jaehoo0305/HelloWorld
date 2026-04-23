using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;

public class KingGridMovement : MonoBehaviour
{
    [Header("Components")]
    private GridSensor sensor;        // 분리된 센서 컴포넌트

    [Header("Grid Settings")]
    public float cellSize;       // 그리드 한 칸의 크기
    public float moveSpeed;      // 이동 속도

    [Header("Input Buffering")]
    public float bufferWindow = 0.2f; // 입력이 버퍼에 머무는 시간
    private Vector2Int? bufferedInput; // 예약된 입력 방향
    private float bufferTimer;        // 버퍼 타이머

    [Header("State")]
    private Vector3 targetPosition;   // 목표 월드 좌표
    private bool isMoving = false;    // 현재 이동 중인지 확인
    private Vector2Int currentGridPos; // 현재 그리드 좌표 (x, z)

    // 입력 스택 (최신 입력 방향 유지)
    private List<Vector2Int> inputStack = new List<Vector2Int>();

    void Awake()
    {
        // 동일 오브젝트의 GridSensor 참조
        sensor = GetComponent<GridSensor>();
    }

    void Start()
    {
        // 현재 위치를 그리드에 맞게 정렬
        SnapToGrid();
        targetPosition = transform.position;
    }

    void Update()
    {
        UpdateInputStack();
        HandleBuffer();

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

        // 입력 상태 보정
        bool isPressed = mainKey.isPressed || arrowKey.isPressed;
        if (!isPressed && inputStack.Contains(dir)) inputStack.Remove(dir);
        else if (isPressed && !inputStack.Contains(dir)) inputStack.Add(dir);
    }

    private void HandleBuffer()
    {
        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
            if (bufferTimer <= 0) bufferedInput = null;
        }
    }

    private void ProcessNextMovement()
    {
        Vector2Int moveDir = Vector2Int.zero;
        bool inputDetected = false;

        // 1. 입력 방향 결정 (버퍼 우선)
        if (bufferedInput.HasValue)
        {
            moveDir = bufferedInput.Value;
            inputDetected = true;
        }
        else if (inputStack.Count > 0)
        {
            moveDir = inputStack[inputStack.Count - 1];
            inputDetected = true;
        }

        if (inputDetected)
        {
            Vector2Int nextGridPos = currentGridPos + moveDir;
            Vector3 nextWorldPos = GridToWorld(nextGridPos);

            // [로직 변경] 이동 가능 여부를 따지기 전에 씬 진입점(Enter)인지 먼저 체크
            string nextScene = sensor.GetEntrySceneName(nextWorldPos);

            if (!string.IsNullOrEmpty(nextScene))
            {
                // 입구라면 물리적으로 이동하지 않고 씬 전환만 수행
                // Enter 레이어는 IsWalkable에서 false를 반환하므로 캐릭터는 문 앞에 멈춤
                SceneLoader.Instance.LoadScene(nextScene);

                // 버퍼 비우기
                bufferedInput = null;
                bufferTimer = 0;
                return;
            }

            // 입구가 아닐 경우에만 이동 가능 여부(IsWalkable)를 확인
            if (sensor.IsWalkable(nextWorldPos))
            {
                bufferedInput = null;
                bufferTimer = 0;

                currentGridPos = nextGridPos;
                targetPosition = nextWorldPos;
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
            // 멈춤 없이 다음 입력 처리
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
}
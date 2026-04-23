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
    public float bufferWindow = 0.2f; // 입력 버퍼 시간
    private Vector2Int? bufferedInput;
    private float bufferTimer;

    [Header("State")]
    private Vector3 targetPosition;   // 목표 월드 좌표
    private bool isMoving = false;    // 이동 중 여부
    private Vector2Int currentGridPos; // 현재 그리드 좌표

    // 입력 스택 (최신 입력 우선순위 관리)
    private List<Vector2Int> inputStack = new List<Vector2Int>();

    void Awake()
    {
        // 동일한 오브젝트에 있는 GridSensor를 가져옵니다.
        sensor = GetComponent<GridSensor>();
    }

    void Start()
    {
        SnapToGrid();
        targetPosition = transform.position;
    }

    void Update()
    {
        UpdateInputStack();
        HandleBuffer();

        if (!isMoving)
        {
            // 이동 중이 아닐 때 다음 이동 결정
            ProcessNextMovement();
            // 이동 중이 아닐 때만 상호작용 가능
            HandleInteraction();
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

        // 키 상태 강제 동기화
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
        bool hasInput = false;

        // 1순위: 버퍼된 입력이 있고 해당 방향이 이동 가능한가?
        if (bufferedInput.HasValue)
        {
            Vector2Int nextPos = currentGridPos + bufferedInput.Value;
            if (sensor.IsWalkable(GridToWorld(nextPos)))
            {
                moveDir = bufferedInput.Value;
                bufferedInput = null;
                bufferTimer = 0;
                hasInput = true;
            }
        }

        // 2순위: 현재 누르고 있는 키 중 최신 입력이 이동 가능한가?
        if (!hasInput && inputStack.Count > 0)
        {
            moveDir = inputStack[inputStack.Count - 1];
            if (sensor.IsWalkable(GridToWorld(currentGridPos + moveDir)))
            {
                hasInput = true;
            }
        }

        if (hasInput)
        {
            currentGridPos += moveDir;
            targetPosition = GridToWorld(currentGridPos);
            isMoving = true;
        }
    }

    private void HandleInteraction()
    {
        // 스페이스바를 누르면 현재 바라보는 방향(최신 입력 방향)의 상호작용체 확인
        if (Keyboard.current.spaceKey.wasPressedThisFrame && inputStack.Count > 0)
        {
            Vector2Int facingDir = inputStack[inputStack.Count - 1];
            Collider interactable = sensor.GetInteractable(GridToWorld(currentGridPos + facingDir));

            if (interactable != null)
            {
                Debug.Log($"[상호작용] 대상: {interactable.name}");
                // 예: interactable.GetComponent<IInteractable>()?.OnInteract();
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
            // 멈춤 없이 즉시 다음 이동 확인
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
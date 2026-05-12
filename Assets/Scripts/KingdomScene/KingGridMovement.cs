using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;
using System.Linq;

public class KingGridMovement : MonoBehaviour
{
    [Header("Components")]
    private GridSensor sensor;
    [SerializeField] private FacilityDataSO facilityData; // 상점 데이터 참조

    [Header("Grid Settings")]
    public float cellSize = 1f;
    public float moveSpeed = 8f;

    [Header("Input Buffering")]
    public float bufferWindow = 0.2f;
    private Vector2Int? bufferedInput;
    private float bufferTimer;

    [Header("State")]
    private Vector3 targetPosition;
    private bool isMoving = false;
    private Vector2Int currentGridPos;

    private List<Vector2Int> inputStack = new List<Vector2Int>();

    void Awake()
    {
        sensor = GetComponent<GridSensor>();
    }

    void Start()
    {
        // 씬 시작 시 상점에서 돌아오는 중인지 확인합니다.
        if (facilityData != null && facilityData.isReturning)
        {
            SetPositionToRandomGate();
        }
        else
        {
            SnapToGrid();
        }

        targetPosition = transform.position;
    }

    /// <summary>
    /// 현재 씬 내의 같은 건물 타입 문들 중 하나를 랜덤하게 골라 플레이어를 배치합니다.
    /// </summary>
    private void SetPositionToRandomGate()
    {
        // 현재 씬에 있는 모든 SceneGate를 찾습니다.
        SceneGate[] allGates = Object.FindObjectsByType<SceneGate>(FindObjectsSortMode.None);

        // 방금 머물렀던 상점 타입과 일치하는 게이트들만 필터링합니다.
        var validGates = allGates.Where(g => g.targetFacility == facilityData.currentFacility).ToList();

        if (validGates.Count > 0)
        {
            // 랜덤하게 하나의 문을 선택합니다.
            int randomIndex = Random.Range(0, validGates.Count);
            SceneGate selectedGate = validGates[randomIndex];

            // 선택된 게이트의 퇴장 방향 데이터를 가져옵니다.
            Vector2Int exitDir = selectedGate.GetExitDirectionVector();

            // 게이트의 위치에서 퇴장 방향으로 cellSize만큼 떨어진 위치를 스폰 지점으로 계산합니다.
            Vector3 spawnPos = selectedGate.transform.position;
            spawnPos.x += exitDir.x * cellSize;
            spawnPos.z += exitDir.y * cellSize;
            spawnPos.y = transform.position.y; // Y축은 캐릭터 높이 유지

            transform.position = spawnPos;

            // 그리드 좌표 갱신 및 복귀 플래그 초기화
            SnapToGrid();
            facilityData.isReturning = false;

            Debug.Log($"[Spawn] {facilityData.currentFacility}의 {validGates.Count}개 문 중 랜덤 스폰 완료.");
        }
        else
        {
            SnapToGrid();
            facilityData.isReturning = false;
        }
    }

    void Update()
    {
        // 씬 로딩 중에는 조작을 완전히 차단합니다.
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
        {
            inputStack.Clear();
            bufferedInput = null;
            return;
        }

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

            // 다음 칸이 입구인지 확인
            string nextScene = sensor.GetEntrySceneName(nextWorldPos);

            if (!string.IsNullOrEmpty(nextScene))
            {
                SceneLoader.Instance.LoadScene(nextScene);
                bufferedInput = null;
                bufferTimer = 0;
                return;
            }

            // 이동 가능 여부 확인
            if (sensor.IsWalkable(nextWorldPos))
            {
                bufferedInput = null;
                bufferTimer = 0;

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
        if (cellSize > 0)
        {
            currentGridPos.x = Mathf.RoundToInt(transform.position.x / cellSize);
            currentGridPos.y = Mathf.RoundToInt(transform.position.z / cellSize);
            transform.position = GridToWorld(currentGridPos);
        }
    }
}
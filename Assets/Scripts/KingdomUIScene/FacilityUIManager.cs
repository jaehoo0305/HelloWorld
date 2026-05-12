using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages camera position and rotation along a circular orbit for facility UI.
/// </summary>
public class FacilityUIManager : MonoBehaviour
{
    [Header("Data References")]
    [SerializeField] private FacilityDataSO facilityData;

    [Header("Camera Settings")]
    [SerializeField] private Transform uiCameraTransform;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float cameraRadius = 10f;

    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.5f;
    [SerializeField] private float sHoldExitTime = 1.5f;
    [SerializeField] private string exitSceneName = "KingdomScene";

    // Constants to avoid magic numbers
    private const int TotalFacilityCount = 8;
    private const float AngleStep = 360f / TotalFacilityCount;
    private const float PrecisionThreshold = 0.001f;

    private int _targetIndex = 0;
    private Vector3 _targetPosition;
    private float _targetYRotation;

    private float _lastInputTime = -10f;
    private float _sHoldTimer = 0f;

    private void Start()
    {
        if (facilityData == null || uiCameraTransform == null)
        {
            Debug.LogError("FacilityUIManager: Required references (DataSO or Camera) are missing.");
            return;
        }

        // Initialize index based on data from Kingdom Scene
        _targetIndex = facilityData.CurrentIndex;

        // Apply initial transform immediately
        UpdateTargetTransform();
        uiCameraTransform.position = _targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, _targetYRotation, 0);

        Debug.Log($"[FacilityUI] Entered. Target Facility: {facilityData.currentFacility}");
    }

    private void Update()
    {
        HandleInput();
        MoveAndRotateCameraSmoothly();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // --- 1. A, D Key Navigation (with cooldown) ---
        if (Time.time >= _lastInputTime + inputCooldown)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                ApplyNavigation(-1);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                ApplyNavigation(1);
            }
        }

        // --- 2. S Key Hold (Exit Logic) ---
        if (Keyboard.current.sKey.isPressed)
        {
            _sHoldTimer += Time.deltaTime;

            if (_sHoldTimer >= sHoldExitTime)
            {
                ExitToKingdomScene();
            }
        }
        else
        {
            _sHoldTimer = 0f;
        }
    }

    private void ApplyNavigation(int direction)
    {
        _targetIndex += direction;
        UpdateTargetTransform();
        _lastInputTime = Time.time;
    }

    private void ExitToKingdomScene()
    {
        Debug.Log("[FacilityUI] Exiting to KingdomScene via S-Key hold.");

        // [추가] 상점에서 나간다는 사실을 데이터에 기록
        if (facilityData != null)
        {
            facilityData.isReturning = true;
        }

        SceneManager.LoadScene(exitSceneName);
    }

    private void UpdateTargetTransform()
    {
        _targetYRotation = _targetIndex * AngleStep;

        float radian = _targetYRotation * Mathf.Deg2Rad;
        float x = Mathf.Sin(radian) * cameraRadius;
        float z = Mathf.Cos(radian) * cameraRadius;

        // Floating point snapping
        if (Mathf.Abs(x) < PrecisionThreshold) x = 0f;
        if (Mathf.Abs(z) < PrecisionThreshold) z = 0f;

        _targetPosition = new Vector3(x, 0, z);

        // Wrap index (0 to TotalFacilityCount - 1)
        int wrappedIndex = (_targetIndex % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;
        facilityData.SetFacility((FacilityType)wrappedIndex);

        Debug.Log($"[FacilityUI] Switch focus to index: {wrappedIndex} ({facilityData.currentFacility})");
    }

    private void MoveAndRotateCameraSmoothly()
    {
        uiCameraTransform.position = Vector3.Lerp(uiCameraTransform.position, _targetPosition, Time.deltaTime * movementSpeed);

        float currentY = uiCameraTransform.eulerAngles.y;
        float nextY = Mathf.LerpAngle(currentY, _targetYRotation, Time.deltaTime * rotationSpeed);
        uiCameraTransform.rotation = Quaternion.Euler(0, nextY, 0);
    }
}
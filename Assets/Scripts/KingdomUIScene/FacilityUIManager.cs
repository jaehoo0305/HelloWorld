using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages camera orbit, facility highlighting, and priority-based arrow UI.
/// Supports active input feedback and idle state visibility.
/// </summary>
public class FacilityUIManager : MonoBehaviour
{
    [Header("Data Reference")]
    [SerializeField] private FacilityDataSO facilityData;

    [Header("Camera Settings")]
    [SerializeField] private Transform uiCameraTransform;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float cameraRadius = 10f;

    [Header("Facility Icon Settings")]
    [SerializeField] private Image[] facilityIcons;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    [SerializeField] private float visualTransitionSpeed = 15f;

    [Header("Arrow UI Settings (A, D, S)")]
    [SerializeField] private Image leftArrow;
    [SerializeField] private Image rightArrow;
    [SerializeField] private Image downArrow;
    [SerializeField] private float arrowShowDuration = 2.0f;
    [SerializeField] private float arrowFadeSpeed = 5.0f;

    [Header("Idle UI Settings")]
    [SerializeField] private float idleTimeout = 5.0f; // Seconds of inactivity (excluding A, S, D)

    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.5f;
    [SerializeField] private float sHoldExitTime = 1.5f;
    [SerializeField] private string exitSceneName = "KingdomScene";

    // Constants
    private const int TotalFacilityCount = 8;
    private const float AngleStep = 360f / TotalFacilityCount;
    private const float PrecisionThreshold = 0.001f;
    private const float MouseMoveThreshold = 0.1f;

    private int _targetIndex = 0;
    private int _currentActiveIndex = 0;
    private Vector3 _targetPosition;
    private float _targetYRotation;

    private float _lastInputTime = -10f;
    private float _sHoldTimer = 0f;

    // Arrow specific states
    private float _leftArrowTimer = 0f;
    private float _rightArrowTimer = 0f;

    // Idle detection
    private float _idleTimer = 0f;
    private Vector2 _lastMousePos;

    private void Start()
    {
        if (facilityData == null || uiCameraTransform == null)
        {
            Debug.LogError("FacilityUIManager: Essential references are missing.");
            return;
        }

        if (Mouse.current != null)
        {
            _lastMousePos = Mouse.current.position.ReadValue();
        }

        // Initialize arrows to transparent
        SetImageAlpha(leftArrow, 0f);
        SetImageAlpha(rightArrow, 0f);
        SetImageAlpha(downArrow, 0f);

        _targetIndex = facilityData.CurrentIndex;
        _currentActiveIndex = (_targetIndex % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;

        UpdateTargetTransform();
        uiCameraTransform.position = _targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, _targetYRotation, 0);

        InitializeIconColors();

        Debug.Log($"[FacilityUI] System Initialized for {facilityData.currentFacility}");
    }

    private void Update()
    {
        HandleInput();
        UpdateIdleDetection();
        UpdateArrowVisuals();
        MoveAndRotateCameraSmoothly();
        UpdateIconColorsSmoothly();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // Navigation (A, D)
        if (Time.time >= _lastInputTime + inputCooldown)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                ApplyNavigation(-1);
                TriggerArrow(ref _leftArrowTimer, ref _rightArrowTimer);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                ApplyNavigation(1);
                TriggerArrow(ref _rightArrowTimer, ref _leftArrowTimer);
            }
        }

        // Exit (S Hold)
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

    /// <summary>
    /// Tracks inactivity, specifically ignoring A, S, and D keyboard inputs.
    /// </summary>
    private void UpdateIdleDetection()
    {
        bool isAnyActivity = false;

        // 1. Check Keyboard activity EXCLUDING A, S, D
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            // If the key pressed is NOT A, S, or D, count as activity
            if (!Keyboard.current.aKey.wasPressedThisFrame &&
                !Keyboard.current.sKey.wasPressedThisFrame &&
                !Keyboard.current.dKey.wasPressedThisFrame)
            {
                isAnyActivity = true;
            }
        }

        // 2. Check Mouse Click
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
        {
            isAnyActivity = true;
        }

        // 3. Check Mouse Movement
        if (Mouse.current != null)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            if (Vector2.Distance(currentMousePos, _lastMousePos) > MouseMoveThreshold)
            {
                isAnyActivity = true;
                _lastMousePos = currentMousePos;
            }
        }

        if (isAnyActivity)
        {
            _idleTimer = 0f;
        }
        else
        {
            _idleTimer += Time.deltaTime;
        }
    }

    private void TriggerArrow(ref float activeTimer, ref float inactiveTimer)
    {
        activeTimer = arrowShowDuration;
        inactiveTimer = 0f;
        _lastInputTime = Time.time;
    }

    /// <summary>
    /// Updates alpha for each arrow based on a priority system:
    /// Active Input > Idle State > Hidden
    /// </summary>
    private void UpdateArrowVisuals()
    {
        bool isIdle = _idleTimer >= idleTimeout;
        bool isSPressed = Keyboard.current != null && Keyboard.current.sKey.isPressed;

        // Update timers
        if (_leftArrowTimer > 0) _leftArrowTimer -= Time.deltaTime;
        if (_rightArrowTimer > 0) _rightArrowTimer -= Time.deltaTime;

        // Check if there is ANY active specific input being processed
        bool hasActiveSpecificInput = (_leftArrowTimer > 0 || _rightArrowTimer > 0 || isSPressed);

        // Calculate Target Alphas using priority logic
        // Priority 1: Current specific input (A, S, D)
        // Priority 2: Idle state (Show all 3) - Only if Priority 1 is not active for OTHER arrows
        float leftTarget = (_leftArrowTimer > 0 || (isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;
        float rightTarget = (_rightArrowTimer > 0 || (isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;
        float downTarget = (isSPressed || (isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;

        // Apply Lerp for smooth transition
        LerpAlpha(leftArrow, leftTarget);
        LerpAlpha(rightArrow, rightTarget);
        LerpAlpha(downArrow, downTarget);
    }

    private void LerpAlpha(Image img, float targetAlpha)
    {
        if (img == null) return;
        Color color = img.color;
        color.a = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * arrowFadeSpeed);
        img.color = color;
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color color = img.color;
        color.a = alpha;
        img.color = color;
    }

    private void ApplyNavigation(int direction)
    {
        _targetIndex += direction;
        UpdateTargetTransform();
    }

    private void ExitToKingdomScene()
    {
        Debug.Log("[FacilityUI] Exiting.");
        if (facilityData != null) facilityData.isReturning = true;
        SceneManager.LoadScene(exitSceneName);
    }

    private void UpdateTargetTransform()
    {
        _targetYRotation = _targetIndex * AngleStep;

        float radian = _targetYRotation * Mathf.Deg2Rad;
        float x = Mathf.Sin(radian) * cameraRadius;
        float z = Mathf.Cos(radian) * cameraRadius;

        if (Mathf.Abs(x) < PrecisionThreshold) x = 0f;
        if (Mathf.Abs(z) < PrecisionThreshold) z = 0f;

        _targetPosition = new Vector3(x, 0, z);

        _currentActiveIndex = (_targetIndex % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;
        facilityData.SetFacility((FacilityType)_currentActiveIndex);
    }

    private void InitializeIconColors()
    {
        if (facilityIcons == null) return;
        for (int i = 0; i < facilityIcons.Length; i++)
        {
            if (facilityIcons[i] == null) continue;
            facilityIcons[i].color = (i == _currentActiveIndex) ? activeColor : inactiveColor;
        }
    }

    private void UpdateIconColorsSmoothly()
    {
        if (facilityIcons == null) return;
        for (int i = 0; i < facilityIcons.Length; i++)
        {
            if (facilityIcons[i] == null) continue;
            Color targetColor = (i == _currentActiveIndex) ? activeColor : inactiveColor;
            facilityIcons[i].color = Color.Lerp(facilityIcons[i].color, targetColor, Time.deltaTime * visualTransitionSpeed);
        }
    }

    private void MoveAndRotateCameraSmoothly()
    {
        uiCameraTransform.position = Vector3.Lerp(uiCameraTransform.position, _targetPosition, Time.deltaTime * movementSpeed);
        float currentY = uiCameraTransform.eulerAngles.y;
        float nextY = Mathf.LerpAngle(currentY, _targetYRotation, Time.deltaTime * rotationSpeed);
        uiCameraTransform.rotation = Quaternion.Euler(0, nextY, 0);
    }
}
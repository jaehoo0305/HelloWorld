using UnityEngine;
using UnityEngine.UI; // Image 컴포넌트 사용을 위해 필요
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 상점 UI의 원형 궤도 카메라 이동 및 아이콘 하이라이트 효과를 관리하는 매니저입니다.
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

    [Header("UI Visual Effect Settings")]
    [SerializeField] private Image[] facilityIcons; // 8개의 아이콘을 순서대로 할당
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    [SerializeField] private float visualTransitionSpeed = 15f; // 색상 전환 속도 (높을수록 빠름)

    [Header("Inupt Settings")]
    [SerializeField] private float inputCooldown = 0.5f;
    [SerializeField] private float sHoldExitTime = 1.5f;
    [SerializeField] private string exitSceneName = "KingdomScene";

    // 상수 정의
    private const int TotalFacilityCount = 8;
    private const float AngleStep = 360f / TotalFacilityCount;
    private const float PrecisionThreshold = 0.001f;

    private int _targetIndex = 0;
    private int _currentActiveIndex = 0; // 현재 강조되어야 할 아이콘 인덱스
    private Vector3 _targetPosition;
    private float _targetYRotation;

    private float _lastInputTime = -10f;
    private float _sHoldTimer = 0f;

    private void Start()
    {
        if (facilityData == null || uiCameraTransform == null)
        {
            Debug.LogError("FacilityUIManager: Where is camera or DataSO?");
            return;
        }

        if (facilityIcons == null || facilityIcons.Length != TotalFacilityCount)
        {
            Debug.LogWarning("FacilityUIManager: Facility Icon List is 8!");
        }

        // 이전 씬에서 넘어온 데이터를 기반으로 인덱스 초기화
        _targetIndex = facilityData.CurrentIndex;
        _currentActiveIndex = (_targetIndex % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;

        // 초기 상태 즉시 적용
        UpdateTargetTransform();
        uiCameraTransform.position = _targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, _targetYRotation, 0);

        // 초기 아이콘 색상 강제 설정
        InitializeIconColors();

        Debug.Log($"[FacilityUI] {facilityData.currentFacility} Started");
    }

    private void Update()
    {
        HandleInput();
        MoveAndRotateCameraSmoothly();
        UpdateIconColorsSmoothly(); // 매 프레임 색상을 부드럽게 변경
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // --- 좌우 이동 관리 (쿨타임 적용) ---
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

        // --- 퇴장 로직 (S 키 홀드) ---
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
        Debug.Log("[FacilityUI] KingdomScene Exit");

        if (facilityData != null)
        {
            facilityData.isReturning = true;
        }

        SceneManager.LoadScene(exitSceneName);
    }

    private void UpdateTargetTransform()
    {
        _targetYRotation = _targetIndex * AngleStep;

        // 궤도 위치 계산
        float radian = _targetYRotation * Mathf.Deg2Rad;
        float x = Mathf.Sin(radian) * cameraRadius;
        float z = Mathf.Cos(radian) * cameraRadius;

        // 미세 오차 보정
        if (Mathf.Abs(x) < PrecisionThreshold) x = 0f;
        if (Mathf.Abs(z) < PrecisionThreshold) z = 0f;

        _targetPosition = new Vector3(x, 0, z);

        // 현재 인덱스 계산 및 데이터 갱신
        _currentActiveIndex = (_targetIndex % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;
        facilityData.SetFacility((FacilityType)_currentActiveIndex);

        Debug.Log($"[FacilityUI] Target is {_currentActiveIndex} ({facilityData.currentFacility}) Changed.");
    }

    /// <summary>
    /// 시작 시 아이콘 색상을 초기화합니다.
    /// </summary>
    private void InitializeIconColors()
    {
        if (facilityIcons == null) return;
        for (int i = 0; i < facilityIcons.Length; i++)
        {
            if (facilityIcons[i] == null) continue;
            facilityIcons[i].color = (i == _currentActiveIndex) ? activeColor : inactiveColor;
        }
    }

    /// <summary>
    /// 아이콘의 색상을 목표 색상으로 부드럽게 선형 보간합니다.
    /// </summary>
    private void UpdateIconColorsSmoothly()
    {
        if (facilityIcons == null) return;

        for (int i = 0; i < facilityIcons.Length; i++)
        {
            if (facilityIcons[i] == null) continue;

            // 목표 색상 결정
            Color targetColor = (i == _currentActiveIndex) ? activeColor : inactiveColor;

            // 현재 색상에서 목표 색상으로 선형 보간 이동
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
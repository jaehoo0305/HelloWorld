using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 상점 UI의 원형 궤도를 따라 카메라 위치와 회전을 동시에 관리하는 매니저
/// </summary>
public class FacilityUIManager : MonoBehaviour
{
    [Header("데이터 참조")]
    [SerializeField] private FacilityDataSO facilityData;

    [Header("카메라 설정")]
    [SerializeField] private Transform uiCameraTransform;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float cameraRadius = 10f; // 카메라가 움직일 원의 반지름

    private const float AngleStep = 45f; // 8개 상점 (360 / 8)
    private int _targetIndex = 0;

    private Vector3 _targetPosition;
    private float _targetYRotation;

    private void Start()
    {
        if (facilityData == null || uiCameraTransform == null)
        {
            Debug.LogError("FacilityUIManager: 필수 참조가 누락되었습니다.");
            return;
        }

        // 1. Kingdom Scene에서 전달받은 데이터로 초기 인덱스 설정
        _targetIndex = facilityData.CurrentIndex;

        // 2. 초기 위치 및 회전 즉시 계산 및 적용
        UpdateTargetTransform();
        uiCameraTransform.position = _targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, _targetYRotation, 0);

        Debug.Log($"[UI 입장] {facilityData.currentFacility} 위치로 카메라 이동 완료");
    }

    private void Update()
    {
        HandleInput();
        MoveAndRotateCameraSmoothly();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // A키: 왼쪽 (인덱스 감소), D키: 오른쪽 (인덱스 증가)
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            _targetIndex--;
            UpdateTargetTransform();
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            _targetIndex++;
            UpdateTargetTransform();
        }
    }

    /// <summary>
    /// 목표 인덱스에 따른 카메라의 목표 좌표와 회전각을 계산합니다.
    /// </summary>
    private void UpdateTargetTransform()
    {
        _targetYRotation = _targetIndex * AngleStep;

        // 원형 궤도 좌표 계산 (삼각함수 사용)
        float radian = _targetYRotation * Mathf.Deg2Rad;
        float x = Mathf.Sin(radian) * cameraRadius;
        float z = Mathf.Cos(radian) * cameraRadius;

        // [부동 소수점 오차 수정] 아주 미세한 값(0에 가까운 값)은 0으로 고정
        if (Mathf.Abs(x) < 0.001f) x = 0f;
        if (Mathf.Abs(z) < 0.001f) z = 0f;

        _targetPosition = new Vector3(x, 0, z);

        // 데이터 갱신 (0~7 랩핑)
        int wrappedIndex = (_targetIndex % 8 + 8) % 8;
        facilityData.SetFacility((FacilityType)wrappedIndex);
    }

    /// <summary>
    /// 카메라를 목표 위치와 회전값으로 부드럽게 보간 이동시킵니다.
    /// </summary>
    private void MoveAndRotateCameraSmoothly()
    {
        // 1. 위치 이동 (Vector3.Lerp)
        uiCameraTransform.position = Vector3.Lerp(uiCameraTransform.position, _targetPosition, Time.deltaTime * movementSpeed);

        // 2. 회전 이동 (Mathf.LerpAngle 사용하여 최단거리 회전)
        float currentY = uiCameraTransform.eulerAngles.y;
        float nextY = Mathf.LerpAngle(currentY, _targetYRotation, Time.deltaTime * rotationSpeed);
        uiCameraTransform.rotation = Quaternion.Euler(0, nextY, 0);
    }
}
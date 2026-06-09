using UnityEngine;

/// <summary>
/// 중앙 사령탑의 TargetIndex를 바탕으로 카메라 위치와 회전을 수학적으로 계산합니다.
/// </summary>
public class FacilityCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FacilityManager manager;
    [SerializeField] private Transform uiCameraTransform;

    [Header("Camera Settings")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float cameraRadius = 10f;

    // 외부(줌 컨트롤러 등)에서 카메라 반지름 값을 읽어갈 수 있도록 프로퍼티 추가
    public float CameraRadius => cameraRadius;

    private const float AngleStep = 360f / FacilityManager.TotalFacilityCount;
    private const float PrecisionThreshold = 0.001f;

    private Vector3 _targetPosition;
    private float _targetYRotation;

    private void Start()
    {
        if (manager == null || uiCameraTransform == null) return;

        // 씬 진입 시 대기 시간 없이 즉시 카메라를 배치
        CalculateTargetTransform();
        uiCameraTransform.position = _targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, _targetYRotation, 0);
    }

    private void Update()
    {
        if (manager == null || uiCameraTransform == null) return;

        CalculateTargetTransform();
        MoveAndRotateCameraSmoothly();
    }

    private void CalculateTargetTransform()
    {
        _targetYRotation = manager.TargetIndex * AngleStep;

        float radian = _targetYRotation * Mathf.Deg2Rad;
        float x = Mathf.Sin(radian) * cameraRadius;
        float z = Mathf.Cos(radian) * cameraRadius;

        if (Mathf.Abs(x) < PrecisionThreshold) x = 0f;
        if (Mathf.Abs(z) < PrecisionThreshold) z = 0f;

        _targetPosition = new Vector3(x, 0, z);
    }

    private void MoveAndRotateCameraSmoothly()
    {
        uiCameraTransform.position = Vector3.Lerp(uiCameraTransform.position, _targetPosition, Time.deltaTime * movementSpeed);

        float currentY = uiCameraTransform.eulerAngles.y;
        float nextY = Mathf.LerpAngle(currentY, _targetYRotation, Time.deltaTime * rotationSpeed);
        uiCameraTransform.rotation = Quaternion.Euler(0, nextY, 0);
    }
}
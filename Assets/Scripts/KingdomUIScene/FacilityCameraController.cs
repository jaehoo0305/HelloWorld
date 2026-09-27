using UnityEngine;

public class FacilityCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FacilityManager manager;
    [SerializeField] private Transform uiCameraTransform;

    [Header("Camera Settings")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float cameraRadius = 10f;

    public float CameraRadius
    {
        get { return cameraRadius; }
    } 

    private const float AngleStep = 360f / FacilityManager.TotalFacilityCount;

    private Vector3 targetPosition;
    private float targetRotation;

    private void Start()
    {
        CalculateTargetTransform();

        uiCameraTransform.position = targetPosition;
        uiCameraTransform.rotation = Quaternion.Euler(0, targetRotation, 0);
    }

    private void Update()
    {
        CalculateTargetTransform();
        MoveAndRotateCameraSmooth();
    }

    private void CalculateTargetTransform()
    {
        targetRotation = manager.TargetIndex * AngleStep;
        targetPosition = Quaternion.Euler(0, targetRotation, 0) * new Vector3(0, 0, cameraRadius);
    }

    private void MoveAndRotateCameraSmooth()
    {
        uiCameraTransform.position = Vector3.Lerp(uiCameraTransform.position, targetPosition, Time.deltaTime * movementSpeed);

        float currentY = uiCameraTransform.eulerAngles.y;
        float nextY = Mathf.LerpAngle(currentY, targetRotation, Time.deltaTime * rotationSpeed);
        uiCameraTransform.rotation = Quaternion.Euler(0, nextY, 0);
    }
}
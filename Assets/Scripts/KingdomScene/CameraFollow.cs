using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;

    [Header("Movement Settings")]
    public float smoothTime = 0.3f;                 // 카메라가 타겟에 도달하는 데 걸리는 대략적인 시간
    public Vector3 offset;                          // 타겟과의 유지 거리 (상태창에서 수동 조절 가능)

    private Vector3 currentVelocity = Vector3.zero; // SmoothDamp 내부 계산용 속도

    void Start()
    {
        if (target != null && offset == Vector3.zero)
        {
            offset = transform.position - target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }
}
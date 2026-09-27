using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;           // 따라갈 대상

    [Header("Movement Settings")]
    public float smoothTime = 0.3f;
    public Vector3 offset;             // 타겟과의 상대 거리

    private Vector3 currentVelocity = Vector3.zero;
    private bool isInitialized = false;

    void Start()
    {
        FindTarget();

        if (target != null)
        {
            // 인스펙터의 Offset이 (0,0,0)으로 비어있다면 현재 카메라와 플레이어의 초기 거리로 자동 계산
            if (offset == Vector3.zero)
            {
                offset = transform.position - target.position;
            }

            // 씬 시작 시 부드럽게 오지 않고 플레이어 위치로 즉시 스냅(Snap)
            SnapToTarget();
        }
    }

    void LateUpdate()
    {
        // 씬 전환 등으로 타겟이 끊겼을 경우 재검색
        if (target == null)
        {
            FindTarget();
            if (target == null) return;

            // 다시 찾았을 때 오프셋이 없다면 재계산 후 즉시 스냅
            if (offset == Vector3.zero)
            {
                offset = new Vector3(0, 9.49f, -12.65f); // 34도 디오라마 권장 기본값
            }
            SnapToTarget();
            return;
        }

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }

    private void FindTarget()
    {
        // 1. Tag가 "Player"인 오브젝트 검색
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            return;
        }

        // 2. 컴포넌트 타입으로 직접 검색
        KingGridMovement movement = Object.FindFirstObjectByType<KingGridMovement>();
        if (movement != null)
        {
            target = movement.transform;
        }
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        currentVelocity = Vector3.zero;
    }
}
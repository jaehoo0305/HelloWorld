using UnityEngine;

public class GridSensor : MonoBehaviour
{
    [Header("Detection Settings")]
    public LayerMask obstacleLayer;     // 장애물 레이어
    public LayerMask interactableLayer; // 상호작용 레이어
    public LayerMask enterLayer;        // 씬 진입(Enter) 레이어

    public float checkRadius = 0.4f;    // 감지 범위
    public float yOffset = 0.5f;        // 감지 높이

    // 특정 위치가 이동 가능한지 확인 (엔터 레이어가 있으면 장애물 무시)
    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3 checkPos = worldPos;
        checkPos.y += yOffset;

        // 장애물 레이어 '또는' 엔터 레이어가 있다면 갈 수 없는 곳으로 판정
        bool isObstacle = Physics.CheckSphere(checkPos, checkRadius, obstacleLayer);
        bool isEnter = Physics.CheckSphere(checkPos, checkRadius, enterLayer);

        // 둘 중 하나라도 걸리면 false 반환 (막혀있음)
        return !(isObstacle || isEnter);
    }

    // 진입할 씬 이름 가져오기 및 상점 데이터 세팅
    public string GetEntrySceneName(Vector3 worldPos)
    {
        Vector3 checkPos = worldPos;
        checkPos.y += yOffset;

        Collider[] hitColliders = Physics.OverlapSphere(checkPos, checkRadius, enterLayer);
        if (hitColliders.Length > 0)
        {
            SceneGate gate = hitColliders[0].GetComponent<SceneGate>();
            if (gate != null)
            {
                // [핵심 추가] 씬을 이동하기 전에 해당 게이트가 가진 상점 정보를 
                // ScriptableObject(FacilityDataSO)에 먼저 기록합니다.
                gate.PrepareTransition();

                return gate.destinationSceneName;
            }
        }
        return null;
    }

    public Collider GetInteractable(Vector3 worldPos)
    {
        Vector3 checkPos = worldPos;
        checkPos.y += yOffset;

        Collider[] hitColliders = Physics.OverlapSphere(checkPos, checkRadius, interactableLayer);
        if (hitColliders.Length > 0) return hitColliders[0];

        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 debugPos = transform.position;
        debugPos.y += yOffset;
        Gizmos.DrawWireSphere(debugPos, checkRadius);
    }
}
using UnityEngine;

public enum ExitDirection
{
    Up,
    Down,
    Left,
    Right
}

public class SceneGate : MonoBehaviour
{
    [Header("Scene Movement Settings")]
    public string destinationSceneName = "KingdomUIScene";

    [Header("Building Data Settings")]
    public FacilityType targetFacility;
    public FacilityDataSO facilityData;

    [Header("Exit Settings")]
    public ExitDirection exitDirection = ExitDirection.Down;

    public void PrepareTransition()
    {
        if (facilityData != null)
        {
            facilityData.SetFacility(targetFacility);
            // 함수 이름을 GetExitDirectionVector로 통일했습니다.
            facilityData.SetExitDirection(GetExitDirectionVector());
            facilityData.isReturning = false;
        }
    }

    // 이름을 GetExitDirectionVector로 변경하고 public으로 설정하여 외부(KingGridMovement)에서 접근 가능하게 했습니다.
    public Vector2Int GetExitDirectionVector()
    {
        switch (exitDirection)
        {
            case ExitDirection.Up: return Vector2Int.up;
            case ExitDirection.Down: return Vector2Int.down;
            case ExitDirection.Left: return Vector2Int.left;
            case ExitDirection.Right: return Vector2Int.right;
            default: return Vector2Int.down;
        }
    }
}
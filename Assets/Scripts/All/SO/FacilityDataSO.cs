using UnityEngine;

[CreateAssetMenu(fileName = "FacilityData", menuName = "Kingdom/FacilityData")]
public class FacilityDataSO : ScriptableObject
{
    [Header("현재 시설 정보")]
    public FacilityType currentFacility;

    [Header("퇴장 설정")]
    [Tooltip("상점에서 다시 광장으로 나올 때 플레이어가 바라보고 이동할 방향 벡터입니다.")]
    public Vector2Int exitDirection;

    [Tooltip("현재 상점에서 나가는 중인지 확인하는 플래그입니다.")]
    public bool isReturning;

    public int CurrentIndex => (int)currentFacility;

    /// <summary>
    /// 진입하는 시설의 종류를 설정합니다.
    /// </summary>
    public void SetFacility(FacilityType newFacility)
    {
        currentFacility = newFacility;
    }

    /// <summary>   
    /// 해당 시설에서 퇴장할 때의 방향을 설정합니다. (SceneGate에서 호출)
    /// </summary>
    /// <param name="direction">퇴장 방향 벡터</param>
    public void SetExitDirection(Vector2Int direction)
    {
        exitDirection = direction;
    }
}
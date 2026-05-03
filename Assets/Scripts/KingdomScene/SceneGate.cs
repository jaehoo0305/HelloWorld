using UnityEngine;

public class SceneGate : MonoBehaviour
{
    [Header("씬 이동 설정")]
    // GridSensor가 찾는 변수명을 정확히 일치시킵니다 (오류 해결)
    public string destinationSceneName = "KingdomUIScene";

    [Header("상점 데이터 설정")]
    public FacilityType targetFacility;
    public FacilityDataSO facilityData;

    // 플레이어가 진입했을 때 데이터를 먼저 설정하는 함수
    public void PrepareTransition()
    {
        if (facilityData != null)
        {
            facilityData.SetFacility(targetFacility);
        }
    }
}
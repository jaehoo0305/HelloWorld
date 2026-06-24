using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 각 시설의 개별 해금(Unlock) 및 레벨 정보를 담는 구조체입니다.
/// </summary>
[System.Serializable]
public struct FacilityUnlockInfo
{
    public FacilityType facilityType;
    public UnlockType unlockType;
    public bool isUnlocked; // 현재 해금 여부
    public int level;       // 현재 시설 레벨 (0레벨부터 시작 가능)
}

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

    [Header("해금 설정")]
    [Tooltip("모든 시설의 해금 정보를 관리하는 리스트입니다. 인스펙터에서 시설별로 추가할 수 있습니다.")]
    public List<FacilityUnlockInfo> facilityList = new List<FacilityUnlockInfo>();

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

    /// <summary>
    /// 특정 시설이 현재 해금된 상태인지 확인합니다.
    /// 만약 리스트가 비어있거나 찾을 수 없는 경우, 기본값으로 '해금(true)'을 반환합니다.
    /// </summary>
    public bool IsFacilityUnlocked(FacilityType type)
    {
        if (facilityList == null || facilityList.Count == 0)
            return true;

        FacilityUnlockInfo info = facilityList.Find(x => x.facilityType == type);

        return info.facilityType == type ? info.isUnlocked : true;
    }

    /// <summary>
    /// 특정 시설의 현재 레벨을 안전하게 조회합니다.
    /// </summary>
    public int GetFacilityLevel(FacilityType type)
    {
        if (facilityList == null || facilityList.Count == 0)
            return 0;

        FacilityUnlockInfo info = facilityList.Find(x => x.facilityType == type);

        return info.facilityType == type ? info.level : 0;
    }

    /// <summary>
    /// 특정 시설의 레벨을 외부에서 직접 설정하여 갱신할 수 있도록 돕는 메서드입니다.
    /// </summary>
    public void SetFacilityLevel(FacilityType type, int newLevel)
    {
        if (facilityList == null) return;

        for (int i = 0; i < facilityList.Count; i++)
        {
            if (facilityList[i].facilityType == type)
            {
                FacilityUnlockInfo updatedInfo = facilityList[i];
                updatedInfo.level = newLevel;
                facilityList[i] = updatedInfo;
                return;
            }
        }
    }
}
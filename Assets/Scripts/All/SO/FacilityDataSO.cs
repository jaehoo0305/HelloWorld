using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct FacilityUnlockInfo
{
    public FacilityType facilityType;
    public UnlockType unlockType;
    public bool isUnlocked;
    public int level;
}

[CreateAssetMenu(fileName = "FacilityData", menuName = "Kingdom/FacilityData")]
public class FacilityDataSO : ScriptableObject
{
    [Header("기준 데이터베이스 연결")]
    [Tooltip("최초 해금 규칙 및 기본 기획 데이터를 조회하기 위해 기획 시트 에셋을 연결합니다.")]
    [SerializeField] private FacilityDatabaseSO facilityDatabase;

    [Header("현재 시설 정보")]
    public FacilityType currentFacility;

    [Header("퇴장 설정")]
    [Tooltip("상점에서 다시 광장으로 나올 때 플레이어가 바라보고 이동할 방향 벡터입니다.")]
    public Vector2Int exitDirection;

    [Tooltip("현재 상점에서 나가는 중인지 확인하는 플래그입니다.")]
    public bool isReturning;

    [Header("해금 설정")]
    [Tooltip("모든 시설의 해금 정보를 관리하는 리스트입니다. 인스펙터에서 시설별로 추가하거나 자동 생성할 수 있습니다.")]
    public List<FacilityUnlockInfo> facilityList = new List<FacilityUnlockInfo>();

    public int CurrentIndex => (int)currentFacility;

    private void OnEnable()
    {
        ValidateAndPopulateList();
    }

    private void OnValidate()
    {
        ValidateAndPopulateList();
    }

    private void ValidateAndPopulateList()
    {
        if (facilityList == null)
        {
            facilityList = new List<FacilityUnlockInfo>();
        }

        Dictionary<FacilityType, FacilityUnlockInfo> existingData = new Dictionary<FacilityType, FacilityUnlockInfo>();
        foreach (var info in facilityList)
        {
            if (!existingData.ContainsKey(info.facilityType))
            {
                existingData[info.facilityType] = info;
            }
        }

        facilityList.Clear();

        System.Array allTypes = System.Enum.GetValues(typeof(FacilityType));
        foreach (FacilityType type in allTypes)
        {
            bool hasDatabaseSetup = false;
            UnlockType dbUnlockType = UnlockType.ResourceRequired;

            if (facilityDatabase != null && facilityDatabase.TryGetDetails(type, out FacilityDetails details))
            {
                hasDatabaseSetup = true;
                dbUnlockType = details.initialUnlockType;
            }

            if (existingData.TryGetValue(type, out FacilityUnlockInfo existing))
            {
                if (hasDatabaseSetup)
                {
                    existing.unlockType = dbUnlockType;
                }

                if (existing.unlockType == UnlockType.InitUnlocked)
                {
                    existing.isUnlocked = true;
                    if (existing.level < 1)
                    {
                        existing.level = 1;
                    }
                }
                facilityList.Add(existing);
            }
            else
            {
                FacilityUnlockInfo newInfo = new FacilityUnlockInfo();
                newInfo.facilityType = type;

                if (hasDatabaseSetup)
                {
                    newInfo.unlockType = dbUnlockType;

                    if (dbUnlockType == UnlockType.InitUnlocked)
                    {
                        newInfo.isUnlocked = true;
                        newInfo.level = 1;
                    }
                    else
                    {
                        newInfo.isUnlocked = false;
                        newInfo.level = 0;
                    }
                }
                else
                {
                    newInfo.unlockType = UnlockType.ResourceRequired;
                    newInfo.isUnlocked = false;
                    newInfo.level = 0;
                }

                facilityList.Add(newInfo);
            }
        }
    }

    public void SetFacility(FacilityType newFacility)
    {
        currentFacility = newFacility;
    }

    /// <param name="direction">퇴장 방향 벡터</param>
    public void SetExitDirection(Vector2Int direction)
    {
        exitDirection = direction;
    }

    public bool IsFacilityUnlocked(FacilityType type)
    {
        if (facilityList == null || facilityList.Count == 0)
            return true;

        FacilityUnlockInfo info = facilityList.Find(x => x.facilityType == type);

        if (info.facilityType != type)
            return true;

        if (info.unlockType == UnlockType.InitUnlocked)
            return true;

        return info.isUnlocked;
    }

    public int GetFacilityLevel(FacilityType type)
    {
        if (facilityList == null || facilityList.Count == 0)
            return 0;

        FacilityUnlockInfo info = facilityList.Find(x => x.facilityType == type);

        if (info.facilityType != type)
            return 0;

        if (info.unlockType == UnlockType.InitUnlocked && info.level < 1)
        {
            return 1;
        }

        return info.level;
    }

    public void SetFacilityLevel(FacilityType type, int newLevel)
    {
        if (facilityList == null) return;

        for (int i = 0; i < facilityList.Count; i++)
        {
            if (facilityList[i].facilityType == type)
            {
                FacilityUnlockInfo updatedInfo = facilityList[i];

                if (updatedInfo.unlockType == UnlockType.InitUnlocked && newLevel < 1)
                {
                    newLevel = 1;
                }

                updatedInfo.level = newLevel;
                facilityList[i] = updatedInfo;
                return;
            }
        }
    }
}
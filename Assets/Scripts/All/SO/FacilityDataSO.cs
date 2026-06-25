using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 각 시설의 개별 해금 및 레벨 정보를 담는 구조체입니다.
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

    /// <summary>
    /// 에셋이 로드될 때 호출되어 누락된 시설 목록을 자동 초기화합니다.
    /// </summary>
    private void OnEnable()
    {
        ValidateAndPopulateList();
    }

    /// <summary>
    /// 에디터 인스펙터에서 값이 변경될 때 자동으로 호출되는 유니티 내장 함수입니다.
    /// </summary>
    private void OnValidate()
    {
        ValidateAndPopulateList();
    }

    /// <summary>
    /// 모든 시설 타입이 리스트에 존재하도록 자동 검증하고 생성합니다.
    /// 기존에 기입해 둔 해금 수치와 데이터베이스 시트 설정을 실시간 동기화합니다.
    /// </summary>
    private void ValidateAndPopulateList()
    {
        if (facilityList == null)
        {
            facilityList = new List<FacilityUnlockInfo>();
        }

        // 기존에 등록되어 있던 데이터를 임시 딕셔너리에 저장하여 유실 방지
        Dictionary<FacilityType, FacilityUnlockInfo> existingData = new Dictionary<FacilityType, FacilityUnlockInfo>();
        foreach (var info in facilityList)
        {
            if (!existingData.ContainsKey(info.facilityType))
            {
                existingData[info.facilityType] = info;
            }
        }

        // 리스트를 비우고 Enum 정의 순서대로 정렬하여 리빌딩
        facilityList.Clear();

        System.Array allTypes = System.Enum.GetValues(typeof(FacilityType));
        foreach (FacilityType type in allTypes)
        {
            // 데이터베이스에서 해당 시설의 원본 기획 데이터 조회 시도
            bool hasDatabaseSetup = false;
            UnlockType dbUnlockType = UnlockType.ResourceRequired;

            if (facilityDatabase != null && facilityDatabase.TryGetFacilityDetails(type, out FacilityDetails details))
            {
                hasDatabaseSetup = true;
                dbUnlockType = details.initialUnlockType;
            }

            if (existingData.TryGetValue(type, out FacilityUnlockInfo existing))
            {
                // 기존 데이터가 있는 경우: 데이터베이스 시트에 맞추어 해금 타입 최신화
                if (hasDatabaseSetup)
                {
                    existing.unlockType = dbUnlockType;
                }

                // 기획 시트 규칙상 최초 해금 타입이라면 안전장치 작동
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
                // 리스트에 없던 새로운 시설이 감지되었을 때 자동 삽입
                FacilityUnlockInfo newInfo = new FacilityUnlockInfo();
                newInfo.facilityType = type;

                if (hasDatabaseSetup)
                {
                    newInfo.unlockType = dbUnlockType;

                    // 최초 해금 건물(InitUnlocked)은 1레벨 및 해금으로 시작, 나머지는 0레벨 잠금으로 시작
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
                    // 데이터베이스가 연결되어 있지 않은 예외 상황 대비 방어 코드
                    newInfo.unlockType = UnlockType.ResourceRequired;
                    newInfo.isUnlocked = false;
                    newInfo.level = 0;
                }

                facilityList.Add(newInfo);
            }
        }
    }

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

        if (info.facilityType != type)
            return true;

        if (info.unlockType == UnlockType.InitUnlocked)
            return true;

        return info.isUnlocked;
    }

    /// <summary>
    /// 특정 시설의 현재 레벨을 안전하게 조회합니다.
    /// </summary>
    public int GetFacilityLevel(FacilityType type)
    {
        if (facilityList == null || facilityList.Count == 0)
            return 0;

        FacilityUnlockInfo info = facilityList.Find(x => x.facilityType == type);

        if (info.facilityType != type)
            return 0;

        // 실시간 보정: InitUnlocked 타입의 건물이 1레벨 미만으로 계산되는 현상을 원천 방어합니다.
        if (info.unlockType == UnlockType.InitUnlocked && info.level < 1)
        {
            return 1;
        }

        return info.level;
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
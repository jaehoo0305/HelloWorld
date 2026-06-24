using System;
using System.Collections.Generic;
using UnityEngine;

#region 1. 기본 구조체 정의 (Structs)

/// <summary>
/// 특정 시설 레벨 달성 시 해금될 세부 기능(버튼)들의 규칙을 정의하는 구조체입니다.
/// </summary>
[Serializable]
public struct FacilitySubFeature
{
    [Tooltip("해금될 기능의 직관적인 이름입니다. (예: 예금, 보험, 장비 합성)")]
    public string featureName;

    [Tooltip("이 기능이 활성화되기 위해 필요한 최소 시설 레벨입니다. (예: 2, 3)")]
    public int unlockRequiredLevel;

    [Tooltip("실제 UI 버튼 오브젝트를 찾아내고 매핑하기 위한 고유 식별 코드(ID)입니다.\n(예: BTN_BANK_DEPOSIT, BTN_BLACKSMITH_COMBINE)")]
    public string buttonIdentifier;
}

/// <summary>
/// 각 건물의 고유 정보 및 시트 데이터를 담는 구조체입니다.
/// </summary>
[Serializable]
public struct FacilityDetails
{
    [Header("기본 정보")]
    [Tooltip("상점의 고유 타입 Enum입니다.")]
    public FacilityType facilityType;

    [Tooltip("게임 내에 노출될 한국어 이름입니다. (예: 은행, 대장간)")]
    public string facilityName;

    [TextArea(3, 5)]
    [Tooltip("상점에 대한 설명 문구입니다.")]
    public string description;

    [Header("해금 설정")]
    [Tooltip("이 건물이 최초에 해금되는 방식입니다.")]
    public UnlockType initialUnlockType;

    [Header("레벨업 효과 리스트 (텍스트 툴팁용)")]
    [Tooltip("레벨별로 어떤 효과가 나타나는지 설명용 텍스트를 적습니다.\n[0]은 1레벨, [4]는 5레벨 효과입니다.")]
    [TextArea(2, 4)]
    public List<string> levelUpDescriptions;

    [Header("건물 레벨업 시 해금될 실제 기능(버튼)들")]
    [Tooltip("이 건물 내에서 특정 레벨 달성 시 실제로 활성화/비활성화할 버튼 정보들의 목록입니다.")]
    public List<FacilitySubFeature> subFeatures;
}

/// <summary>
/// 모든 건물 공통 레벨업 비용 정보를 담는 구조체입니다.
/// </summary>
[Serializable]
public struct LevelCostDetails
{
    [Tooltip("목표 레벨입니다. (예: 1, 2, 3, 4, 5)")]
    public int targetLevel;

    [Tooltip("소모되는 전기량입니다.")]
    public long requiredElectricity;

    [Tooltip("소모되는 비트코인 개수입니다.")]
    public int requiredBitcoin;

    [Tooltip("레벨업에 걸리는 시간(초)입니다. (예: 25, 300, 3600 등)")]
    public float buildTimeInSeconds;
}

#endregion

#region 2. ScriptableObjects 정의

/// <summary>
/// 8개 시설의 고유 설명 및 해금 정보를 관리하는 중앙 데이터베이스 SO입니다.
/// </summary>
[CreateAssetMenu(fileName = "FacilityDatabase", menuName = "Kingdom/Database/Facility Database")]
public class FacilityDatabaseSO : ScriptableObject
{
    [Header("시설 목록")]
    [Tooltip("시트에 작성된 8개의 상점 데이터를 인스펙터에서 차례대로 추가하세요.")]
    public List<FacilityDetails> facilities = new List<FacilityDetails>();

    /// <summary>
    /// 특정 시설의 세부 정보 데이터를 찾아 반환합니다.
    /// </summary>
    public bool TryGetFacilityDetails(FacilityType type, out FacilityDetails details)
    {
        for (int i = 0; i < facilities.Count; i++)
        {
            if (facilities[i].facilityType == type)
            {
                details = facilities[i];
                return true;
            }
        }
        details = default;
        return false;
    }
}

/// <summary>
/// 모든 건물 공통의 레벨별 비용 테이블을 저장하는 SO입니다.
/// </summary>
[CreateAssetMenu(fileName = "FacilityLevelCostTable", menuName = "Kingdom/Database/Level Cost Table")]
public class FacilityLevelCostTableSO : ScriptableObject
{
    [Header("레벨별 요구 자원표")]
    [Tooltip("Lv.1부터 Lv.5까지의 공통 요구 자원 데이터 5개를 입력해 주세요.")]
    public List<LevelCostDetails> levelCosts = new List<LevelCostDetails>();

    /// <summary>
    /// 특정 타겟 레벨의 업그레이드 비용 정보를 가져옵니다.
    /// </summary>
    public bool TryGetCostForLevel(int level, out LevelCostDetails costDetails)
    {
        for (int i = 0; i < levelCosts.Count; i++)
        {
            if (levelCosts[i].targetLevel == level)
            {
                costDetails = levelCosts[i];
                return true;
            }
        }
        costDetails = default;
        return false;
    }
}

#endregion
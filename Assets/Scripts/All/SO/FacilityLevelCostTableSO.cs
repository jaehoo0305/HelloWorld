using System;
using System.Collections.Generic;
using UnityEngine;

#region 1. 기본 구조체 정의 (Structs)

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
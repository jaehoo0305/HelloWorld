using System;
using UnityEngine;

/// <summary>
/// 레벨업 비용 세부 정보를 담는 구조체입니다.
/// </summary>
[Serializable]
public struct LevelCostDetails
{
    public int targetLevel;
    public long requiredElectricity;
    public int requiredBitcoin;
    public float buildTimeInSeconds;
}

/// <summary>
/// JSON에서 읽어올 자원 계산용 기본값 및 배수 계수 데이터 클래스입니다.
/// </summary>
[Serializable]
public class FacilityCostFormulaData
{
    public long baseElectricity = 1;
    public double electricityMultiplier = 100.0;

    public int baseBitcoin = 10;
    public double bitcoinMultiplier = 5.0;

    public float baseBuildTimeInSeconds = 25.0f;
    public double buildTimeMultiplier = 12.0;
}

/// <summary>
/// Resources/FacilityLevelCostTable.json 계수 데이터를 불러와 
/// 공식을 통해 레벨별 비용을 동적으로 계산해주는 매니저입니다.
/// </summary>
public class FacilityLevelCostDatabase : MonoBehaviour
{
    private static FacilityLevelCostDatabase _instance;

    public static FacilityLevelCostDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<FacilityLevelCostDatabase>();
#else
                _instance = FindObjectOfType<FacilityLevelCostDatabase>();
#endif
                if (_instance == null)
                {
                    GameObject go = new GameObject("FacilityLevelCostDatabase");
                    _instance = go.AddComponent<FacilityLevelCostDatabase>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private FacilityCostFormulaData _formulaData;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCostDataFromJson();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Resources/FacilityLevelCostTable.json 계수 파일 데이터를 파싱합니다.
    /// </summary>
    public void LoadCostDataFromJson()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("FacilityLevelCostTable");
        if (jsonFile != null)
        {
            _formulaData = JsonUtility.FromJson<FacilityCostFormulaData>(jsonFile.text);
            Debug.Log("[CostDatabase] JSON 비용 계수 데이터가 성공적으로 로드되었습니다.");
        }
        else
        {
            Debug.LogError("[CostDatabase] Resources/FacilityLevelCostTable.json 파일을 찾을 수 없어 기본 계수를 사용합니다.");
            _formulaData = new FacilityCostFormulaData();
        }
    }

    /// <summary>
    /// 목표 레벨(level)을 입력받아 수식을 통해 요구 자원 및 시간을 실시간 계산합니다.
    /// Formula: Base * (Multiplier ^ (level - 1))
    /// </summary>
    public bool TryGetCostForLevel(int level, out LevelCostDetails costDetails)
    {
        if (level < 1 || _formulaData == null)
        {
            costDetails = default;
            return false;
        }

        int exponent = level - 1;

        // 등비수열 공식 적용
        long electricity = (long)Math.Round(_formulaData.baseElectricity * Math.Pow(_formulaData.electricityMultiplier, exponent));
        int bitcoin = (int)Math.Round(_formulaData.baseBitcoin * Math.Pow(_formulaData.bitcoinMultiplier, exponent));
        float buildTime = (float)(_formulaData.baseBuildTimeInSeconds * Math.Pow(_formulaData.buildTimeMultiplier, exponent));

        costDetails = new LevelCostDetails
        {
            targetLevel = level,
            requiredElectricity = electricity,
            requiredBitcoin = bitcoin,
            buildTimeInSeconds = buildTime
        };

        return true;
    }
}
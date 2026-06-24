using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임의 핵심 재화인 '전기'의 생산, 발전기 레벨, 그리고 시설 업그레이드를 총괄하는 중앙 은행 매니저입니다.
/// DontDestroyOnLoad 패턴을 지원하여 씬 이동 시에도 데이터가 완벽하게 유지됩니다.
/// </summary>
public class GeneratorResourceManager : MonoBehaviour
{
    // 1. 싱글톤 인스턴스 (어디서든 GeneratorResourceManager.Instance로 접근 가능)
    public static GeneratorResourceManager Instance { get; private set; }

    [Header("발전기 기본 설정")]
    [SerializeField] private int generatorLevel = 1;
    [SerializeField] private double currentElectricity = 0.0;

    // 각 시설별 레벨을 저장하는 딕셔너리 (기본값은 1레벨)
    private Dictionary<FacilityType, int> _facilityLevels = new Dictionary<FacilityType, int>();

    // UI나 다른 시스템에서 리소스 변화를 실시간으로 감지할 수 있도록 돕는 이벤트들 (구독 전용)
    public static event Action OnElectricityChanged;
    public static event Action OnGeneratorUpgraded;
    public static event Action<FacilityType, int> OnFacilityUpgraded;

    #region Properties (외부 노출 데이터)

    public double CurrentElectricity => currentElectricity;
    public int GeneratorLevel => generatorLevel;

    /// <summary>
    /// 최종 초당 전기 생산량 (EPS - Electricity Per Second)을 반환하는 프로퍼티입니다.
    /// </summary>
    public double ElectricityPerSecond => CalculateElectricityPerSecond();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // 싱글톤 안착 및 중복 생성 방지
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFacilityLevels();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // 매 프레임 초당 생산량에 deltaTime을 곱하여 실시간 전기 충전
        if (ElectricityPerSecond > 0)
        {
            AddElectricity(ElectricityPerSecond * Time.deltaTime);
        }
    }

    #endregion

    #region Core Logic (공식 및 연산 기능)

    /// <summary>
    /// 전기를 안전하게 추가하는 내부 메서드입니다.
    /// </summary>
    private void AddElectricity(double amount)
    {
        if (amount <= 0) return;
        currentElectricity += amount;
        OnElectricityChanged?.Invoke();
    }

    /// <summary>
    /// 최종 초당 생산량(EPS)을 계산합니다: (Level * 1) * MilestoneMultiplier
    /// </summary>
    private double CalculateElectricityPerSecond()
    {
        double baseProduction = generatorLevel * 1.0;
        double multiplier = GetMilestoneMultiplier(generatorLevel);
        return baseProduction * multiplier;
    }

    /// <summary>
    /// 발전기 특정 레벨에 따른 마일스톤 배수를 계산하는 헬퍼 메서드입니다.
    /// </summary>
    public double GetMilestoneMultiplier(int level)
    {
        if (level >= 100) return 2000.0;
        if (level >= 75) return 250.0;
        if (level >= 50) return 25.0;
        if (level >= 25) return 4.0;
        if (level >= 10) return 2.0;
        return 1.0;
    }

    /// <summary>
    /// 발전기의 '다음 레벨' 업그레이드 비용을 계산합니다: Floor(10 * 1.15^(Level-1))
    /// </summary>
    public double GetGeneratorUpgradeCost()
    {
        double cost = 10.0 * Math.Pow(1.15, generatorLevel - 1);
        return Math.Floor(cost);
    }

    /// <summary>
    /// 특정 시설의 현재 레벨을 안전하게 반환합니다. (세이브 데이터가 없을 경우 기본 1레벨)
    /// </summary>
    public int GetFacilityLevel(FacilityType type)
    {
        if (!_facilityLevels.ContainsKey(type))
        {
            _facilityLevels[type] = 1;
        }
        return _facilityLevels[type];
    }

    /// <summary>
    /// 특정 시설의 '다음 레벨' 업그레이드 비용을 반환합니다. (최대 5레벨 제한)
    /// </summary>
    public double GetFacilityUpgradeCost(FacilityType type)
    {
        int currentLevel = GetFacilityLevel(type);

        switch (currentLevel)
        {
            case 1: return 100.0;           // Lv.1 -> Lv.2
            case 2: return 10000.0;         // Lv.2 -> Lv.3
            case 3: return 1000000.0;       // Lv.3 -> Lv.4
            case 4: return 100000000.0;     // Lv.4 -> Lv.5
            default: return double.MaxValue; // 이미 최대 레벨(Lv.5)인 경우 업그레이드 불가 비용 처리
        }
    }

    #endregion

    #region Public Interaction Methods (버튼 연동 및 비즈니스 로직)

    /// <summary>
    /// 발전기 업그레이드를 시도합니다. 재화가 충분하면 차감 후 성공(true)을 반환합니다.
    /// </summary>
    public bool TryUpgradeGenerator()
    {
        double cost = GetGeneratorUpgradeCost();

        if (currentElectricity >= cost)
        {
            currentElectricity -= cost;
            generatorLevel++;

            OnElectricityChanged?.Invoke();
            OnGeneratorUpgraded?.Invoke();
            Debug.Log($"[Generator] 발전기가 {generatorLevel}레벨로 업그레이드 되었습니다! 소모된 전기: {cost}");
            return true;
        }

        Debug.LogWarning("[Generator] 전기가 부족하여 발전기 업그레이드에 실패했습니다.");
        return false;
    }

    /// <summary>
    /// 특정 시설의 업그레이드를 시도합니다. 재화가 충분하면 차감 후 성공(true)을 반환합니다.
    /// </summary>
    public bool TryUpgradeFacility(FacilityType type)
    {
        int currentLevel = GetFacilityLevel(type);
        if (currentLevel >= 5)
        {
            Debug.LogWarning($"[Facility] {type}은(는) 이미 최대 레벨(5)입니다.");
            return false;
        }

        double cost = GetFacilityUpgradeCost(type);

        if (currentElectricity >= cost)
        {
            currentElectricity -= cost;
            _facilityLevels[type] = currentLevel + 1;

            OnElectricityChanged?.Invoke();
            OnFacilityUpgraded?.Invoke(type, _facilityLevels[type]);
            Debug.Log($"[Facility] {type}이(가) {_facilityLevels[type]}레벨로 업그레이드 되었습니다! 소모된 전기: {cost}");
            return true;
        }

        Debug.LogWarning($"[Facility] 전기가 부족하여 {type} 업그레이드에 실패했습니다.");
        return false;
    }

    /// <summary>
    /// 게임을 끈 시간(초) 동안 누적된 전기를 일괄 지급하는 오프라인 보상용 메서드입니다.
    /// </summary>
    public void AddOfflineElectricity(double seconds)
    {
        if (seconds <= 0) return;
        double offlineEarned = ElectricityPerSecond * seconds;
        AddElectricity(offlineEarned);
        Debug.Log($"[Offline] 오프라인 보상 지급 완료! 대기 시간: {seconds}초, 획득한 전기: {offlineEarned}");
    }

    #endregion

    #region Helpers

    private void InitializeFacilityLevels()
    {
        // 모든 상점 종류를 순회하며 초기 1레벨로 등록해 둡니다.
        foreach (FacilityType type in Enum.GetValues(typeof(FacilityType)))
        {
            if (!_facilityLevels.ContainsKey(type))
            {
                _facilityLevels[type] = 1;
            }
        }
    }

    #endregion
}
using System;
using UnityEngine;

/// <summary>
/// 게임의 핵심 재화인 전기를 관리하고 생산하며, 발전기의 레벨과 전력량 수치를 총괄하는 매니저 클래스입니다.
/// </summary>
public class GeneratorResourceManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static GeneratorResourceManager Instance { get; private set; }

    [Header("발전기 설정")]
    [SerializeField] private int generatorLevel = 1;
    [SerializeField] private double currentElectricity = 0.0;

    // 리소스 및 발전기 상태 변경 감지 이벤트
    public static event Action OnElectricityChanged;
    public static event Action OnGeneratorUpgraded;

    #region Properties
    public double CurrentElectricity => currentElectricity;
    public int GeneratorLevel => generatorLevel;
    public double ElectricityPerSecond => CalculateElectricityPerSecond();
    #endregion

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (ElectricityPerSecond > 0)
        {
            AddElectricity(ElectricityPerSecond * Time.deltaTime);
        }
    }

    /// <summary>
    /// 전력을 안전하게 추가하고 이벤트를 발생시킵니다.
    /// </summary>
    public void AddElectricity(double amount)
    {
        if (amount <= 0) return;
        currentElectricity += amount;
        OnElectricityChanged?.Invoke();
    }

    /// <summary>
    /// 전력을 차감해야 하는 외부 시스템을 위한 범용 소비 메서드입니다.
    /// 외부에서 시설 업그레이드 등을 개별 구현할 때 이 함수를 호출하여 전기를 소비할 수 있습니다.
    /// </summary>
    public bool TryConsumeElectricity(double amount)
    {
        if (amount <= 0) return false;

        if (currentElectricity >= amount)
        {
            currentElectricity -= amount;
            OnElectricityChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 최종 초당 전기 생산량(EPS)을 계산합니다.
    /// </summary>
    private double CalculateElectricityPerSecond()
    {
        double baseProduction = generatorLevel * 1.0;
        double multiplier = GetMilestoneMultiplier(generatorLevel);
        return baseProduction * multiplier;
    }

    /// <summary>
    /// 발전기 특정 레벨에 따른 마일스톤 배수를 계산하여 반환합니다.
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
    /// 발전기의 다음 레벨 업그레이드 비용을 계산합니다.
    /// </summary>
    public double GetGeneratorUpgradeCost()
    {
        double cost = 10.0 * Math.Pow(1.15, generatorLevel - 1);
        return Math.Floor(cost);
    }

    /// <summary>
    /// 발전기 자체의 업그레이드를 시도합니다.
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
            return true;
        }
        return false;
    }

    /// <summary>
    /// 오프라인 누적 시간 동안 생성된 전기를 반영합니다.
    /// </summary>
    public void AddOfflineElectricity(double seconds)
    {
        if (seconds <= 0) return;
        double offlineEarned = ElectricityPerSecond * seconds;
        AddElectricity(offlineEarned);
    }
}
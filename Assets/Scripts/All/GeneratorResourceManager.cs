using System;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 감지를 위한 네임스페이스 추가

/// <summary>
/// 게임의 핵심 재화인 전기를 관리하고 생산하며, 발전기의 레벨과 전력량 수치를 총괄하는 매니저 클래스입니다.
/// 싱글톤 패턴이 적용되어 씬이 전환되어도 파괴되지 않고 누적 연산을 지속합니다.
/// </summary>
public class GeneratorResourceManager : MonoBehaviour
{
    // --- 싱글톤 구조 업그레이드 (Lazy Initialization) ---
    public static GeneratorResourceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 현재 씬에 이미 존재하는지 탐색
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<GeneratorResourceManager>();
#else
                _instance = FindObjectOfType<GeneratorResourceManager>();
#endif
                // 존재하지 않는다면 자동으로 생성하여 방치씬 등에서 단독 실행 시 오류를 철저히 방지합니다.
                if (_instance == null)
                {
                    GameObject go = new GameObject("GeneratorResourceManager");
                    _instance = go.AddComponent<GeneratorResourceManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    private static GeneratorResourceManager _instance;

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
        // 중복 생성 방지 및 씬 전환 시 파괴 방지 설정
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 데이터를 기기 저장소로부터 안전하게 불러옵니다.
        // 나중에 보안 요소 필요
        LoadData();
    }

    private void Update()
    {
        // 킹덤 광장 씬("KingdomScene")에 머물고 있을 때는 전력 생산을 차단합니다.
        if (SceneManager.GetActiveScene().name == "KingdomUIScene")
        {
            return;
        }

        // 매 프레임 초당 생산 속도(EPS) 비례 전기를 안전하게 적립합니다.
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
    /// </summary>
    public bool TryConsumeElectricity(double amount)
    {
        if (amount <= 0) return false;

        if (currentElectricity >= amount)
        {
            currentElectricity -= amount;
            OnElectricityChanged?.Invoke();
            SaveData(); // 재화 소모 발생 시 즉시 저장
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
            SaveData(); // 레벨업 성공 시 즉시 저장
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

    // --- PlayerPrefs 영구 저장 및 오프라인 생산 시스템 ---

    /// <summary>
    /// 현재 누적된 전력 및 레벨, 그리고 종료 시간을 로컬 기기에 저장합니다.
    /// </summary>
    public void SaveData()
    {
        PlayerPrefs.SetInt("GeneratorLevel", generatorLevel);
        PlayerPrefs.SetString("CurrentElectricity", currentElectricity.ToString());
        PlayerPrefs.SetString("LastQuitTime", DateTime.UtcNow.ToBinary().ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 이전 누적 데이터와 오프라인 방치 보상을 자동으로 연산하여 적용합니다.
    /// </summary>
    public void LoadData()
    {
        //함수 위치만 남기고 나중에 개발한다.
    }

    // 포커스를 일시적으로 잃거나 게임을 끌 때 데이터를 항상 동기화 저장합니다.
    private void OnApplicationQuit()
    {
        SaveData();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveData();
        }
    }
}
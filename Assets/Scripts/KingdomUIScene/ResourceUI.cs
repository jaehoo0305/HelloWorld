using UnityEngine;
using TMPro;

/// <summary>
/// GeneratorResourceManager의 데이터를 실시간으로 읽어와 TMP UI에 반영하는 뷰(View) 스크립트입니다.
/// 가로 화면(MainUICanvas)과 월드 화면(PowerStationUICanvas)에 각각 부착하여 개별 설정이 가능합니다.
/// </summary>
public class ResourceUI : MonoBehaviour
{
    [Header("전력 표시 (MainUICanvas용)")]
    [Tooltip("현재 보유 중인 총 전력량을 표시할 텍스트 컴포넌트입니다. (CurrentEle)")]
    [SerializeField] private TMP_Text currentElectricityText;

    [Tooltip("초당 전력 생산 속도(EPS)를 표시할 텍스트 컴포넌트입니다. (ResourceEle)")]
    [SerializeField] private TMP_Text electricityPerSecondText;

    [Header("레벨 및 비트코인 표시 (PowerStationUICanvas / MainUICanvas용)")]
    [Tooltip("발전기의 현재 레벨을 표시할 텍스트 컴포넌트입니다. (ResourceBit - 발전소 내부용)")]
    [SerializeField] private TMP_Text generatorLevelText;

    [Tooltip("현재 보유 중인 비트코인 갯수를 표시할 텍스트 컴포넌트입니다. (ResourceBit - 메인 UI용)")]
    [SerializeField] private TMP_Text bitcoinText;

    private void Update()
    {
        // 중앙 매니저 싱글톤이 존재할 때만 데이터 연동 실행
        if (GeneratorResourceManager.Instance == null) return;

        UpdateElectricityUI();
        UpdateGeneratorLevelUI();
        UpdateBitcoinUI();
    }

    /// <summary>
    /// 현재 전력량 및 초당 생산 속도(EPS) UI를 갱신합니다.
    /// </summary>
    private void UpdateElectricityUI()
    {
        // 1. 현재 전력량 업데이트 (CurrentEle)
        if (currentElectricityText != null)
        {
            double currentEle = GeneratorResourceManager.Instance.CurrentElectricity;
            currentElectricityText.text = ((long)currentEle).ToString("F0");
        }

        // 2. 초당 생산량 업데이트 (ResourceEle)
        if (electricityPerSecondText != null)
        {
            double eps = GeneratorResourceManager.Instance.ElectricityPerSecond;
            // 가독성을 위해 생산 속도는 앞에 + 기호와 뒤에 /s 단위 표시
            electricityPerSecondText.text = $"+{FormatNumber(eps)}/s";
        }
    }

    /// <summary>
    /// 발전기 레벨 UI를 갱신합니다. (PowerStationUICanvas의 ResourceBit에 매핑 가능)
    /// </summary>
    private void UpdateGeneratorLevelUI()
    {
        if (generatorLevelText != null)
        {
            int level = GeneratorResourceManager.Instance.GeneratorLevel;
            generatorLevelText.text = $"Lv. {level}";
        }
    }

    /// <summary>
    /// 비트코인 UI를 갱신합니다. (현재 매니저에 비트코인 재화가 추가되기 전이므로 임시 0개 표시 또는 추후 확장용)
    /// </summary>
    private void UpdateBitcoinUI()
    {
        if (bitcoinText != null)
        {
            // 현재는 0으로 표기하며, 추후 비트코인 전역 데이터 추가 시 이곳만 연결하면 됩니다.
            bitcoinText.text = "0";
        }
    }

    /// <summary>
    /// 방치형 게임 특성상 숫자가 엄청나게 커질 수 있으므로, K, M, B 단위를 적용하여 가독성을 높입니다.
    /// </summary>
    private string FormatNumber(double value)
    {
        if (value >= 1000000000.0) // 10억 이상 (Billion)
        {
            return (value / 1000000000.0).ToString("F2") + "B";
        }
        if (value >= 1000000.0) // 100만 이상 (Million)
        {
            return (value / 1000000.0).ToString("F2") + "M";
        }
        if (value >= 1000.0) // 1000 이상 (Kilo)
        {
            return (value / 1000.0).ToString("F1") + "K";
        }

        // 1000 미만인 경우 소수점 첫째 자리까지만 깔끔하게 정수/실수 표시
        return (value).ToString(("F1"));
    }
}
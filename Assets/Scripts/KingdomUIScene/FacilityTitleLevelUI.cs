using UnityEngine;
using TMPro;

/// <summary>
/// 상단 UI 패널에 현재 활성화된 시설의 이름과 레벨을 실시간 매핑하여 보여주는 뷰 스크립트입니다.
/// </summary>
public class FacilityTitleLevelUI : MonoBehaviour
{
    [Header("중앙 데이터베이스 연결")]
    [Tooltip("상점의 한글 이름 데이터를 가져올 구글 시트 역할의 SO입니다.")]
    [SerializeField] private FacilityDatabaseSO facilityDatabase;

    [Tooltip("현재 활성화된 상점 및 실시간 데이터를 보관하는 SO입니다.")]
    [SerializeField] private FacilityDataSO facilityDataState;

    [Header("UI 컴포넌트 연결")]
    [Tooltip("시설 이름이 노출될 텍스트입니다. (예: 회관, 은행)")]
    [SerializeField] private TMP_Text facilityNameText;

    [Tooltip("시설 레벨이 노출될 텍스트입니다. (예: Lv. 1, 0 Lv)")]
    [SerializeField] private TMP_Text facilityLevelText;

    private void Update()
    {
        if (facilityDataState == null) return;

        // 1. 현재 활성화된 시설 타입을 가져옵니다.
        FacilityType currentType = facilityDataState.currentFacility;

        // 2. 실시간 정보 갱신을 위해 매 프레임 정보를 업데이트합니다.
        UpdateFacilityInfo(currentType);
    }

    /// <summary>
    /// 현재 활성화된 상점의 정보를 수집하여 UI를 업데이트합니다.
    /// </summary>
    private void UpdateFacilityInfo(FacilityType type)
    {
        // [A] 시설 이름 불러오기 (FacilityDatabaseSO 연동)
        string displayName = type.ToString(); // 데이터가 없을 때를 대비한 예외 처리 (Enum 영문명)
        if (facilityDatabase != null && facilityDatabase.TryGetFacilityDetails(type, out FacilityDetails details))
        {
            displayName = details.facilityName;
        }

        if (facilityNameText != null)
        {
            facilityNameText.text = displayName;
        }

        // [B] 시설 레벨 불러오기 (실시간 데이터 보관함인 FacilityDataSO 연동)
        int currentLevel = 0;
        if (facilityDataState != null)
        {
            currentLevel = facilityDataState.GetFacilityLevel(type);
        }

        // [C] UI 최종 출력 조율
        if (facilityLevelText != null)
        {
            facilityLevelText.text = $", Lv. {currentLevel}";
        }
    }
}
using UnityEngine;
using TMPro;

/// <summary>
/// 상단 UI 패널에 시설 이름과 레벨을 전달받아 단순 표시하는 수동적 뷰(Passive View) 스크립트입니다.
/// Model(FacilityDataSO)을 직접 참조하지 않으며, Presenter(FacilityManager)의 지시에 따라서만 화면을 갱신합니다.
/// </summary>
public class FacilityTitleLevelUI : MonoBehaviour
{
    [Header("UI 컴포넌트 연결")]
    [Tooltip("시설 이름이 노출될 텍스트입니다. (예: 회관, 은행)")]
    [SerializeField] private TMP_Text facilityNameText;

    [Tooltip("시설 레벨이 노출될 텍스트입니다. (예: Lv. 1, 0 Lv)")]
    [SerializeField] private TMP_Text facilityLevelText;

    /// <summary>
    /// Presenter(FacilityManager)로부터 전달받은 시설 이름과 레벨 정보를 화면에 출력합니다.
    /// </summary>
    /// <param name="displayName">시설 한글 표시 이름</param>
    /// <param name="level">시설 레벨</param>
    public void SetFacilityTitleAndLevel(string displayName, int level)
    {
        if (facilityNameText != null)
        {
            facilityNameText.text = displayName;
        }

        if (facilityLevelText != null)
        {
            facilityLevelText.text = $", Lv. {level}";
        }
    }
}
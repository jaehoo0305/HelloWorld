using UnityEngine;
using TMPro;

public class FacilityTitleLevelUI : MonoBehaviour
{
    [Header("UI 컴포넌트 연결")]
    [Tooltip("시설 레벨이 노출될 텍스트입니다. (예: Lv. 1, 0 Lv)")]
    [SerializeField] private TMP_Text facilityLevelText;

    public void SetFacilityLevel(int level)
    {
        if (facilityLevelText != null)
        {
            facilityLevelText.text = $", Lv. {level}";
        }
    }
}
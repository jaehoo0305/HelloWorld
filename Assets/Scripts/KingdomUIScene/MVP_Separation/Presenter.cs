using UnityEngine;

public class Presenter : MonoBehaviour
{
    [Header("Model (데이터 에셋들)")]
    [SerializeField] private FacilityDataSO facilityData;
    [SerializeField] private FacilityDatabaseSO facilityDatabase;

    [Header("View (화면 UI)")]
    [SerializeField] private View nameView;

    private FacilityType lastFacilityType;

    private void Start()
    {
        if (facilityData != null) 
        {
            lastFacilityType = facilityData.currentFacility;
        }
        UpdateNameDisplay();
    }

    private void Update()
    {
        if (facilityData != null && facilityData.currentFacility != lastFacilityType)
        {
            lastFacilityType = facilityData.currentFacility;
            UpdateNameDisplay();
        }
    }

    public void UpdateNameDisplay()
    {
        if (facilityData == null || facilityDatabase == null || nameView == null)
        {
            return;
        }

        FacilityType currentType = facilityData.currentFacility;

        string displayName = currentType.ToString();
        if (facilityDatabase.TryGetFacilityDetails(currentType, out FacilityDetails details))
        {
            displayName = details.facilityName;
        }

        nameView.SetFacilityName(displayName);
    }
}

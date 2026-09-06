using UnityEngine;

public class Presenter : MonoBehaviour
{
    [SerializeField] private FacilityDataSO facilityData;
    [SerializeField] private FacilityDatabaseSO facilityDatabase;

    [SerializeField] private View view;

    private FacilityType lastFacilityType;

    private void Start()
    {
        lastFacilityType = facilityData.currentFacility;
        RefreshDispaly();
    }

    private void Update()
    {
        if (lastFacilityType != facilityData.currentFacility)
        {
            lastFacilityType = facilityData.currentFacility;
            RefreshDispaly();
        }
    }

    public void RefreshDispaly()
    {
        FacilityType currentType = facilityData.currentFacility;

        string displayName = currentType.ToString();

        if (facilityDatabase.TryGetDetails(currentType, out FacilityDetails details))
        {
            displayName = details.facilityName;
        }

        view.SetDisplay(displayName);
    }
}

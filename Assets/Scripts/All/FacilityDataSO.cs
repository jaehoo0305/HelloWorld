using UnityEngine;

[CreateAssetMenu(fileName = "FacilityData", menuName = "Kingdom/FacilityData")]
public class FacilityDataSO : ScriptableObject
{
    public FacilityType currentFacility;

    public int CurrentIndex => (int)currentFacility;

    public void SetFacility(FacilityType newFacility)
    {
        currentFacility = newFacility;
    }
}
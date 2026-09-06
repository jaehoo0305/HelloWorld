using System;
using System.Collections.Generic;
using UnityEngine;

#region 1. 기본 구조체 정의 (Structs)

[Serializable]
public struct FacilitySubFeature
{
    [Tooltip("Next Unlock")]
    public string featureName;

    [Tooltip("Required Facility Level")]
    public int unlockRequiredLevel;

    [Tooltip("Unique Code")]
    public string buttonIdentifier;
}

[Serializable]
public struct FacilityDetails
{
    [Header("Basic Info")]
    [Tooltip("Unique Facility Type Enum")]
    public FacilityType facilityType;

    [Tooltip("In-game Display Name")]
    public string facilityName;

    [TextArea(3, 5)]
    [Tooltip("Shop Description")]
    public string description;

    [Header("Unlock Settings")]
    [Tooltip("Initial Unlock Condition/Method")]
    public UnlockType initialUnlockType;

    [Header("Level-up Effects (Tooltip Text)")]
    [Tooltip("Per-level effect descriptions")]
    [TextArea(2, 4)]
    public List<string> levelUpDescriptions;

    [Header("Unlocked Features/Buttons")]
    [Tooltip("Buttons toggled upon reaching specific levels")]
    public List<FacilitySubFeature> subFeatures;
}

#endregion

[CreateAssetMenu(fileName = "FacilityDatabase", menuName = "Kingdom/Database/Facility Database")]
public class FacilityDatabaseSO : ScriptableObject
{
    public List<FacilityDetails> facilities = new List<FacilityDetails>();

    public bool TryGetDetails(FacilityType type, out FacilityDetails details)
    {
        for (int i = 0; i < facilities.Count; i++)
        {
            if (facilities[i].facilityType == type)
            {
                details = facilities[i];
                return true;
            }
        }

        details = default;
        return false;
    }
}
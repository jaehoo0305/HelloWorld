using UnityEngine;
using TMPro;

public class View : MonoBehaviour
{
    [Header("UI Component")]
    [SerializeField] private TMP_Text facilityNameText;

    public void SetFacilityName(string displayName)
    {
        if (facilityNameText != null)
        {
            facilityNameText.text = displayName;
        }
    }
}
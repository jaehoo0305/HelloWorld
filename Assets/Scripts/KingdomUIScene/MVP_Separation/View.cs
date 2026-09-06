using UnityEngine;
using TMPro;

public class View : MonoBehaviour
{
    [SerializeField] private TMP_Text curText;

    public void SetDisplay(string displayName)
    {
        curText.text = displayName;
    }
}
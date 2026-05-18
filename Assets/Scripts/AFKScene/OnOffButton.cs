using UnityEngine;
using UnityEngine.UI;

public class OnOffButton : MonoBehaviour
{
    [Header("UI Component")]
    public Button toggleButton;
    public Image buttonImage;

    [Header("Sprites by State")]
    public Sprite onSprite;
    public Sprite offSprite;

    private bool isOn = true;

    void Start()
    {
        if (toggleButton == null) toggleButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        UpdateVisual();

        toggleButton.onClick.AddListener(ChangeState);
    }

    void ChangeState()
    {
        isOn = !isOn;

        UpdateVisual();
        ExecuteLogic();
    }

    void UpdateVisual()
    {
        buttonImage.sprite = isOn ? onSprite : offSprite;
    }

    void ExecuteLogic()
    {
        if (isOn)
        {
            Debug.Log("Onn");
        }
        else
        {
            Debug.Log("Off");
        }
    }
}
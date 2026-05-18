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

    [Header("Save Settings")]
    [Tooltip("각 버튼을 구분할 고유한 저장 키 이름")]
    public string saveKey = "Default_OnOff_Key";

    [Tooltip("처음 게임을 실행했을 때의 기본 상태값 설정")]
    public bool defaultState = true;

    private bool isOn = true;

    void Start()
    {
        if (toggleButton == null) toggleButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // 1. 시작 시 PlayerPrefs에서 저장된 값 불러오기 (저장된 값이 없다면 defaultState 값 사용)
        LoadState();

        // 2. 불러온 상태에 맞춰 UI 비주얼 갱신
        UpdateVisual();

        // 3. 불러온 상태에 따른 초기 로직 실행 (예: 시작하자마자 항상 위 옵션 등을 강제 적용)
        ExecuteLogic();

        toggleButton.onClick.AddListener(ChangeState);
    }

    void ChangeState()
    {
        isOn = !isOn;

        // 4. 상태 변경 시 즉시 PlayerPrefs에 저장
        SaveState();

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
            Debug.Log($"{saveKey} : Onn");
        }
        else
        {
            Debug.Log($"{saveKey} : Off");
        }
    }

    /// <summary>
    /// PlayerPrefs를 통해 현재 상태를 기기에 저장합니다.
    /// </summary>
    private void SaveState()
    {
        // bool 값을 정수(1 또는 0)로 변환하여 저장합니다.
        PlayerPrefs.SetInt(saveKey, isOn ? 1 : 0);
        PlayerPrefs.Save(); // 디스크에 물리적으로 데이터 반영
    }

    /// <summary>
    /// PlayerPrefs로부터 이전 상태를 불러옵니다.
    /// </summary>
    private void LoadState()
    {
        int defaultValue = defaultState ? 1 : 0;
        int savedValue = PlayerPrefs.GetInt(saveKey, defaultValue);

        isOn = (savedValue == 1);
    }
}
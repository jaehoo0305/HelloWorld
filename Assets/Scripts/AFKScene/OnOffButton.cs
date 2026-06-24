using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events; // 이벤트 바인딩을 위한 네임스페이스 추가

public class OnOffButton : MonoBehaviour
{
    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { } // 인스펙터 노출용 커스텀 불리언 이벤트

    [Header("UI Component")]
    public Button toggleButton;
    public Image buttonImage;

    [Header("Sprites by State")]
    public Sprite onSprite;
    public Sprite offSprite;

    [Header("Save Settings")]
    [Tooltip("각 버튼의 On/Off 데이터를 개별 분리하여 저장할 레지스트리 키")]
    public string saveKey = "Default_OnOff_Key";

    [Tooltip("처음 게임을 실행했을 때 적용될 기본 값 (켜짐/꺼짐 상태)")]
    public bool defaultState = true;

    [Header("Event Triggers")]
    [Tooltip("버튼의 On/Off 값이 바뀔 때 연동되어 실행될 기능들을 인스펙터에서 등록하세요.")]
    public BoolEvent onValueChanged;

    private bool isOn = true;

    void Start()
    {
        if (toggleButton == null) toggleButton = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // 1. 시작 시 PlayerPrefs에서 저장된 값 로드
        LoadState();

        // 2. 불러온 값에 맞추어 UI 비주얼 스피드 교체
        UpdateVisual();

        // 3. 불러온 직후 최초 1회 이벤트를 발송하여 윈도우 상태 강제 동기화
        ExecuteLogic();

        toggleButton.onClick.AddListener(ChangeState);
    }

    void ChangeState()
    {
        isOn = !isOn;

        // 4. 상태 변경 즉시 PlayerPrefs에 영구 저장
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
            Debug.Log($"[OnOffButton] {saveKey} : Enabled (ON)");
        }
        else
        {
            Debug.Log($"[OnOffButton] {saveKey} : Disabled (OFF)");
        }

        // 인스펙터 상에서 연결한 스크립트의 Dynamic bool 함수로 상태값 전달
        onValueChanged?.Invoke(isOn);
    }

    private void SaveState()
    {
        PlayerPrefs.SetInt(saveKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        int defaultValue = defaultState ? 1 : 0;
        int savedValue = PlayerPrefs.GetInt(saveKey, defaultValue);

        isOn = (savedValue == 1);
    }
}
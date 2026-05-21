using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 중앙 사령탑의 데이터를 바탕으로 아이콘 색상을 변경하고, 
/// 입력 컨트롤러의 상태를 받아 화살표 투명도를 조절합니다.
/// </summary>
public class FacilityVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FacilityManager manager;

    [Header("Facility Icon Settings")]
    [SerializeField] private Image[] facilityIcons;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    [SerializeField] private float visualTransitionSpeed = 15f;

    [Header("Arrow UI Settings")]
    [SerializeField] private Image leftArrow;
    [SerializeField] private Image rightArrow;
    [SerializeField] private Image downArrow;
    [SerializeField] private float arrowShowDuration = 2.0f;
    [SerializeField] private float arrowFadeSpeed = 5.0f;

    private float _leftArrowTimer = 0f;
    private float _rightArrowTimer = 0f;

    private bool _isIdle = false;
    private bool _isSPressed = false;

    private void Start()
    {
        SetImageAlpha(leftArrow, 0f);
        SetImageAlpha(rightArrow, 0f);
        SetImageAlpha(downArrow, 0f);

        if (manager != null && facilityIcons != null)
        {
            for (int i = 0; i < facilityIcons.Length; i++)
            {
                if (facilityIcons[i] == null) continue;
                facilityIcons[i].color = (i == manager.CurrentActiveIndex) ? activeColor : inactiveColor;
            }
        }
    }

    private void Update()
    {
        if (manager == null) return;

        UpdateArrowVisuals();
        UpdateIconColorsSmoothly();
    }

    // --- InputController에서 호출하는 함수들 --- //

    public void TriggerArrowLeft()
    {
        _leftArrowTimer = arrowShowDuration;
        _rightArrowTimer = 0f;
    }

    public void TriggerArrowRight()
    {
        _rightArrowTimer = arrowShowDuration;
        _leftArrowTimer = 0f;
    }

    public void SetSPressed(bool isPressed)
    {
        _isSPressed = isPressed;
    }

    public void SetIdleState(bool isIdle)
    {
        _isIdle = isIdle;
    }

    // --- 내부 시각 효과 처리 로직 --- //

    private void UpdateArrowVisuals()
    {
        if (_leftArrowTimer > 0) _leftArrowTimer -= Time.deltaTime;
        if (_rightArrowTimer > 0) _rightArrowTimer -= Time.deltaTime;

        bool hasActiveSpecificInput = (_leftArrowTimer > 0 || _rightArrowTimer > 0 || _isSPressed);

        float leftTarget = (_leftArrowTimer > 0 || (_isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;
        float rightTarget = (_rightArrowTimer > 0 || (_isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;
        float downTarget = (_isSPressed || (_isIdle && !hasActiveSpecificInput)) ? 1.0f : 0.0f;

        LerpAlpha(leftArrow, leftTarget);
        LerpAlpha(rightArrow, rightTarget);
        LerpAlpha(downArrow, downTarget);
    }

    private void UpdateIconColorsSmoothly()
    {
        if (facilityIcons == null) return;

        for (int i = 0; i < facilityIcons.Length; i++)
        {
            if (facilityIcons[i] == null) continue;
            Color targetColor = (i == manager.CurrentActiveIndex) ? activeColor : inactiveColor;
            facilityIcons[i].color = Color.Lerp(facilityIcons[i].color, targetColor, Time.deltaTime * visualTransitionSpeed);
        }
    }

    private void LerpAlpha(Image img, float targetAlpha)
    {
        if (img == null) return;
        Color color = img.color;
        color.a = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * arrowFadeSpeed);
        img.color = color;
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color color = img.color;
        color.a = alpha;
        img.color = color;
    }
}
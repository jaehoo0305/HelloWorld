using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 유저의 키보드/마우스 입력을 감지하여 매니저와 시각 컨트롤러에 전달합니다.
/// </summary>
public class FacilityInputController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FacilityManager manager;
    [SerializeField] private FacilityVisualController visualController;

    [Header("Settings")]
    [SerializeField] private float inputCooldown = 0.5f;
    [SerializeField] private float sHoldExitTime = 1.5f;
    [SerializeField] private float idleTimeout = 5.0f;

    private float _lastInputTime = -10f;
    private float _sHoldTimer = 0f;
    private float _idleTimer = 0f;

    private Vector2 _lastMousePos;
    private const float MouseMoveThreshold = 0.1f;

    private void Start()
    {
        if (Mouse.current != null) _lastMousePos = Mouse.current.position.ReadValue();
    }

    private void Update()
    {
        if (Keyboard.current == null || manager == null || visualController == null) return;

        HandleNavigation();
        HandleExit();
        UpdateIdleDetection();
    }

    private void HandleNavigation()
    {
        if (Time.time < _lastInputTime + inputCooldown) return;

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            manager.MoveIndex(-1);
            visualController.TriggerArrowLeft();
            _lastInputTime = Time.time;
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            manager.MoveIndex(1);
            visualController.TriggerArrowRight();
            _lastInputTime = Time.time;
        }
    }

    private void HandleExit()
    {
        bool isSPressed = Keyboard.current.sKey.isPressed;
        visualController.SetSPressed(isSPressed); // S키 누름 상태를 시각 컨트롤러에 전달

        if (isSPressed)
        {
            _sHoldTimer += Time.deltaTime;
            if (_sHoldTimer >= sHoldExitTime)
            {
                manager.ExitToKingdomScene();
            }
        }
        else
        {
            _sHoldTimer = 0f;
        }
    }

    private void UpdateIdleDetection()
    {
        bool isActivityDetected = false;

        // A, S, D 키를 제외한 키보드 활동 감지
        if (Keyboard.current.anyKey.wasPressedThisFrame)
        {
            if (!Keyboard.current.aKey.wasPressedThisFrame &&
                !Keyboard.current.sKey.wasPressedThisFrame &&
                !Keyboard.current.dKey.wasPressedThisFrame)
            {
                isActivityDetected = true;
            }
        }

        // 마우스 활동 감지
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                isActivityDetected = true;
            }

            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            if (Vector2.Distance(currentMousePos, _lastMousePos) > MouseMoveThreshold)
            {
                _lastMousePos = currentMousePos;
                isActivityDetected = true;
            }
        }

        // 타이머 갱신
        if (isActivityDetected) _idleTimer = 0f;
        else _idleTimer += Time.deltaTime;

        // 유휴 상태 여부를 시각 컨트롤러에 전달
        visualController.SetIdleState(_idleTimer >= idleTimeout);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 특정 시설 내의 세부 메뉴(월드 캔버스)를 클릭 시 비율에 맞게 줌인/줌아웃하고
/// 씬을 넘나들어도 그 상태를 정적으로 기억하는 컨트롤러입니다.
/// </summary>
public class FacilitySubMenuZoom : MonoBehaviour
{
    [System.Serializable]
    public class SubMenu
    {
        public string menuName;
        public Button triggerButton;
        public RectTransform targetCanvas;
        [Tooltip("이 메뉴로 줌인할 때 임시로 숨길 다른 UI 요소들")]
        public GameObject[] elementsToHide;
    }

    [Header("Manager References")]
    [SerializeField] private FacilityManager manager;
    [SerializeField] private FacilityCameraController cameraController;
    [SerializeField] private Transform mainCameraTransform;

    [Header("Facility Settings")]
    [SerializeField] private FacilityType facilityType;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomDuration = 0.5f;
    [SerializeField] private List<SubMenu> subMenus;

    private SubMenu _currentZoomedMenu = null;
    private bool _isMyFacilityActive = false;
    private Coroutine _movementCoroutine;

    private const float BaseCanvasWidth = 10.25f;
    private const float BaseCanvasHeight = 5.75f;
    private const float BaseCameraDistance = 5.0f;

    // 씬이 전환되어도 파괴되지 않는 전역(Static) 기억장치
    private static Dictionary<FacilityType, string> _savedZoomStates = new Dictionary<FacilityType, string>();

    /// <summary>
    /// 외부(씬 이동 버튼 등)에서 씬 진입 전에 미리 특정 메뉴 줌인 상태를 예약할 수 있습니다.
    /// </summary>
    public static void SetPreloadedZoomState(FacilityType type, string menuName)
    {
        _savedZoomStates[type] = menuName;
    }

    private void Start()
    {
        if (manager == null || cameraController == null || mainCameraTransform == null)
        {
            Debug.LogError("[FacilitySubMenuZoom] 필수 참조가 누락되었습니다.");
            return;
        }

        foreach (var menu in subMenus)
        {
            if (menu.triggerButton != null)
            {
                menu.triggerButton.onClick.AddListener(() => OnSubMenuClicked(menu));
            }
        }

        // 씬 로드 시, 정적 기억장치에 이 시설의 줌인 기록이 있다면 복구 준비
        if (_savedZoomStates.TryGetValue(facilityType, out string savedMenuName))
        {
            _currentZoomedMenu = subMenus.Find(m => m.menuName == savedMenuName);
            if (_currentZoomedMenu != null)
            {
                foreach (var go in _currentZoomedMenu.elementsToHide)
                {
                    if (go != null) go.SetActive(false);
                }
            }
        }
    }

    private void Update()
    {
        if (manager == null) return;

        bool isActiveNow = (manager.CurrentActiveIndex == (int)facilityType);

        if (isActiveNow && !_isMyFacilityActive)
        {
            OnFacilityEntered();
        }
        else if (!isActiveNow && _isMyFacilityActive)
        {
            OnFacilityExited();
        }

        _isMyFacilityActive = isActiveNow;

        // 줌인 상태일 때 S키 클릭으로 줌아웃
        if (_isMyFacilityActive && _currentZoomedMenu != null)
        {
            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            {
                ExecuteZoomOut();
            }
        }
    }

    private void OnFacilityEntered()
    {
        // 씬 전환 또는 다른 상점에서 돌아왔을 때 상태 복구
        if (_currentZoomedMenu != null)
        {
            // [해결] 이 시설이 줌인 상태라면 궤도 컨트롤러를 확실하게 끕니다.
            cameraController.enabled = false;
            StartCameraMovement(GetZoomTargetPosition(_currentZoomedMenu.targetCanvas), GetZoomTargetRotation(_currentZoomedMenu.targetCanvas));
            Debug.Log($"[FacilitySubMenuZoom] 전역 기억 복구 완료: {_currentZoomedMenu.menuName}");
        }
        else
        {
            // [해결] 이 시설이 메인 뷰 상태라면 궤도 컨트롤러를 켭니다. 
            // 입장하는 녀석이 직접 켜주기 때문에 경쟁 상태가 사라집니다.
            cameraController.enabled = true;
        }
    }

    private void OnFacilityExited()
    {
        // [해결] 퇴장할 때는 제어권(cameraController.enabled)에 절대 손대지 않습니다.
        // 다음으로 입장하는 상점이 자신의 상태(줌인/메인뷰)에 맞춰서 알아서 세팅할 것입니다.
        if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
    }

    private void OnSubMenuClicked(SubMenu menu)
    {
        if (_currentZoomedMenu == menu) return;

        _currentZoomedMenu = menu;
        _savedZoomStates[facilityType] = menu.menuName;

        foreach (var go in menu.elementsToHide)
        {
            if (go != null) go.SetActive(false);
        }

        cameraController.enabled = false;
        StartCameraMovement(GetZoomTargetPosition(menu.targetCanvas), GetZoomTargetRotation(menu.targetCanvas));
    }

    private void ExecuteZoomOut()
    {
        if (_currentZoomedMenu == null) return;

        _savedZoomStates.Remove(facilityType);

        foreach (var go in _currentZoomedMenu.elementsToHide)
        {
            if (go != null) go.SetActive(true);
        }

        _currentZoomedMenu = null;

        CalculateOrbitTransform(out Vector3 orbitPos, out Quaternion orbitRot);

        StartCameraMovement(orbitPos, orbitRot, onComplete: () =>
        {
            // 줌아웃 이동이 끝난 후, 여전히 현재 활성화된 시설일 때만 궤도 컨트롤러를 켭니다.
            if (_isMyFacilityActive && _currentZoomedMenu == null)
            {
                cameraController.enabled = true;
            }
        });
    }

    private Vector3 GetZoomTargetPosition(RectTransform canvasRect)
    {
        float canvasWorldWidth = canvasRect.rect.width * canvasRect.lossyScale.x;
        float distanceRatio = canvasWorldWidth / BaseCanvasWidth;
        float targetDistance = BaseCameraDistance * distanceRatio;

        return canvasRect.position - (canvasRect.forward * targetDistance);
    }

    private Quaternion GetZoomTargetRotation(RectTransform canvasRect)
    {
        return Quaternion.LookRotation(canvasRect.forward, canvasRect.up);
    }

    private void CalculateOrbitTransform(out Vector3 pos, out Quaternion rot)
    {
        float angle = (int)facilityType * (360f / FacilityManager.TotalFacilityCount);
        float rad = angle * Mathf.Deg2Rad;

        float x = Mathf.Sin(rad) * cameraController.CameraRadius;
        float z = Mathf.Cos(rad) * cameraController.CameraRadius;

        if (Mathf.Abs(x) < 0.001f) x = 0f;
        if (Mathf.Abs(z) < 0.001f) z = 0f;

        pos = new Vector3(x, 0, z);
        rot = Quaternion.Euler(0, angle, 0);
    }

    private void StartCameraMovement(Vector3 targetPos, Quaternion targetRot, System.Action onComplete = null)
    {
        if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
        _movementCoroutine = StartCoroutine(MoveCameraRoutine(targetPos, targetRot, onComplete));
    }

    private IEnumerator MoveCameraRoutine(Vector3 targetPos, Quaternion targetRot, System.Action onComplete)
    {
        Vector3 startPos = mainCameraTransform.position;
        Quaternion startRot = mainCameraTransform.rotation;
        float elapsed = 0f;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDuration;

            mainCameraTransform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCameraTransform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            yield return null;
        }

        mainCameraTransform.position = targetPos;
        mainCameraTransform.rotation = targetRot;

        onComplete?.Invoke();
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 특정 시설 내의 세부 메뉴(월드 캔버스)를 클릭 시 비율에 맞게 줌인/줌아웃하고
/// 상태를 기억하는 컨트롤러입니다.
/// </summary>
public class FacilitySubMenuZoom : MonoBehaviour
{
    [System.Serializable]
    public class SubMenu
    {
        public string menuName;
        public Button triggerButton;
        public RectTransform targetCanvas;
        [Tooltip("이 메뉴로 줌인할 때 임시로 숨길 다른 UI 요소들 (예: 다른 버튼이나 캔버스)")]
        public GameObject[] elementsToHide;
    }

    [Header("Manager References")]
    [SerializeField] private FacilityManager manager;
    [SerializeField] private FacilityCameraController cameraController;
    [SerializeField] private Transform mainCameraTransform;

    [Header("Facility Settings")]
    [SerializeField] private FacilityType facilityType; // 이 스크립트가 담당할 시설

    [Header("Zoom Settings")]
    [SerializeField] private float zoomDuration = 0.5f; // 선형 이동 시간 (0.5초)
    [SerializeField] private List<SubMenu> subMenus;

    private SubMenu _currentZoomedMenu = null;
    private bool _isMyFacilityActive = false;
    private Coroutine _movementCoroutine;

    // 캔버스 거리 계산용 상수 (오류 수정 및 명시적 분리)
    private const float BaseCanvasWidth = 10.25f;
    private const float BaseCanvasHeight = 5.75f;  // 캔버스 세로 크기 (참고용)
    private const float BaseCameraDistance = 5.0f; // 정사이즈일 때 카메라가 뒤로 물러날 정확한 목표 거리

    private void Start()
    {
        if (manager == null || cameraController == null || mainCameraTransform == null)
        {
            Debug.LogError("[FacilitySubMenuZoom] 필수 참조가 누락되었습니다. 인스펙터를 확인해주세요.");
            return;
        }

        // 각 서브메뉴 버튼 클릭 이벤트 바인딩
        foreach (var menu in subMenus)
        {
            if (menu.triggerButton != null)
            {
                menu.triggerButton.onClick.AddListener(() => OnSubMenuClicked(menu));
            }
        }
    }

    private void Update()
    {
        if (manager == null) return;

        bool isActiveNow = (manager.CurrentActiveIndex == (int)facilityType);

        // 시설 진입/퇴장 상태 변화 감지
        if (isActiveNow && !_isMyFacilityActive)
        {
            OnFacilityEntered();
        }
        else if (!isActiveNow && _isMyFacilityActive)
        {
            OnFacilityExited();
        }

        _isMyFacilityActive = isActiveNow;

        // 현재 시설을 보고 있고 줌인 상태일 때, S키 '단발성' 클릭으로 줌아웃
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
        // 다른 시설에 갔다가 돌아왔을 때, 이전에 줌인 상태였다면 그 위치를 기억하고 즉시 이동
        if (_currentZoomedMenu != null)
        {
            cameraController.enabled = false;
            StartCameraMovement(GetZoomTargetPosition(_currentZoomedMenu.targetCanvas), GetZoomTargetRotation(_currentZoomedMenu.targetCanvas));
            Debug.Log($"[FacilitySubMenuZoom] 기억된 상태로 복귀: {_currentZoomedMenu.menuName}");
        }
    }

    private void OnFacilityExited()
    {
        // 이 시설을 떠날 때는 메인 카메라 컨트롤러를 다시 켜서 자유롭게 이동하도록 권한 반환
        cameraController.enabled = true;
        if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
    }

    private void OnSubMenuClicked(SubMenu menu)
    {
        if (_currentZoomedMenu == menu) return;

        _currentZoomedMenu = menu;

        // 시야를 가리는 다른 요소 비활성화
        foreach (var go in menu.elementsToHide)
        {
            if (go != null) go.SetActive(false);
        }

        // 메인 카메라 컨트롤러 정지 (충돌 방지)
        cameraController.enabled = false;

        // 줌인 이동
        StartCameraMovement(GetZoomTargetPosition(menu.targetCanvas), GetZoomTargetRotation(menu.targetCanvas));

        Debug.Log($"[FacilitySubMenuZoom] 줌인 실행: {menu.menuName}");
    }

    private void ExecuteZoomOut()
    {
        if (_currentZoomedMenu == null) return;

        // 비활성화했던 요소들 복구
        foreach (var go in _currentZoomedMenu.elementsToHide)
        {
            if (go != null) go.SetActive(true);
        }

        _currentZoomedMenu = null;

        // 돌아갈 원래 궤도(Orbit) 좌표 계산
        CalculateOrbitTransform(out Vector3 orbitPos, out Quaternion orbitRot);

        // 줌아웃 이동 후, 완료되면 메인 카메라 컨트롤러 다시 활성화
        StartCameraMovement(orbitPos, orbitRot, onComplete: () =>
        {
            if (_isMyFacilityActive) cameraController.enabled = true;
        });

        Debug.Log("[FacilitySubMenuZoom] 줌아웃 (메인 뷰로 복귀)");
    }

    /// <summary>
    /// 캔버스 너비 비율에 따라 동적으로 카메라의 Z 거리(뒤로 물러날 거리)를 계산합니다.
    /// </summary>
    private Vector3 GetZoomTargetPosition(RectTransform canvasRect)
    {
        float canvasWorldWidth = canvasRect.rect.width * canvasRect.lossyScale.x;
        float distanceRatio = canvasWorldWidth / BaseCanvasWidth;
        float targetDistance = BaseCameraDistance * distanceRatio;

        // 카메라가 캔버스를 정면으로 바라보도록 캔버스의 반대 방향(-forward)으로 물러납니다.
        return canvasRect.position - (canvasRect.forward * targetDistance);
    }

    private Quaternion GetZoomTargetRotation(RectTransform canvasRect)
    {
        return Quaternion.LookRotation(canvasRect.forward, canvasRect.up);
    }

    /// <summary>
    /// 현재 시설의 기본 메인 뷰(궤도) 좌표와 회전값을 수학적으로 계산합니다.
    /// </summary>
    private void CalculateOrbitTransform(out Vector3 pos, out Quaternion rot)
    {
        float angle = (int)facilityType * (360f / FacilityManager.TotalFacilityCount);
        float rad = angle * Mathf.Deg2Rad;

        // CameraController에서 직접 반지름 값을 가져와 사용합니다.
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

    /// <summary>
    /// 0.5초 동안 목표 위치로 '선형(Linear)' 보간 이동합니다.
    /// </summary>
    private IEnumerator MoveCameraRoutine(Vector3 targetPos, Quaternion targetRot, System.Action onComplete)
    {
        Vector3 startPos = mainCameraTransform.position;
        Quaternion startRot = mainCameraTransform.rotation;
        float elapsed = 0f;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDuration;

            mainCameraTransform.position = Vector3.Lerp(startPos, targetPos, t); // 선형 이동
            mainCameraTransform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            yield return null;
        }

        // 최종 오차 보정
        mainCameraTransform.position = targetPos;
        mainCameraTransform.rotation = targetRot;

        onComplete?.Invoke(); // 이동 완료 콜백 실행
    }
}
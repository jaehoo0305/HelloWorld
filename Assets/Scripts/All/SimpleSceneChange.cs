using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 버튼 클릭 시 지정된 씬으로 이동하며, 필요 시 화면 해상도(비율)까지 변경하는 범용 스크립트입니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class SimpleSceneChange : MonoBehaviour
{
    public enum ScreenRatio
    {
        KeepCurrent,    // 현재 비율 유지
        Ratio16x9,      // 16:9 비율로 변경 (기본 씬)
        Ratio1x1        // 1:1 비율로 변경 (AFK 씬)
    }

    [Header("Scene Settings")]
    [Tooltip("이동할 씬의 정확한 이름을 입력하세요.")]
    [SerializeField] private string targetSceneName;

    [Header("Screen Settings")]
    [Tooltip("해당 씬으로 넘어갈 때 변경할 화면 비율을 선택하세요.")]
    [SerializeField] private ScreenRatio targetAspectRatio = ScreenRatio.KeepCurrent;

    [Header("Facility Shortcut (Optional)")]
    [Tooltip("체크 시 씬 전환 후 특정 상점과 세부 캔버스(예: Study)로 즉시 줌인합니다.")]
    [SerializeField] private bool useShortcut = false;
    [SerializeField] private FacilityDataSO facilityData;
    [SerializeField] private FacilityType targetFacility;
    [Tooltip("이동할 세부 메뉴의 Menu Name을 정확히 입력하세요 (예: Study)")]
    [SerializeField] private string targetSubMenuName;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnClickLoadScene);
        }
    }

    private void OnClickLoadScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning($"[SceneChangeButton] {gameObject.name}에 목표 씬 이름이 없습니다!");
            return;
        }

        // [추가] 숏컷 사용 시 데이터 및 줌인 상태 미리 세팅 (예약)
        if (useShortcut)
        {
            if (facilityData != null)
            {
                facilityData.SetFacility(targetFacility); // 씬 진입 시 해당 시설을 바라보도록 세팅
            }

            if (!string.IsNullOrEmpty(targetSubMenuName))
            {
                // 씬 진입 시 해당 캔버스로 즉시 줌인하도록 전역 기억장치에 예약
                FacilitySubMenuZoom.SetPreloadedZoomState(targetFacility, targetSubMenuName);
            }
        }

        // 1. 화면 해상도(비율) 변경 로직
        ApplyScreenRatio();

        // 2. 씬 로딩 로직
        if (SceneLoader.Instance != null)
        {
            Debug.Log($"[SceneChangeButton] SceneLoader로 '{targetSceneName}' 이동.");
            SceneLoader.Instance.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning($"[SceneChangeButton] SceneLoader가 없어 기본 모드로 '{targetSceneName}' 이동.");
            SceneManager.LoadScene(targetSceneName);
        }
    }

    /// <summary>
    /// 설정된 타겟 비율에 맞추어 창 해상도를 강제로 변경합니다.
    /// (PC 빌드/에디터 환경 기준 작동)
    /// </summary>
    private void ApplyScreenRatio()
    {
        switch (targetAspectRatio)
        {
            case ScreenRatio.Ratio16x9:
                // 16:9 기본 해상도 (예: FHD)
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                Debug.Log("[SceneChangeButton] 16:9 화면 비율로 전환합니다.");
                break;

            case ScreenRatio.Ratio1x1:
                // 1:1 정사각형 해상도
                Screen.SetResolution(1080, 1080, FullScreenMode.Windowed);
                Debug.Log("[SceneChangeButton] 1:1 화면 비율로 전환합니다.");
                break;

            case ScreenRatio.KeepCurrent:
            default:
                // 아무 작업도 하지 않음
                break;
        }
    }
}
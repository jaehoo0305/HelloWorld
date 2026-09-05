using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Model(FacilityDataSO, FacilityDatabaseSO)과 View(FacilityTitleLevelUI) 사이에서 데이터 흐름 및 상태를 중개하고
/// 씬 전환 및 인덱스 이동을 제어하는 Presenter / Controller 역할을 수행합니다.
/// </summary>
public class FacilityManager : MonoBehaviour
{
    [Header("Data (Model)")]
    [SerializeField] private FacilityDataSO facilityData;
    [SerializeField] private string exitSceneName = "KingdomScene";

    [Header("UI (View)")]
    [SerializeField] private FacilityTitleLevelUI titleLevelUI;

    public const int TotalFacilityCount = 8;

    // 외부 컨트롤러들이 읽어갈 수 있는 프로퍼티 (기존 유지)
    public int TargetIndex { get; private set; }
    public int CurrentActiveIndex { get; private set; }

    private void Awake()
    {
        if (facilityData == null)
        {
            Debug.LogError("[FacilityManager] 데이터 참조가 누락되었습니다.");
            return;
        }

        // 초기화
        TargetIndex = facilityData.CurrentIndex;
        CurrentActiveIndex = WrapIndex(TargetIndex);
    }

    private void Start()
    {
        // 씬 진입 시 초기 UI 화면 중개
        RefreshUI();
    }

    private void Update()
    {
        // 실시간 레벨 변경이나 외부 데이터 변동이 있을 수 있으므로 주기적 UI 동기화 중개
        RefreshUI();
    }

    /// <summary>
    /// Model에서 최신 상태를 읽어와 View에게 전달하는 MVP 중개 메서드입니다.
    /// </summary>
    public void RefreshUI()
    {
        if (facilityData == null || titleLevelUI == null) return;

        FacilityType currentType = facilityData.currentFacility;
        int currentLevel = facilityData.GetFacilityLevel(currentType);

        // 순수 동적 데이터(레벨)만 View에 전달
        titleLevelUI.SetFacilityLevel(currentLevel);
    }

    /// <summary>
    /// A, D 키 입력을 받아 목표 인덱스를 증감시킵니다.
    /// </summary>
    public void MoveIndex(int direction)
    {
        TargetIndex += direction;
        UpdateData();
    }

    /// <summary>
    /// 하단 아이콘 버튼 클릭 시 최단 경로로 인덱스를 이동시킵니다.
    /// </summary>
    public void MoveToTargetIndex(int index)
    {
        int wrappedIndex = WrapIndex(index);
        int currentIndexWrapped = WrapIndex(TargetIndex);
        int difference = wrappedIndex - currentIndexWrapped;

        const int HalfCount = TotalFacilityCount / 2;
        if (difference > HalfCount) difference -= TotalFacilityCount;
        else if (difference <= -HalfCount) difference += TotalFacilityCount;

        TargetIndex += difference;
        UpdateData();
    }

    public void ExitToKingdomScene()
    {
        if (facilityData != null) facilityData.isReturning = true;

        // 1. 이미 씬에 살아있는 SceneLoader 싱글톤이 있다면 고급 로딩(비동기) 사용
        if (SceneLoader.Instance != null)
        {
            Debug.Log($"[FacilityManager] SceneLoader를 통해 '{exitSceneName}'(으)로 이동합니다.");
            SceneLoader.Instance.LoadScene(exitSceneName);
        }
        // 2. 만약 해당 씬만 단독으로 테스트 중이라 SceneLoader가 없다면 기본 로딩 사용
        else
        {
            Debug.LogWarning($"[FacilityManager] SceneLoader를 찾을 수 없어 기본 모드로 '{exitSceneName}'(으)로 이동합니다.");
            SceneManager.LoadScene(exitSceneName);
        }
    }

    private void UpdateData()
    {
        CurrentActiveIndex = WrapIndex(TargetIndex);
        if (facilityData != null)
        {
            facilityData.SetFacility((FacilityType)CurrentActiveIndex);
        }

        // 인덱스 변경 시 Presenter가 View에 갱신 지시
        RefreshUI();
    }

    private int WrapIndex(int index)
    {
        return (index % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;
    }
}
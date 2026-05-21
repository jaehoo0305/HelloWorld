using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 상태(인덱스)를 관리하고 씬을 전환하는 중앙 사령탑입니다.
/// </summary>
public class FacilityManager : MonoBehaviour
{
    [Header("Data Reference")]
    [SerializeField] private FacilityDataSO facilityData;
    [SerializeField] private string exitSceneName = "KingdomScene";

    public const int TotalFacilityCount = 8;

    // 외부 컨트롤러들이 읽어갈 수 있는 프로퍼티
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
        SceneManager.LoadScene(exitSceneName);
    }

    private void UpdateData()
    {
        CurrentActiveIndex = WrapIndex(TargetIndex);
        if (facilityData != null)
        {
            facilityData.SetFacility((FacilityType)CurrentActiveIndex);
        }
    }

    private int WrapIndex(int index)
    {
        return (index % TotalFacilityCount + TotalFacilityCount) % TotalFacilityCount;
    }
}
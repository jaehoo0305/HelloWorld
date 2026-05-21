using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 상점 아이콘 버튼에 부착되어 클릭 시 카메라를 해당 상점 뷰로 한 번에 이동시킵니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class FacilityDirectSelector : MonoBehaviour
{
    [Header("Manager Reference")]
    [Tooltip("중앙 사령탑 스크립트를 연결합니다. 비워두면 Start 시점에 자동 탐색합니다.")]
    [SerializeField] private FacilityManager manager;

    [Header("Facility Target Settings")]
    [Tooltip("이 버튼을 눌렀을 때 이동할 목표 시설을 선택하세요.")]
    [SerializeField] private FacilityType targetFacility;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnDirectMoveClick);
        }
    }

    private void Start()
    {
        // 인스펙터에서 사령탑을 연결하지 않았을 경우 자동 탐색을 시도 (안정성 확보)
        if (manager == null)
        {
            manager = Object.FindFirstObjectByType<FacilityManager>();

            if (manager == null)
            {
                Debug.LogError($"[FacilityDirectSelector] {gameObject.name} 버튼에 연결할 FacilityManager를 씬에서 찾을 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// 버튼이 클릭되었을 때 호출되어 사령탑에 이동 명령을 내립니다.
    /// </summary>
    private void OnDirectMoveClick()
    {
        if (manager == null) return;

        // 선택된 상점의 Enum 값을 정수형 인덱스(0~7)로 변환
        int targetIndex = (int)targetFacility;

        // 사령탑에게 해당 인덱스로 한 번에 최단 거리 이동을 지시
        manager.MoveToTargetIndex(targetIndex);

        Debug.Log($"[FacilityDirectSelector] '{targetFacility}' 버튼 클릭됨 -> 다이렉트 이동 요청");
    }
}
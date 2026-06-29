using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 상점의 0레벨(미건설) 잠금 상태를 감지하여 장막을 치고, 
/// 비용 차감 및 시간 타이머 진행 후 자물쇠가 열리는 시각 연출을 전담하는 컨트롤러입니다.
/// </summary>
public class FacilityUnlockController : MonoBehaviour
{
    [Header("기준 데이터베이스 및 상태 연동")]
    [SerializeField] private FacilityDataSO facilityDataState;
    [SerializeField] private FacilityLevelCostTableSO costTable;

    [Header("화면 장막 및 비주얼 제어")]
    [Tooltip("뒤쪽에 화면을 어둡게 가리는 장막 오브젝트입니다. (Blind 오브젝트)")]
    [SerializeField] private GameObject blindObject;

    [Tooltip("장막 및 자물쇠 전체 UI가 부드럽게 사라지도록 제어할 CanvasGroup입니다. (Lock 오브젝트 또는 하위에 부착)")]
    [SerializeField] private CanvasGroup lockCanvasGroup;

    [Header("자물쇠 3D/UI 비주얼 구성요소")]
    [Tooltip("자물쇠의 몸체 트랜스폼입니다. (Body)")]
    [SerializeField] private RectTransform lockBodyTransform;

    [Tooltip("자물쇠의 상단 걸쇠(고리) 트랜스폼입니다. (Shackle)")]
    [SerializeField] private RectTransform lockShackleTransform;

    [Header("텍스트 정보 및 빌드 게이지 (선택 사항)")]
    [SerializeField] private TMP_Text costElectricityText;
    [SerializeField] private TMP_Text costBitcoinText;
    [SerializeField] private TMP_Text buildTimeText;
    [SerializeField] private Image buildProgressBar;
    [SerializeField] private Button upgradeButton;

    [Header("해금 애니메이션 세부 설정")]
    [Tooltip("걸쇠가 위로 올라가는 높이(픽셀) 설정입니다.")]
    [SerializeField] private float shackleLiftY = 120f;
    [Tooltip("걸쇠가 위로 올라가는 속도입니다.")]
    [SerializeField] private float shackleSpeed = 4f;
    [Tooltip("걸쇠(고리)가 Y축으로 회전할 최종 각도입니다.")]
    [SerializeField] private float bodyRotateAngle = 90f;
    [Tooltip("걸쇠(고리)가 회전하는 속도입니다.")]
    [SerializeField] private float bodyRotateSpeed = 3f;
    [Tooltip("최종 화면 장막이 페이드아웃되는 속도입니다.")]
    [SerializeField] private float fadeOutSpeed = 2f;

    private Vector2 _initialShacklePosition;
    private FacilityType _currentActiveFacility;
    private bool _isUpgrading = false;
    private Coroutine _buildCoroutine;

    private void Start()
    {
        // 1. Shackle의 에디터 상 최초 오리지널 위치를 완벽하게 백업하여 보존합니다.
        if (lockShackleTransform != null)
        {
            _initialShacklePosition = lockShackleTransform.anchoredPosition;
        }

        if (lockCanvasGroup == null)
        {
            lockCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeButtonClick);
        }

        // 초기 화면 세팅
        RefreshLockState();
    }

    private void Update()
    {
        if (facilityDataState == null) return;

        // 상점이 바뀐 것을 감지하면 즉시 UI 상태를 리프레시합니다.
        if (_currentActiveFacility != facilityDataState.currentFacility)
        {
            _currentActiveFacility = facilityDataState.currentFacility;
            RefreshLockState();
        }

        // 디버그 키 'F' 감지 시 즉시 잠금 풀림 치트 연출 실행
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TriggerDebugUnlock();
        }
    }

    /// <summary>
    /// 현재 활성화된 시설의 레벨 정보를 분석하여 장막 및 자물쇠의 활성화 여부를 결정합니다.
    /// </summary>
    private void RefreshLockState()
    {
        if (facilityDataState == null) return;

        // 코루틴 작동 중이었다면 안전하게 중단하고 초기화
        if (_buildCoroutine != null)
        {
            StopCoroutine(_buildCoroutine);
            _buildCoroutine = null;
        }

        _isUpgrading = false;

        int currentLevel = facilityDataState.GetFacilityLevel(_currentActiveFacility);

        if (currentLevel == 0)
        {
            // 아직 건설되지 않은 0레벨 상태라면 장막 및 자물쇠 요소를 화면에 활성화
            SetLockVisualsActive(true);

            if (lockCanvasGroup != null) lockCanvasGroup.alpha = 1f;

            ResetLockTransforms();
            SetupCostTexts();

            if (upgradeButton != null)
            {
                // 교회(Church)는 직접 재화 강화를 할 수 없는 건물이므로 버튼 상호작용을 방지합니다.
                upgradeButton.interactable = (_currentActiveFacility != FacilityType.Church);
            }

            if (buildProgressBar != null) buildProgressBar.fillAmount = 0f;
        }
        else
        {
            // 이미 1레벨 이상 건설이 끝난 상점이라면 잠금 화면 비활성화
            SetLockVisualsActive(false);
        }
    }

    /// <summary>
    /// 스크립트 본인이 꺼지는 문제를 방지하기 위해, 자물쇠 스크립트는 켜둔 채 
    /// 장막(Blind) 오브젝트와 자물쇠 비주얼 요소(Body, Shackle)만 안전하게 온오프합니다.
    /// </summary>
    private void SetLockVisualsActive(bool active)
    {
        if (blindObject != null)
        {
            blindObject.SetActive(active);
        }

        if (lockBodyTransform != null)
        {
            lockBodyTransform.gameObject.SetActive(active);
        }

        if (lockShackleTransform != null)
        {
            lockShackleTransform.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// 자물쇠 고리와 몸체의 애니메이션 위치/회전 상태를 최초 설정 상태로 원상 복귀합니다.
    /// </summary>
    private void ResetLockTransforms()
    {
        if (lockShackleTransform != null)
        {
            // 본래 오프셋 위치로 안전 복구하고, 회전 상태값도 깔끔하게 초기화합니다.
            lockShackleTransform.anchoredPosition = _initialShacklePosition;
            lockShackleTransform.localRotation = Quaternion.identity;
        }
        if (lockBodyTransform != null)
        {
            lockBodyTransform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 0Lv -> 1Lv 에 해당하는 자원 요구치를 테이블에서 읽어와 화면에 반영합니다.
    /// </summary>
    private void SetupCostTexts()
    {
        // 예외 처리: 교회의 경우 비용 정보를 노출하지 않고 특수 해금 조건 메시지를 표시합니다.
        if (_currentActiveFacility == FacilityType.Church)
        {
            if (costElectricityText != null) costElectricityText.text = "";
            if (costBitcoinText != null) costBitcoinText.text = "원정 후 해금";
            if (buildTimeText != null) buildTimeText.text = "";
            return;
        }

        if (costTable == null) return;

        // 0레벨 상점 건설 비용은 Target Level이 1인 행의 비용을 가져옵니다.
        if (costTable.TryGetCostForLevel(1, out LevelCostDetails details))
        {
            if (costElectricityText != null) costElectricityText.text = $"Ele: {FormatValue(details.requiredElectricity)}";
            if (costBitcoinText != null) costBitcoinText.text = $"Bit: {details.requiredBitcoin.ToString()}";
            if (buildTimeText != null) buildTimeText.text = $"Time: {FormatTime(details.buildTimeInSeconds)}";
        }
    }

    /// <summary>
    /// 강화(건설) 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    private void OnUpgradeButtonClick()
    {
        if (_isUpgrading || costTable == null || GeneratorResourceManager.Instance == null) return;

        // 교회의 경우 버튼 클릭 작동 방지
        if (_currentActiveFacility == FacilityType.Church) return;

        if (costTable.TryGetCostForLevel(1, out LevelCostDetails details))
        {
            // 1. 필요한 전력량이 충분한지 체크 후 안전하게 차감 시도
            if (GeneratorResourceManager.Instance.TryConsumeElectricity(details.requiredElectricity))
            {
                // 2. 타이머 코루틴 구동 시작
                _buildCoroutine = StartCoroutine(BuildTimerRoutine(details.buildTimeInSeconds));
            }
            else
            {
                Debug.LogWarning("[FacilityUnlock] 업그레이드에 필요한 전력이 부족합니다.");
            }
        }
    }

    /// <summary>
    /// 지정된 시간만큼 타이머를 돌려 진행률을 표시하는 코루틴입니다.
    /// </summary>
    private IEnumerator BuildTimerRoutine(float buildTime)
    {
        _isUpgrading = true;
        if (upgradeButton != null) upgradeButton.interactable = false;

        float remainingTime = buildTime;

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            // 진행률 게이지 반영
            float progress = 1f - (remainingTime / buildTime);
            if (buildProgressBar != null) buildProgressBar.fillAmount = progress;

            // 남은 시간 텍스트 최신화
            if (buildTimeText != null) buildTimeText.text = FormatTime(remainingTime);

            yield return null;
        }

        // 시간 종료 시 게이지를 꽉 채우고 최종 레벨업 반영
        if (buildProgressBar != null) buildProgressBar.fillAmount = 1f;
        if (buildTimeText != null) buildTimeText.text = "0s";

        // 세이브 데이터에만 깔끔하게 1레벨 정보를 할당합니다.
        if (facilityDataState != null)
        {
            facilityDataState.SetFacilityLevel(_currentActiveFacility, 1);
        }

        // 자물쇠 물리 해금 연출 시작
        yield return StartCoroutine(LockOpenAnimationRoutine());
    }

    /// <summary>
    /// 디버그 전용 치트로 즉각적인 건설 연출을 실행하는 헬퍼 메서드입니다.
    /// </summary>
    private void TriggerDebugUnlock()
    {
        if (_isUpgrading) return;

        Debug.Log($"[FacilityUnlock-Debug] 'F' 치트키 감지됨! '{_currentActiveFacility}'의 0레벨 잠금을 즉각 해제합니다.");

        if (_buildCoroutine != null)
        {
            StopCoroutine(_buildCoroutine);
        }

        _buildCoroutine = StartCoroutine(DebugUnlockRoutine());
    }

    /// <summary>
    /// 자원의 소모나 대기 시간 없이 즉시 1레벨을 부여하고 비주얼을 열어주는 치트 코루틴입니다.
    /// </summary>
    private IEnumerator DebugUnlockRoutine()
    {
        _isUpgrading = true;
        if (upgradeButton != null) upgradeButton.interactable = false;

        if (buildProgressBar != null) buildProgressBar.fillAmount = 1f;
        if (buildTimeText != null) buildTimeText.text = "0s";

        if (facilityDataState != null)
        {
            facilityDataState.SetFacilityLevel(_currentActiveFacility, 1);
        }

        // 자물쇠 회전 및 장막 걷기 연출 재생
        yield return StartCoroutine(LockOpenAnimationRoutine());
    }

    /// <summary>
    /// 자물쇠 걸쇠가 들리고, 몸체가 회전하며, 장막이 페이드아웃 되는 연출을 총괄합니다.
    /// </summary>
    private IEnumerator LockOpenAnimationRoutine()
    {
        // 1. 자물쇠 걸쇠가 위로 상승하는 연출 (Shackle Lift)
        if (lockShackleTransform != null)
        {
            Vector2 startPos = lockShackleTransform.anchoredPosition;
            Vector2 targetPos = new Vector2(startPos.x, startPos.y + shackleLiftY);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * shackleSpeed;
                lockShackleTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            lockShackleTransform.anchoredPosition = targetPos;
        }

        yield return new WaitForSeconds(0.1f);

        // 2. 자물쇠 몸체(Body)는 그대로 두고, 위로 솟아오른 걸쇠(Shackle)가 Y축으로 회전하도록 연출을 변경합니다.
        if (lockShackleTransform != null)
        {
            Quaternion startRot = lockShackleTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(0f, bodyRotateAngle, 0f);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * bodyRotateSpeed;
                lockShackleTransform.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                yield return null;
            }
            lockShackleTransform.localRotation = targetRot;
        }

        yield return new WaitForSeconds(0.2f);

        // 3. 화면 장막 알파 페이드아웃 (Blind Fade Out)
        if (lockCanvasGroup != null)
        {
            while (lockCanvasGroup.alpha > 0f)
            {
                lockCanvasGroup.alpha -= Time.deltaTime * fadeOutSpeed;
                yield return null;
            }
        }

        // 4. 해금 완료 후 비활성화 및 최종 정리
        SetLockVisualsActive(false);

        _isUpgrading = false;
        _buildCoroutine = null;
    }

    private string FormatTime(float seconds)
    {
        if (seconds < 60f)
        {
            return $"{seconds:F0}s";
        }

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int remainingSecs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes}m {remainingSecs}s";
    }

    private string FormatValue(long value)
    {
        if (value >= 1000000) return $"{(value / 1000000f):F1}M";
        if (value >= 1000) return $"{(value / 1000f):F1}K";
        return value.ToString();
    }
}
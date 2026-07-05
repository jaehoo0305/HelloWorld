using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DungeonCombat.Data;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 슬레이 더 스파이어 스타일의 카메라 무빙, 중앙 라운드 및 턴 배너 알림, 
    /// 그리고 전투 순서 자동 가속 테스트 등을 일괄적으로 컨트롤하는 비주얼 총감독 스크립트입니다.
    /// </summary>
    public class BattleVisualDirector : MonoBehaviour
    {
        [Header("[ 핵심 컴포넌트 연결 ]")]
        [SerializeField] private BattleTurnManager turnManager;
        [SerializeField] private BattleUIController uiController;

        [Header("[ 1. 카메라 제어 (Camera Motion) ]")]
        [Tooltip("전투를 촬영할 메인 카메라를 연결하세요.")]
        [SerializeField] private Camera mainCamera;
        [Tooltip("턴이 시작될 때 카메라가 유닛을 바라보는 오프셋 좌표입니다.")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.5f, -10f);
        [Tooltip("카메라가 중립 상태일 때(라운드 전환 등) 바라볼 기본 전장 중앙 좌표입니다.")]
        [SerializeField] private Vector3 neutralCenterPosition = new Vector3(0f, 0f, -10f);

        [Header("[ 2. 할로우 나이트식 카메라 팔로우 설정 ]")]
        [Tooltip("할로우 나이트식 카메라 부드러운 위치 도달 시간 (낮을수록 빠르게 반응)")]
        [SerializeField] private float cameraSmoothTime = 0.22f;

        [Header("[ 3. 슬더스 스타일 배너 UI (Notification Banner) ]")]
        [Tooltip("배너 전체를 깜빡이게 할 Canvas Group (배경 블랙 바 오브젝트 권장)")]
        [SerializeField] private CanvasGroup bannerCanvasGroup;
        [Tooltip("배너 중앙에 크게 보일 메인 텍스트 (TMP)")]
        [SerializeField] private TextMeshProUGUI mainTitleText;

        [Header("[ 4. 테스트 및 자동화 옵션 ]")]
        [Tooltip("체크 시 스페이스바를 누르면 다음 캐릭터의 턴으로 즉시 건너뜁니다.")]
        [SerializeField] private bool enableSpacebarTest = true;
        [Tooltip("체크 시 일정 시간마다 자동으로 턴이 끝납니다. (자동 무대 연출 감상용)")]
        [SerializeField] private bool enableAutoPlay = false;
        [SerializeField] private float autoPlayDelay = 3f;

        // --- 내부 상태 관리 변수 ---
        private Vector3 targetCameraPos;
        private bool isSequencing = false;
        private float autoPlayTimer = 0f;
        private Queue<IEnumerator> visualSequenceQueue = new Queue<IEnumerator>();

        // 할로우 나이트식 Elastic 트래킹 가속도 상태 저장 벡터
        private Vector3 cameraVelocity = Vector3.zero;

        // 카메라 모드 상태 플래그 및 문명 6식 드래그 상태 보관용
        private bool isCameraLocked = true; // 기본값은 캐릭터 자동 추적 고정 상태
        private Vector3 dragStartMousePos;  // 드래그 시작 시점의 스크린 마우스 좌표
        private Vector3 dragStartCamPos;
        private bool isDragging = false;

        private void Start()
        {
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();
            if (uiController == null) uiController = FindFirstObjectByType<BattleUIController>();
            if (mainCamera == null) mainCamera = Camera.main;

            // 카메라 초기 위치는 전장 중앙으로 세팅
            targetCameraPos = neutralCenterPosition;
            if (mainCamera != null) mainCamera.transform.position = targetCameraPos;

            // 배너 초기 투명도는 0%로 가려둡니다.
            if (bannerCanvasGroup != null)
            {
                bannerCanvasGroup.alpha = 0f;
                bannerCanvasGroup.transform.localScale = Vector3.one * 1.5f;
            }

            // 이벤트 바인딩
            if (turnManager != null)
            {
                turnManager.OnRoundStarted += OnRoundStartedHandler;
                turnManager.OnTurnStarted += OnTurnStartedHandler;
                turnManager.OnBattleEnded += OnBattleEndedHandler;
            }

            // 자동 시동 드라이버 작동 (전장에 깔린 배틀 유닛들을 긁어모아 최초 1회 게임을 켭니다)
            StartCoroutine(DelayedStartBattle());
        }

        private void Update()
        {
            // 1. 카메라 자유 조작 및 캐릭터 실시간 탄성 추적(Smooth Follow) 상태 분기
            if (isCameraLocked)
            {
                // [카메라 고정 모드]: 현재 행동권을 가진 유닛의 실시간 이동 상태를 쫓아갑니다 (아군, 적군 실시간 동기화)
                if (turnManager != null && turnManager.CurrentTurnUnit != null)
                {
                    targetCameraPos = turnManager.CurrentTurnUnit.transform.position + cameraOffset;
                }
            }
            else
            {
                // [자유 이동 모드]: 마우스 우클릭 드래그 입력을 계산하여 targetCameraPos를 조율합니다.
                HandleFreeCameraPan();
            }

            // 2. 카메라는 항상 부드러우면서도 탄력 넘치게 할로우 나이트식으로 목표 지점을 향해 가감속합니다.
            // 단, 문명 6식 1:1 드래그 중인 프레임에는 드래그 일체감을 위해 Lerp 딜레이를 생략하고 즉시 붙어있도록 처리합니다.
            if (mainCamera != null && !isDragging)
            {
                mainCamera.transform.position = Vector3.SmoothDamp(
                    mainCamera.transform.position,
                    targetCameraPos,
                    ref cameraVelocity,
                    cameraSmoothTime
                );
            }

            // 3. 비주얼 시퀀스 큐 처리기 작동
            if (visualSequenceQueue.Count > 0 && !isSequencing)
            {
                StartCoroutine(ExecuteNextSequence());
            }

            // 4. CAPS LOCK 입력 감지: 카메라 고정/해제 토글 제어 (Caps Lock 키로 전장 뷰 모드 전환)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.capsLockKey.wasPressedThisFrame)
            {
                ToggleCameraLock();
            }
#else
            if (Input.GetKeyDown(KeyCode.CapsLock))
            {
                ToggleCameraLock();
            }
#endif

            // 5. 원래대로 복원된 디버그 및 수동 조작 테스트 키 (Spacebar로 턴 강제 종료)
            if (enableSpacebarTest)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    TriggerEndTurn();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    TriggerEndTurn();
                }
#endif
            }

            // 6. 관람용 자동 플레이 타이머
            if (enableAutoPlay && turnManager != null && turnManager.CurrentTurnUnit != null)
            {
                autoPlayTimer += Time.deltaTime;
                if (autoPlayTimer >= autoPlayDelay)
                {
                    TriggerEndTurn();
                }
            }
        }

        /// <summary>
        /// Caps Lock을 누르면 카메라 고정을 풀고, 다시 누르면 즉시 현재 유닛에게 빠르게 안착하며 락 상태로 전환합니다.
        /// </summary>
        private void ToggleCameraLock()
        {
            isCameraLocked = !isCameraLocked;

            if (isCameraLocked)
            {
                Debug.Log("[디렉터] 카메라 자동 추적 활성화 (캐릭터 포커스 고정)");
                if (turnManager != null && turnManager.CurrentTurnUnit != null)
                {
                    targetCameraPos = turnManager.CurrentTurnUnit.transform.position + cameraOffset;
                }
                else
                {
                    targetCameraPos = neutralCenterPosition;
                }
            }
            else
            {
                Debug.Log("[디렉터] 카메라 자유 모드 활성화 (우클릭 드래그로 화면 탐색 가능)");
            }
        }

        /// <summary>
        /// 마우스 우클릭으로 바닥 가상 평면(Y=0)을 집어 올린 뒤, 마우스 커서와 맵을 1:1로 결합해 물리적으로 움직입니다.
        /// 카메라 위치 업데이트로 인한 피드백 루프 떨림(Jitter)을 방지하기 위해 드래그 시작 시점의 카메라 가상 평면을 기준으로 계산합니다.
        /// </summary>
        private void HandleFreeCameraPan()
        {
            Vector3 mousePos;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            mousePos = Mouse.current.position.ReadValue();
            bool isPressed = Mouse.current.rightButton.isPressed;
            bool wasPressed = Mouse.current.rightButton.wasPressedThisFrame;
#else
            mousePos = Input.mousePosition;
            bool isPressed = Input.GetMouseButton(1);
            bool wasPressed = Input.GetMouseButtonDown(1);
#endif

            if (wasPressed)
            {
                isDragging = true;
                dragStartMousePos = mousePos;
                dragStartCamPos = targetCameraPos;
            }
            else if (isPressed && isDragging)
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

                // 루프 기반 떨림(Feedback loop jitter)을 막기 위해 드래그 시작 시점 카메라 위치를 기준으로 안정적 레이캐스트 계산
                Vector3 originalCamPos = mainCamera.transform.position;
                mainCamera.transform.position = dragStartCamPos;

                Ray startRay = mainCamera.ScreenPointToRay(dragStartMousePos);
                Ray currentRay = mainCamera.ScreenPointToRay(mousePos);

                bool startHit = groundPlane.Raycast(startRay, out float startEnter);
                bool currentHit = groundPlane.Raycast(currentRay, out float currentEnter);

                // 연출 중간 프레임 흔들림 방지를 위해 원래 카메라 위치로 즉시 환원
                mainCamera.transform.position = originalCamPos;

                if (startHit && currentHit)
                {
                    Vector3 startWorldPoint = startRay.GetPoint(startEnter);
                    Vector3 currentWorldPoint = currentRay.GetPoint(currentEnter);

                    // 시작점과 현재 점의 평면 차이 계산
                    Vector3 delta = currentWorldPoint - startWorldPoint;

                    // Y축(높이)은 고정하고 X, Z축 평면만 오프셋 이동
                    Vector3 nextTarget = dragStartCamPos - new Vector3(delta.x, 0f, delta.z);
                    targetCameraPos = new Vector3(nextTarget.x, originalCamPos.y, nextTarget.z);

                    // 드래그 중인 프레임 동안 1:1 부착 효과 극대화
                    mainCamera.transform.position = targetCameraPos;
                    cameraVelocity = Vector3.zero;
                }
            }
            else
            {
                isDragging = false;
            }
        }

        private IEnumerator DelayedStartBattle()
        {
            // 유니티 씬이 켜진 후 0.5초 대기하고 전투를 정식으로 개시합니다.
            yield return new WaitForSeconds(0.5f);

            BattleUnit[] foundUnits = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            if (foundUnits != null && foundUnits.Length > 0)
            {
                List<BattleUnit> list = new List<BattleUnit>(foundUnits);
                turnManager.StartBattle(list);
            }
            else
            {
                Debug.LogWarning("[디렉터] 전장에 배치된 BattleUnit을 찾지 못해 전투를 시작하지 못했습니다.");
            }
        }

        /// <summary>
        /// 턴을 끝내고 타이머를 초기화하는 통합 조종 메서드
        /// </summary>
        private void TriggerEndTurn()
        {
            autoPlayTimer = 0f;
            if (turnManager != null)
            {
                turnManager.EndCurrentTurn();
            }
        }

        /// <summary>
        /// 연출 대기열에서 하나씩 코루틴을 꺼내 화면에 꼬이지 않고 질서정연하게 뿌립니다.
        /// </summary>
        private IEnumerator ExecuteNextSequence()
        {
            isSequencing = true;
            IEnumerator currentSequence = visualSequenceQueue.Dequeue();
            yield return StartCoroutine(currentSequence);
            isSequencing = false;
        }

        #region [ 이벤트 처리 및 연출 큐 등록 ]

        private void OnRoundStartedHandler(int roundNumber)
        {
            // 라운드 연출은 화면 중앙에서 펼칩니다.
            visualSequenceQueue.Enqueue(SequenceRoundBanner(roundNumber));
        }

        private void OnTurnStartedHandler(BattleUnit activeUnit)
        {
            if (activeUnit == null) return;

            // 타이머 초기화 및 카메라 조준 설정, 캐릭터 턴 배너 연출을 큐에 등록합니다.
            autoPlayTimer = 0f;
            visualSequenceQueue.Enqueue(SequenceTurnBanner(activeUnit));
        }

        private void OnBattleEndedHandler()
        {
            visualSequenceQueue.Clear();
            visualSequenceQueue.Enqueue(SequenceBattleEndBanner());
        }

        #endregion

        #region [ 실제 비주얼 연출 코루틴 모음 ]

        /// <summary>
        /// 라운드 시작 슬더스식 배너 연출
        /// </summary>
        private IEnumerator SequenceRoundBanner(int round)
        {
            // 새로운 라운드 배너가 표시되기 시작할 때 툴팁 및 스킬 UI를 일괄적으로 숨김 처리합니다.
            if (uiController != null)
            {
                uiController.HideAllTooltips();
                uiController.SetSkillPanelActive(false);
            }

            // 라운드가 바뀌면 카메라는 전장 중앙으로 리턴합니다.
            targetCameraPos = neutralCenterPosition;

            if (bannerCanvasGroup == null) yield break;

            // 텍스트 기입
            if (mainTitleText != null)
            {
                mainTitleText.text = $"{round} 라운드 시작";
            }

            // 1. 배너 페이드인 + 위에서 떨어지듯 스케일 축소 연출
            float duration = 0.4f;
            float elapsed = 0f;
            bannerCanvasGroup.transform.localScale = Vector3.one * 1.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                bannerCanvasGroup.alpha = progress;
                bannerCanvasGroup.transform.localScale = Vector3.Lerp(Vector3.one * 1.6f, Vector3.one, progress);
                yield return null;
            }
            bannerCanvasGroup.alpha = 1f;
            bannerCanvasGroup.transform.localScale = Vector3.one;

            // 2. 화면 중앙에서 유저가 읽을 동안 유지
            yield return new WaitForSeconds(1.2f);

            // 3. 자연스럽게 페이드아웃
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                bannerCanvasGroup.alpha = 1f - progress;
                bannerCanvasGroup.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, progress);
                yield return null;
            }
            bannerCanvasGroup.alpha = 0f;
        }

        /// <summary>
        /// 카메라 패닝과 연동되는 캐릭터 턴 개시 배너 연출
        /// </summary>
        private IEnumerator SequenceTurnBanner(BattleUnit unit)
        {
            // 새로운 턴이 개시되어 연출이 시작되는 즉시 전 차례의 툴팁들을 완벽하게 닫아 화면을 정돈합니다.
            if (uiController != null)
            {
                uiController.HideAllTooltips();
            }

            // 1. 카메라 잠금 모드가 활성화되어 있다면 타겟 유닛 위치로 타겟 카메라 위치 즉시 설정
            if (isCameraLocked && unit != null)
            {
                targetCameraPos = unit.transform.position + cameraOffset;
            }

            if (bannerCanvasGroup == null) yield break;

            // 아군은 청록색에 가까운 푸른색(#4D94FF), 적군(EnemyUnit)은 붉은색(#FF4D4D)으로 이름 강조 표출
            bool isEnemy = unit is EnemyUnit;
            string colorHex = isEnemy ? "#FF4D4D" : "#4D94FF";

            if (mainTitleText != null)
            {
                mainTitleText.text = $"<color={colorHex}>{unit.UnitName}</color> 턴 시작";
            }

            // 2. 턴 전환 배너 연출 (FadeIn & FadeOut)
            float duration = 0.25f;
            float elapsed = 0f;
            bannerCanvasGroup.transform.localScale = Vector3.one * 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                bannerCanvasGroup.alpha = progress;
                bannerCanvasGroup.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, progress);
                yield return null;
            }
            bannerCanvasGroup.alpha = 1f;
            bannerCanvasGroup.transform.localScale = Vector3.one;

            // 턴 시작 알림 노출
            yield return new WaitForSeconds(0.7f);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                bannerCanvasGroup.alpha = 1f - progress;
                bannerCanvasGroup.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.9f, progress);
                yield return null;
            }
            bannerCanvasGroup.alpha = 0f;

            // 캐릭터의 턴 개시 배너 애니메이션이 완전히 소멸한 직후, 플레이어 유닛인 경우에만 조작창을 자연스럽게 활성화합니다.
            if (uiController != null)
            {
                bool isPlayer = !(unit.IsBoss || unit is EnemyUnit);
                uiController.SetSkillPanelActive(isPlayer);
            }
        }

        /// <summary>
        /// 승리/패배 상태 시 전투 완료 배너 연출
        /// </summary>
        private IEnumerator SequenceBattleEndBanner()
        {
            targetCameraPos = neutralCenterPosition;
            if (bannerCanvasGroup == null) yield break;

            if (mainTitleText != null)
            {
                mainTitleText.text = "전투 종료";
            }

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                bannerCanvasGroup.alpha = elapsed / 0.5f;
                yield return null;
            }
            bannerCanvasGroup.alpha = 1f;
        }

        #endregion
    }
}
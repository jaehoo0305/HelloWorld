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
        [Tooltip("카메라가 부드럽게 화면을 따라갈 속도 수치입니다.")]
        [SerializeField] private float cameraLerpSpeed = 4f;

        [Header("[ 2. 슬더스 스타일 배너 UI (Notification Banner) ]")]
        [Tooltip("배너 전체를 깜빡이게 할 Canvas Group (배경 블랙 바 오브젝트 권장)")]
        [SerializeField] private CanvasGroup bannerCanvasGroup;
        [Tooltip("배너 중앙에 크게 보일 메인 텍스트 (TMP)")]
        [SerializeField] private TextMeshProUGUI mainTitleText;

        [Header("[ 3. 테스트 및 자동화 옵션 ]")]
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
            // 1. 카메라의 부드러운 목적지 보간 이동 (Lerp)
            if (mainCamera != null)
            {
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position,
                    targetCameraPos,
                    Time.deltaTime * cameraLerpSpeed
                );
            }

            // 2. 비주얼 시퀀스 큐 처리기 작동
            if (visualSequenceQueue.Count > 0 && !isSequencing)
            {
                StartCoroutine(ExecuteNextSequence());
            }

            // 3. 디버그 및 수동 조작 테스트 키 (Spacebar로 턴 강제 종료)
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

            // 4. 관람용 자동 플레이 타이머
            if (enableAutoPlay && turnManager != null && turnManager.CurrentTurnUnit != null)
            {
                autoPlayTimer += Time.deltaTime;
                if (autoPlayTimer >= autoPlayDelay)
                {
                    TriggerEndTurn();
                }
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
            // 1. 카메라를 해당 유닛 위치로 부드럽게 조준
            if (unit != null)
            {
                targetCameraPos = unit.transform.position + cameraOffset;
            }

            if (bannerCanvasGroup == null) yield break;

            // 피아 구분에 따른 컬러 태그 적용
            bool isBoss = unit.CharacterData.PositionType == PositionType.Boss;
            string colorHex = isBoss ? "#FF4D4D" : "#4D94FF";

            if (mainTitleText != null)
            {
                mainTitleText.text = $"<color={colorHex}>{unit.CharacterData.CharacterName}</color> 턴 시작";
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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kingdom.Tutorial
{
    /// <summary>
    /// 외부 트리거 신호를 수신하고 전체적인 가이드 흐름을 제어하는 중앙 싱글톤 매니저입니다.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Database")]
        [Tooltip("게임 내의 모든 정적 튜토리얼 데이터 에셋 리스트")]
        [SerializeField] private List<TutorialDataSO> tutorialDatabase = new List<TutorialDataSO>();

        [Header("Views")]
        [SerializeField] private TutorialView tutorialView;

        [Header("System Options")]
        [Tooltip("연속 클릭 오작동 및 2단계 연속 스킵을 방지하기 위한 최소 쿨타임(초)")]
        [SerializeField] private float inputCooldown = 0.15f;

        // 대화가 최종적으로 종료되었을 때 발생하는 전역 이벤트 (매개변수: 완결된 튜토리얼 ID)
        public static event Action<string> OnTutorialEnded;

        private TutorialDataSO currentActiveTutorial;
        private int currentStepIndex = 0;
        private float lastInputTime = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 필요 시 씬이 전환되어도 파괴되지 않도록 설정할 수 있습니다.
                // DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 외부 시스템(레벨, 퀘스트, 클릭 이벤트 등)에서 특정 가이드를 틀기 위해 호출하는 메서드입니다.
        /// </summary>
        /// <param name="conditionKey">기획 시트에 지정된 triggerKey와 대조할 문자열</param>
        public void TryTriggerTutorial(string conditionKey)
        {
            // 현재 무언가 진행 중이라면 새로운 튜토리얼 요청은 무시
            if (currentActiveTutorial != null) return;

            // 데이터베이스에서 일치하는 트리거 키 탐색
            TutorialDataSO targetData = tutorialDatabase.Find(data => data.triggerKey == conditionKey);

            if (targetData != null)
            {
                StartTutorial(targetData);
            }
        }

        private void StartTutorial(TutorialDataSO data)
        {
            currentActiveTutorial = data;
            currentStepIndex = 0;

            tutorialView.ShowView(true);
            RenderCurrentStep();
        }

        /// <summary>
        /// 화면 클릭 시 다음 상태로 넘어가거나 타이핑을 스킵하는 컨트롤러 로직입니다.
        /// (버튼 OnClick 이벤트나 Update 입력 루프에서 이 함수를 바인딩하면 됩니다.)
        /// </summary>
        public void OnClickNext()
        {
            // 중복 클릭 가드 체크
            if (Time.time - lastInputTime < inputCooldown) return;
            lastInputTime = Time.time;

            if (currentActiveTutorial == null) return;

            // 1. 타이핑 중이었다면 먼저 스킵 처리
            if (tutorialView.IsTyping)
            {
                tutorialView.CompleteTyping();
            }
            // 2. 타이핑이 끝난 상태에서 클릭했다면 다음 단계로 진행
            else
            {
                currentStepIndex++;
                RenderCurrentStep();
            }
        }

        private void RenderCurrentStep()
        {
            if (currentActiveTutorial == null) return;

            // 마지막 가이드까지 출력 완료 시 종료 단계로 진입
            if (currentStepIndex >= currentActiveTutorial.guideTexts.Count)
            {
                EndTutorial();
                return;
            }

            string currentText = currentActiveTutorial.guideTexts[currentStepIndex];
            tutorialView.RenderStep(
                currentActiveTutorial.speakerName,
                currentText,
                currentActiveTutorial.speakerPortrait
            );
        }

        private void EndTutorial()
        {
            string completedID = currentActiveTutorial.tutorialID;

            currentActiveTutorial = null;
            currentStepIndex = 0;

            // 뷰 패널 숨김 처리 (오브젝트 자체는 켜둔 채 알파값 및 상호작용만 차단)
            tutorialView.ShowView(false);

            // 외부 디커플링용 전역 알림 이벤트 전송
            OnTutorialEnded?.Invoke(completedID);
            Debug.Log($"[Tutorial System] Tutorial successfully completed: {completedID}");
        }
    }
}
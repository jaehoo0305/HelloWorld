using System.Collections;
using UnityEngine;

namespace Kingdom.Tutorial
{
    /// <summary>
    /// 씬이 시작된 후 지정된 지연 시간(초) 이후에 자동으로 특정 튜토리얼을 1회 실행하는 컴포넌트입니다.
    /// </summary>
    public class SceneStartTutorialTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [Tooltip("실행할 튜토리얼의 triggerKey를 입력합니다.")]
        [SerializeField] private string targetTriggerKey = "SCENE_START_INTRO";

        [Tooltip("씬 시작 후 대기할 시간(초)입니다.")]
        [SerializeField] private float delaySeconds = 0.5f;

        [Header("Persistence Options")]
        [Tooltip("체크 시, 유저가 이 가이드를 평생 딱 한 번만 보도록 제한합니다. (PlayerPrefs 저장 활용)")]
        [SerializeField] private bool triggerOnlyOnceEver = true;

        private string SaveKey => $"Completed_Trigger_{targetTriggerKey}";

        private void Start()
        {
            // 평생 한 번만 실행하는 조건인데, 이미 실행한 적이 있다면 스킵
            if (triggerOnlyOnceEver && PlayerPrefs.GetInt(SaveKey, 0) == 1)
            {
                Debug.Log($"[Tutorial Trigger] Skip trigger. Already completed: {targetTriggerKey}");
                return;
            }

            // 안전한 실행을 위해 지연 코루틴 시작
            StartCoroutine(TriggerAfterDelayCoroutine());
        }

        private IEnumerator TriggerAfterDelayCoroutine()
        {
            // 지정된 시간만큼 대기 (예: 0.5초)
            yield return new WaitForSeconds(delaySeconds);

            if (TutorialManager.Instance != null)
            {
                Debug.Log($"[Tutorial Trigger] Auto starting scene tutorial with key: {targetTriggerKey}");
                TutorialManager.Instance.TryTriggerTutorial(targetTriggerKey);

                // 성공적으로 발동되었다면 다시는 실행되지 않도록 저장 기록 남김
                if (triggerOnlyOnceEver)
                {
                    PlayerPrefs.SetInt(SaveKey, 1);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                Debug.LogWarning("[Tutorial Trigger] TutorialManager.Instance is missing in this scene!");
            }
        }

        /// <summary>
        /// 테스트용: 이 튜토리얼을 다시 보고 싶을 때 저장 데이터를 초기화하는 디버그 함수입니다.
        /// </summary>
        [ContextMenu("Reset One-Time Trigger State")]
        public void ResetTriggerState()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Debug.Log($"[Tutorial Trigger] Storage reset for key: {targetTriggerKey}");
        }
    }
}
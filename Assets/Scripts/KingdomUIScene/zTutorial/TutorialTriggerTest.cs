using UnityEngine;

namespace Kingdom.Tutorial
{
    /// <summary>
    /// 외부 시스템에서 대화 및 튜토리얼 매니저를 어떻게 호출하고 이벤트를 수신하는지 보여주는 검증용 예제 스크립트입니다.
    /// </summary>
    public class TutorialTriggerTest : MonoBehaviour
    {
        private void OnEnable()
        {
            // 튜토리얼 종료 이벤트를 구독합니다. (다른 매니저가 이 이벤트를 받아 잠금을 해제할 수 있습니다)
            TutorialManager.OnTutorialEnded += HandleTutorialEnded;
        }

        private void OnDisable()
        {
            TutorialManager.OnTutorialEnded -= HandleTutorialEnded;
        }

        private void Update()
        {
            // 디버그 테스트: 숫자 1키를 누르면 "PLAYER_LEVEL_1" 조건 트리거 작동
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("[Test] Try to trigger LEVEL 1 Tutorial");
                TutorialManager.Instance.TryTriggerTutorial("PLAYER_LEVEL_1");
            }

            // 디버그 테스트: 숫자 2키를 누르면 "UNLOCKED_BARRACKS" 조건 트리거 작동
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Debug.Log("[Test] Try to trigger BARRACKS Tutorial");
                TutorialManager.Instance.TryTriggerTutorial("UNLOCKED_BARRACKS");
            }
        }

        private void HandleTutorialEnded(string completedTutorialID)
        {
            // 튜토리얼이 완전히 끝났을 때의 행동을 이곳에서 비동기 처리(Decoupled)합니다.
            if (completedTutorialID == "TUT_LV1")
            {
                Debug.Log("[Test System] Level 1 Tutorial completed! Grant rewards or unlock feature.");
            }
        }
    }
}   
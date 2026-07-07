using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Kingdom.Tutorial
{
    /// <summary>
    /// UI 요소를 갱신하고 타이핑 효과를 연출하는 순수 뷰 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TutorialView : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private Image portraitImage;

        [Header("Typing Settings")]
        [SerializeField] private float typingSpeed = 0.03f;

        private CanvasGroup canvasGroup;
        private Coroutine typingCoroutine;
        private bool isTyping = false;
        private string currentFullText = "";

        public bool IsTyping => isTyping;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            // 시작 시 캔버스 그룹을 투명하게 만들어 클릭 차단 및 숨김 처리
            ShowView(false);
        }

        /// <summary>
        /// CanvasGroup의 Alpha와 Raycast 차단을 이용해 안전하게 UI를 켜고 끕니다.
        /// 오브젝트 자체를 비활성화하지 않으므로 외부 이벤트 리스너가 항상 유지됩니다.
        /// </summary>
        public void ShowView(bool isVisible)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        /// <summary>
        /// 뷰의 내용을 갱신하고 타이핑 연출을 시작합니다.
        /// </summary>
        public void RenderStep(string speakerName, string content, Sprite portrait)
        {
            if (nameText != null)
                nameText.text = speakerName;

            if (portraitImage != null)
            {
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.gameObject.SetActive(true);
                }
                else
                {
                    portraitImage.gameObject.SetActive(false);
                }
            }

            // 기존 타이핑이 돌고 있다면 정지 후 새로 시작
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            currentFullText = content;
            typingCoroutine = StartCoroutine(TypeTextCoroutine(content));
        }

        /// <summary>
        /// TMP의 maxVisibleCharacters를 제어하는 무결점 Rich Text 대응 타이핑 코루틴입니다.
        /// 글자 내부의 HTML 태그(예: <color=yellow>)가 중간에 깨져서 나타나지 않습니다.
        /// </summary>
        private IEnumerator TypeTextCoroutine(string fullText)
        {
            isTyping = true;
            contentText.text = fullText;
            contentText.maxVisibleCharacters = 0;

            // 메쉬를 강제로 업데이트하여 정확한 캐릭터 수를 구합니다.
            contentText.ForceMeshUpdate();
            int totalVisibleCharacters = contentText.textInfo.characterCount;
            int counter = 0;

            while (counter <= totalVisibleCharacters)
            {
                contentText.maxVisibleCharacters = counter;
                counter++;
                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            typingCoroutine = null;
        }

        /// <summary>
        /// 타이핑 효과를 즉시 생략하고 전체 텍스트를 한 번에 보여줍니다.
        /// </summary>
        public void CompleteTyping()
        {
            if (!isTyping) return;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            contentText.text = currentFullText;
            contentText.maxVisibleCharacters = currentFullText.Length;
            isTyping = false;
        }
    }
}
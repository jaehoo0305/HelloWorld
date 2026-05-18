using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUIPopUp : MonoBehaviour
{
    // 각 UI 요소의 목적지 정보를 담는 구조체
    [Serializable]
    public struct UIElementMover
    {
        public RectTransform rectTransform; // 대상 UI의 RectTransform
        public Vector2 positionA;          // 열렸을 때의 목표 위치 (A)
        public Vector2 positionB;          // 닫혔을 때의 원래 위치 (B)
    }

    [Header("UI Elements to Move")]
    [SerializeField] private UIElementMover backgroundImage; // 이동할 이미지 1개
    [SerializeField] private UIElementMover[] menuButtons = new UIElementMover[3]; // 이동할 버튼 3개

    [Header("Animation Settings")]
    [Tooltip("이동하는 데 걸리는 시간 (초)")]
    [SerializeField] private float duration = 0.3f;

    [Tooltip("이동 감속/가속 곡선 (Linear로 설정 시 완벽한 선형 이동)")]
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Trigger Button")]
    [Tooltip("팝업을 켜고 끌 토글 버튼")]
    [SerializeField] private Button triggerButton;

    private bool isOpen = false; // 현재 열려있는 상태(A 위치)인지 여부
    private Coroutine activeTransition; // 현재 진행 중인 이동 코루틴

    void Start()
    {
        // 트리거 버튼이 연결되어 있다면 자동으로 이벤트 연결
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(TogglePopUp);
        }

        // 시작 시 모든 요소를 원래 위치(B)로 초기화 고정
        ResetToDefaultPosition();
    }

    /// <summary>
    /// 외부나 버튼 클릭 시 호출하여 상태를 토글하는 함수
    /// </summary>
    public void TogglePopUp()
    {
        isOpen = !isOpen;

        // 이미 이동 중인 애니메이션이 있다면 중지하고 새로운 방향으로 이동 시작
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
        }

        activeTransition = StartCoroutine(TransitionRoutine(isOpen));
    }

    /// <summary>
    /// UI 요소를 부드럽게 선형 보간(Lerp)으로 이동시키는 코루틴
    /// </summary>
    private IEnumerator TransitionRoutine(bool targetState)
    {
        float elapsed = 0f;

        // 이미지의 시작 위치와 목적지 설정
        Vector2 imgStart = backgroundImage.rectTransform != null ? backgroundImage.rectTransform.anchoredPosition : Vector2.zero;
        Vector2 imgTarget = targetState ? backgroundImage.positionA : backgroundImage.positionB;

        // 버튼들의 시작 위치와 목적지 설정
        Vector2[] btnStarts = new Vector2[menuButtons.Length];
        Vector2[] btnTargets = new Vector2[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i].rectTransform != null)
            {
                btnStarts[i] = menuButtons[i].rectTransform.anchoredPosition;
                btnTargets[i] = targetState ? menuButtons[i].positionA : menuButtons[i].positionB;
            }
        }

        // 보간 이동 실행
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = movementCurve.Evaluate(t); // 커브에 따른 진행률 계산

            // 이미지 이동
            if (backgroundImage.rectTransform != null)
            {
                backgroundImage.rectTransform.anchoredPosition = Vector2.Lerp(imgStart, imgTarget, curveT);
            }

            // 버튼 3개 이동
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i].rectTransform != null)
                {
                    menuButtons[i].rectTransform.anchoredPosition = Vector2.Lerp(btnStarts[i], btnTargets[i], curveT);
                }
            }

            yield return null;
        }

        // 미세한 오차 보정을 위해 최종 목적지 값으로 확실하게 고정
        SetFinalPositions(imgTarget, btnTargets);

        activeTransition = null;
    }

    private void SetFinalPositions(Vector2 imgTarget, Vector2[] btnTargets)
    {
        if (backgroundImage.rectTransform != null)
        {
            backgroundImage.rectTransform.anchoredPosition = imgTarget;
        }

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i].rectTransform != null)
            {
                menuButtons[i].rectTransform.anchoredPosition = btnTargets[i];
            }
        }
    }

    private void ResetToDefaultPosition()
    {
        if (backgroundImage.rectTransform != null)
        {
            backgroundImage.rectTransform.anchoredPosition = backgroundImage.positionB;
        }

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i].rectTransform != null)
            {
                menuButtons[i].rectTransform.anchoredPosition = menuButtons[i].positionB;
            }
        }
    }

    // ====================================================================
    // [유니티 에디터 편의 기능] 인스펙터 컴포넌트 우클릭 메뉴로 좌표 쉽게 잡기
    // ====================================================================

    [ContextMenu("현재 씬 배치 위치를 B(닫힘) 위치로 모두 저장")]
    private void SaveCurrentPositionsAsB()
    {
        if (backgroundImage.rectTransform != null)
            backgroundImage.positionB = backgroundImage.rectTransform.anchoredPosition;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i].rectTransform != null)
                menuButtons[i].positionB = menuButtons[i].rectTransform.anchoredPosition;
        }
        Debug.Log("<color=cyan><b>[SettingsUIPopUp]</b></color> 현재 위치들을 모두 B(닫힘/원래 위치) 좌표로 기록했습니다.");
    }

    [ContextMenu("현재 씬 배치 위치를 A(열림) 위치로 모두 저장")]
    private void SaveCurrentPositionsAsA()
    {
        if (backgroundImage.rectTransform != null)
            backgroundImage.positionA = backgroundImage.rectTransform.anchoredPosition;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i].rectTransform != null)
                menuButtons[i].positionA = menuButtons[i].rectTransform.anchoredPosition;
        }
        Debug.Log("<color=lime><b>[SettingsUIPopUp]</b></color> 현재 위치들을 모두 A(열림/목표 위치) 좌표로 기록했습니다.");
    }
}
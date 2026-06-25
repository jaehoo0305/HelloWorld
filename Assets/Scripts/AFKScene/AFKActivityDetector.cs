using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public class AFKActivityDetector : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 용이 취할 수 있는 AFK 반응 상태 정의 (원하는 대로 확장 가능)
    /// </summary>
    public enum DragonState
    {
        Idle,           // 기본 (유튜브/디스코드 등이 감지되지 않을 때)
        WatchingYouTube, // 유튜브 감상 중일 때의 반응
        OnDiscord,      // 디스코드 활성화 중일 때의 반응
        Working,        // 코딩이나 문서 작업 중일 때 (예시 확장)
        Gaming          // 다른 게임을 플레이 중일 때 (예시 확장)
    }

    /// <summary>
    /// 사용자가 인스펙터에서 감지 규칙을 자유롭게 추가할 수 있도록 돕는 구조체
    /// </summary>
    [Serializable]
    public struct DetectionRule
    {
        [Tooltip("규칙의 이름 (예: 디스코드 감지)")]
        public string ruleName;

        [Tooltip("감지 시 전송할 용의 애니메이션 상태")]
        public DragonState targetState;

        [Tooltip("프로세스 이름 키워드 (대소문자 구분 없음, 미사용 시 비워두기. 예: Discord, chrome)")]
        public string processKeyword;

        [Tooltip("창 타이틀(제목) 검색 키워드 (미사용 시 비워두기. 예: YouTube, 유튜브, Visual Studio)")]
        public string titleKeyword;
    }

    [Header("Detection Rules Settings")]
    [Tooltip("감지하고 싶은 프로그램이나 사이트 규칙을 인스펙터에서 자유롭게 추가하세요.")]
    [SerializeField] private List<DetectionRule> detectionRules = new List<DetectionRule>();

    [Tooltip("감지 검사를 수행할 주기 (초 단위, 기본 1초)")]
    [SerializeField] private float checkInterval = 1.0f;

    [Header("State Change Event")]
    [Tooltip("활동 상태가 바뀔 때 실행될 이벤트 (애니메이터나 다른 UI를 동적 연결할 때 편리함)")]
    public UnityEvent<DragonState> onDragonStateChanged;

    private float _timer = 0f;
    private IntPtr _lastActiveHwnd = IntPtr.Zero;
    private DragonState _currentState = DragonState.Idle;

    void Start()
    {
        // 백그라운드 구동 보장
        Application.runInBackground = true;

        // 기획상 기본 규칙이 비어있다면 디스코드와 유튜브를 기본 탑재해 줍니다.
        if (detectionRules == null || detectionRules.Count == 0)
        {
            SetupDefaultRules();
        }

        // 초기 Idle 상태 통지
        NotifyStateChanged(DragonState.Idle);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= checkInterval)
        {
            _timer = 0f;
            DetectActiveActivity();
        }
    }

    /// <summary>
    /// 현재 최상단 활성화 창을 추적하여 등록된 규칙들과 대조하는 핵심 함수
    /// </summary>
    private void DetectActiveActivity()
    {
        IntPtr currentHwnd = GetForegroundWindow();
        if (currentHwnd == IntPtr.Zero) return;

        // 최상단 창이 바뀌었을 때만 정밀 분석 진행 (성능 최적화)
        if (currentHwnd != _lastActiveHwnd)
        {
            _lastActiveHwnd = currentHwnd;

            // 1. 창 제목(Title) 가져오기
            StringBuilder titleBuilder = new StringBuilder(256);
            GetWindowText(currentHwnd, titleBuilder, 256);
            string windowTitle = titleBuilder.ToString();

            // 2. 프로세스 이름(Process Name) 가져오기
            uint pid;
            GetWindowThreadProcessId(currentHwnd, out pid);
            string processName = "Unknown";
            try
            {
                Process proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch (Exception)
            {
                // 특수 보호 프로세스나 순간 종료된 프로세스 예외 처리
            }

            // 3. 등록된 규칙들을 차례대로 순회하며 매칭 여부 판정
            DragonState nextState = DragonState.Idle; // 기본값은 Idle
            bool isMatched = false;

            foreach (var rule in detectionRules)
            {
                bool processMatched = false;
                bool titleMatched = false;

                // 프로세스 검사
                if (!string.IsNullOrEmpty(rule.processKeyword))
                {
                    if (processName.IndexOf(rule.processKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        processMatched = true;
                    }
                }

                // 타이틀 키워드 검사
                if (!string.IsNullOrEmpty(rule.titleKeyword))
                {
                    if (windowTitle.IndexOf(rule.titleKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        titleMatched = true;
                    }
                }

                // 규칙 정의에 따라 매칭 성공 여부 결정
                // 둘 다 적혀있으면 둘 다 만족해야 하며, 하나만 적혀있으면 적힌 것만 만족하면 매칭됩니다.
                if (!string.IsNullOrEmpty(rule.processKeyword) && !string.IsNullOrEmpty(rule.titleKeyword))
                {
                    if (processMatched && titleMatched) isMatched = true;
                }
                else if (processMatched || titleMatched)
                {
                    isMatched = true;
                }

                if (isMatched)
                {
                    nextState = rule.targetState;
                    UnityEngine.Debug.Log($"<color=lime><b>[AFK 감지]</b></color> 규칙 매칭 성공: {rule.ruleName} | 감지 프로세스: {processName} | 타이틀: {windowTitle}");
                    break; // 첫 번째로 일치하는 규칙을 우선순위로 채택
                }
            }

            // 상태가 이전과 달라졌을 때만 실시간 애니메이션 및 상태 교체 실행
            if (nextState != _currentState)
            {
                _currentState = nextState;
                NotifyStateChanged(_currentState);
            }
        }
    }

    /// <summary>
    /// 상태 변경을 전파하고 애니메이션 출력을 실행하는 함수
    /// </summary>
    private void NotifyStateChanged(DragonState newState)
    {
        // 1. 이벤트 통지 (인스펙터 바인딩용)
        onDragonStateChanged?.Invoke(newState);

        // 2. 자체 애니메이션 출력 처리 (여기에 용 애니메이션 교체 작업 진행)
        ApplyDragonAnimation(newState);
    }

    /// <summary>
    /// [출력 구현부 플레이스홀더]
    /// 용의 애니메이션 및 연출을 바꾸는 실제 코드를 작성할 공간입니다.
    /// </summary>
    private void ApplyDragonAnimation(DragonState state)
    {
        UnityEngine.Debug.Log($"<color=cyan><b>[용 상태 변경 연출]</b></color> 애니메이션 상태가 <b>{state}</b>로 변경되었습니다!");

        switch (state)
        {
            case DragonState.Idle:
                // TODO: 용을 기본 AFK 대기 상태(호흡 등) 애니메이션으로 되돌립니다.
                // animator.SetTrigger("TrigIdle");
                break;

            case DragonState.WatchingYouTube:
                // TODO: 유튜브 감상 중일 때의 애니메이션 (팝콘을 먹거나 눈을 반짝이는 연출 등)을 재생합니다.
                // animator.SetTrigger("TrigWatch");
                break;

            case DragonState.OnDiscord:
                // TODO: 디스코드 대화 중일 때의 애니메이션 (채팅을 마구 치거나 신나하는 연출 등)을 재생합니다.
                // animator.SetTrigger("TrigChat");
                break;

            case DragonState.Working:
                // TODO: 개발자가 코딩 중일 때 (예: 노트북을 같이 타건하는 연출 등)
                break;

            case DragonState.Gaming:
                // TODO: 같이 긴장하며 게임하는 연출 등
                break;
        }
    }

    /// <summary>
    /// 인스펙터가 비어있을 때 자동으로 유튜브와 디코 규칙을 구축하는 안전 코드
    /// </summary>
    private void SetupDefaultRules()
    {
        detectionRules = new List<DetectionRule>
        {
            new DetectionRule
            {
                ruleName = "유튜브 웹서핑 감지",
                targetState = DragonState.WatchingYouTube,
                processKeyword = "", // 브라우저 가리지 않기 위해 비움
                titleKeyword = "YouTube" // 브라우저 창 이름에 YouTube가 들어갈 때
            },
            new DetectionRule
            {
                ruleName = "유튜브 웹서핑 한글 감지",
                targetState = DragonState.WatchingYouTube,
                processKeyword = "",
                titleKeyword = "유튜브"
            },
            new DetectionRule
            {
                ruleName = "디스코드 채팅 감지",
                targetState = DragonState.OnDiscord,
                processKeyword = "Discord", // 디스코드 앱 실행 시
                titleKeyword = ""
            }
        };
    }
}
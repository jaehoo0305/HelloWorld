using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI; // UI 컴포넌트 사용을 위한 네임스페이스 추가

public class Test : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // --- 전역 훅(Hook) API 선언부 ---
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // --- 윈도우 상수 정의 ---
    private const int WH_KEYBOARD_LL = 13; // 키보드 전역 훅 ID
    private const int WH_MOUSE_LL = 14;    // 마우스 전역 훅 ID

    private const int WM_KEYDOWN = 0x0100;    // 키 누름 이벤트
    private const int WM_LBUTTONDOWN = 0x0201; // 마우스 좌클릭 이벤트
    private const int WM_RBUTTONDOWN = 0x0204; // 마우스 우클릭 이벤트

    // --- UI 연동 설정 ---
    [Header("UI Settings")]
    [Tooltip("로그를 화면에 실시간으로 출력할 UI Text 컴포넌트")]
    public Text logUIText;

    [Tooltip("화면에 표시할 최대 로그 줄 수")]
    public int maxLogLines = 15;

    private List<string> _logHistory = new List<string>();

    // --- 멤버 변수 ---
    private IntPtr _keyboardHookID = IntPtr.Zero;
    private IntPtr _mouseHookID = IntPtr.Zero;

    // 가비지 컬렉터(GC)에 의해 델리게이트가 소멸되는 것을 방지하기 위해 레퍼런스 유지
    private LowLevelKeyboardProc _keyboardProc;
    private LowLevelMouseProc _mouseProc;

    private IntPtr _lastActiveHwnd = IntPtr.Zero;
    private float _checkInterval = 1.0f; // 최상단 창 검사 주기 (1초에 한 번)
    private float _timer = 0f;

    void Start()
    {
        WriteLog("[Test] 전역 감지 테스트 시스템이 시작되었습니다.");

        // 유니티가 백그라운드에서도 원활하게 돌 수 있도록 설정
        Application.runInBackground = true;

        // 가비지 컬렉터 방지용 명시적 대입
        _keyboardProc = HookCallbackKeyboard;
        _mouseProc = HookCallbackMouse;

        // 키보드/마우스 전역 훅 설치
#if !UNITY_EDITOR
        _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
        _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
        WriteLog("[Test] 빌드 환경: 전역 마우스/키보드 훅이 활성화되었습니다.");
#else
        WriteLog("<color=orange>[Test] 에디터 안정성을 위해 전역 마우스/키보드 훅 작동을 우회합니다. (빌드 후 확인 가능)</color>");
#endif
    }

    void Update()
    {
        // 1초마다 현재 최상단 프로그램 및 웹사이트를 체크합니다.
        _timer += Time.deltaTime;
        if (_timer >= _checkInterval)
        {
            _timer = 0f;
            CheckForegroundWindow();
        }
    }

    /// <summary>
    /// 디버그 콘솔과 화면 UI Text에 동시에 로그를 작성하는 통합 함수
    /// </summary>
    private void WriteLog(string message)
    {
        // 1. 유니티 디버그 콘솔에 로깅
        UnityEngine.Debug.Log(message);

        // 2. 연결된 UI Text가 있을 경우 화면에 추가 및 갱신
        if (logUIText != null)
        {
            _logHistory.Add(message);

            // 최대 로그 줄 수를 초과하면 오래된 로그부터 제거
            if (_logHistory.Count > maxLogLines)
            {
                _logHistory.RemoveAt(0);
            }

            // 리스트를 줄바꿈 문자(\n)로 병합하여 UI 텍스트에 적용
            logUIText.text = string.Join("\n", _logHistory);
        }
    }

    /// <summary>
    /// 현재 모니터 최상단에 활성화된 창과 프로세스, 그리고 웹사이트를 감지하는 핵심 함수
    /// </summary>
    private void CheckForegroundWindow()
    {
        IntPtr currentHwnd = GetForegroundWindow();
        if (currentHwnd == IntPtr.Zero) return;

        // 최상단 창이 바뀌었을 때만 변경 정보 로그 출력
        if (currentHwnd != _lastActiveHwnd)
        {
            _lastActiveHwnd = currentHwnd;

            // 1. 창 제목(Title) 획득
            StringBuilder titleBuilder = new StringBuilder(256);
            GetWindowText(currentHwnd, titleBuilder, 256);
            string windowTitle = titleBuilder.ToString();

            // 2. 프로세스 이름(PID) 획득
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
                // 이미 종료되었거나 접근 권한이 없는 특수 프로세스 예외 처리
            }

            // 3. 디버그 로그 출력
            WriteLog($"<color=cyan><b>[최상단 창 변경]</b></color> HWND: {currentHwnd} | 프로그램: {processName} | 타이틀: \"{windowTitle}\"");

            // 4. 특정 프로그램 감지 (예: 디스코드)
            if (processName.Equals("Discord", StringComparison.OrdinalIgnoreCase))
            {
                WriteLog("<color=purple><b>[프로그램 감지]</b></color> 현재 플레이어가 <b>디스코드</b>를 활성화하여 대화 중입니다!");
            }

            // 5. 특정 웹사이트 감지 (예: 유튜브)
            if (windowTitle.Contains("YouTube") || windowTitle.Contains("유튜브"))
            {
                WriteLog("<color=red><b>[웹사이트 감지]</b></color> 현재 브라우저 최상단에서 <b>유튜브(YouTube)</b> 웹사이트가 감지되었습니다!");
            }
        }
    }

    // --- 전역 훅 셋업 함수 ---
    private IntPtr SetHook(Delegate proc, int hookType)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    // --- 키보드 전역 훅 콜백 ---
    private IntPtr HookCallbackKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam); // 가상 키코드 읽기
            KeyCode unityKey = (KeyCode)vkCode;

            // 유니티 콘솔 및 UI에 어떤 키가 입력되었는지 실시간 전역 감지 로깅
            WriteLog($"<color=yellow><b>[전역 키보드 감지]</b></color> 키 입력됨: {unityKey} (코드: {vkCode})");
        }
        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
    }

    // --- 마우스 전역 훅 콜백 ---
    private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                WriteLog("<color=lime><b>[전역 마우스 감지]</b></color> 마우스 <b>왼쪽 클릭</b> 발생!");
            }
            else if (wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                WriteLog("<color=lime><b>[전역 마우스 감지]</b></color> 마우스 <b>오른쪽 클릭</b> 발생!");
            }
        }
        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
    }

    // --- 메모리 누수 및 오작동 방지용 해제 처리 ---
    void OnDestroy()
    {
        UnhookAll();
    }

    void OnApplicationQuit()
    {
        UnhookAll();
    }

    private void UnhookAll()
    {
        // 게임이 종료될 때 반드시 전역 훅 체인을 제거해야 윈도우 시스템 오동작이 없습니다.
        if (_keyboardHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookID);
            _keyboardHookID = IntPtr.Zero;
            UnityEngine.Debug.Log("[Test] 전역 키보드 훅이 정상 해제되었습니다.");
        }

        if (_mouseHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookID);
            _mouseHookID = IntPtr.Zero;
            UnityEngine.Debug.Log("[Test] 전역 마우스 훅이 정상 해제되었습니다.");
        }
    }
}
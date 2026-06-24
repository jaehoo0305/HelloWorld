using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text; // StringBuilder 사용을 위해 추가
using UnityEngine;

public class TransparentWindowController : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // --- 윈도우 스타일 및 포지션 상수 정의 ---
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);    // 항상 최상단
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // 최상단 해제

    private const uint SWP_NOMOVE = 0x0002;       // 위치 고정
    private const uint SWP_NOSIZE = 0x0001;       // 크기 고정
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;   // 포커스 활성화 방지

    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isInitialized = false;

    [Header("Window Settings")]
    public bool alwaysOnTop = true;
    public int windowWidth = 400;
    public int windowHeight = 250;

    void Awake()
    {
#if !UNITY_EDITOR
        Screen.fullScreen = false;
        Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
        StartCoroutine(InitializeWindowRoutine());
#endif
    }

    /// <summary>
    /// 현재 유니티 프로세스의 PID 및 가시성, 타이틀을 정밀 대조하여 오리지널 게임 창 핸들만 찾아냅니다.
    /// </summary>
    private IEnumerator InitializeWindowRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        uint myPid = GetCurrentProcessId();

        while (_hwnd == IntPtr.Zero)
        {
            IntPtr currentHwnd = IntPtr.Zero;
            while (true)
            {
                currentHwnd = FindWindowEx(IntPtr.Zero, currentHwnd, null, null);
                if (currentHwnd == IntPtr.Zero) break;

                uint windowPid;
                GetWindowThreadProcessId(currentHwnd, out windowPid);

                // 1. 프로세스 ID가 일치하고, 화면에 보이는(Visible) 창인지 확인
                if (windowPid == myPid && IsWindowVisible(currentHwnd))
                {
                    // 2. 창 제목을 파싱하여 가짜 시스템 창(IME 등)을 필터링합니다.
                    StringBuilder titleBuilder = new StringBuilder(256);
                    GetWindowText(currentHwnd, titleBuilder, 256);
                    string title = titleBuilder.ToString();

                    if (!string.IsNullOrEmpty(title) && !title.Contains("IME") && !title.Contains("MSCTFIME"))
                    {
                        _hwnd = currentHwnd;
                        break;
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }

        ApplyAlwaysOnTop();
        _isInitialized = true;

        StartCoroutine(EnsureTopmostRoutine());
    }

    public void SetAlwaysOnTop(bool enable)
    {
        alwaysOnTop = enable;
        if (_isInitialized) ApplyAlwaysOnTop();
    }

    public void ApplyAlwaysOnTop()
    {
        if (_hwnd == IntPtr.Zero) return;
        IntPtr targetLayer = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(_hwnd, targetLayer, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    IEnumerator EnsureTopmostRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(1.0f);
            if (alwaysOnTop) ApplyAlwaysOnTop();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if !UNITY_EDITOR
        if (alwaysOnTop && _isInitialized) ApplyAlwaysOnTop();
#endif
    }

    void OnValidate()
    {
        if (Application.isPlaying && _isInitialized)
        {
            ApplyAlwaysOnTop();
        }
    }
}
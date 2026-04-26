using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class AlwaysOnTopWindow : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- 윈도우 상수 정의 ---
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1); // 항상 위
    const uint SWP_NOMOVE = 0x0002;   // 위치는 바꾸지 않음
    const uint SWP_NOSIZE = 0x0001;   // 크기는 바꾸지 않음
    const uint SWP_SHOWWINDOW = 0x0040;

    private IntPtr hWnd = IntPtr.Zero;
    private bool isInitialized = false;

    [Header("Window Settings")]
    public int initialWidth = 400;  // 실행 시 초기 가로 크기
    public int initialHeight = 450; // 실행 시 초기 세로 크기

    void Start()
    {
        #if !UNITY_EDITOR
        // 실행 시 창 크기를 설정합니다.
        Screen.fullScreen = false;
        Screen.SetResolution(initialWidth, initialHeight, FullScreenMode.Windowed);
        
        // 창 핸들을 찾고 항상 위로 고정합니다.
        StartCoroutine(FindAndFixWindow());
        #endif
    }

    IEnumerator FindAndFixWindow()
    {
        uint myPid = GetCurrentProcessId();
        int attempts = 0;

        // 창이 생성되고 안정화될 때까지 충분히 시도합니다.
        while (!isInitialized && attempts < 30)
        {
            yield return new WaitForSeconds(0.5f);

            IntPtr tempHwnd = GetActiveWindow();
            if (CheckIfMyWindow(tempHwnd, myPid)) { hWnd = tempHwnd; }

            if (hWnd == IntPtr.Zero)
            {
                IntPtr currentHwnd = IntPtr.Zero;
                while (true)
                {
                    currentHwnd = FindWindowEx(IntPtr.Zero, currentHwnd, null, null);
                    if (currentHwnd == IntPtr.Zero) break;
                    if (CheckIfMyWindow(currentHwnd, myPid))
                    {
                        hWnd = currentHwnd;
                        break;
                    }
                }
            }

            if (hWnd != IntPtr.Zero)
            {
                ApplyTopMost();
                isInitialized = true;
                Debug.Log("Window set to Top-Most.");
                break;
            }

            attempts++;
        }
    }

    // 포커스를 잃었다가 다시 얻을 때 최상단 설정을 재확인합니다.
    void OnApplicationFocus(bool hasFocus)
    {
        #if !UNITY_EDITOR
        if (isInitialized && hWnd != IntPtr.Zero)
        {
            ApplyTopMost();
        }
        #endif
    }

    private void ApplyTopMost()
    {
        if (hWnd != IntPtr.Zero)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }

    private bool CheckIfMyWindow(IntPtr handle, uint myPid)
    {
        if (handle == IntPtr.Zero) return false;
        uint windowPid;
        GetWindowThreadProcessId(handle, out windowPid);
        return windowPid == myPid;
    }
}
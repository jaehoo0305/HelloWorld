using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class TransparentWindowController : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- 윈도우 상수 정의 ---
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);    // 항상 최상단
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // 최상단 해제

    private const uint SWP_NOMOVE = 0x0002;   // 위치 고정
    private const uint SWP_NOSIZE = 0x0001;   // 크기 고정
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010; // 활성화하지 않음

    private IntPtr _hwnd;
    private bool _isInitialized = false;

    [Header("Window Settings")]
    [Tooltip("체크 시 창이 항상 다른 창 위에 표시됩니다.")]
    public bool alwaysOnTop = true;

    [Tooltip("초기 실행 시 창의 가로 크기")]
    public int windowWidth = 400;

    [Tooltip("초기 실행 시 창의 세로 크기")]
    public int windowHeight = 250;

    void Awake()
    {
        // #if UNITY_EDITOR return; 방식 대신 #if !UNITY_EDITOR로 감싸서 '닿을 수 없는 코드' 경고 해결
#if !UNITY_EDITOR
        Screen.fullScreen = false;
        Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);

        // 윈도우가 해상도 변경 처리를 마칠 시간을 준 후 초기화
        Invoke("InitializeWindow", 0.5f);
#endif
    }

    private void InitializeWindow()
    {
        // GetActiveWindow가 실패할 경우를 대비해 GetForegroundWindow도 시도
        _hwnd = GetActiveWindow();
        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = GetForegroundWindow();
        }

        if (_hwnd != IntPtr.Zero)
        {
            ApplyAlwaysOnTop();
            _isInitialized = true;

            // 초기화 직후 한 번 더 보정하기 위해 코루틴 실행
            StartCoroutine(EnsureTopmostRoutine());
        }
    }

    /// <summary>
    /// 창이 최상단에 확실히 고정되도록 초기 실행 시 몇 번 더 재확인합니다.
    /// </summary>
    IEnumerator EnsureTopmostRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(1.0f);
            if (alwaysOnTop) ApplyAlwaysOnTop();
        }
    }

    public void ApplyAlwaysOnTop()
    {
        if (_hwnd == IntPtr.Zero) return;

        IntPtr targetLayer = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        // SWP_NOACTIVATE를 추가하여 최상단 설정 시 포커스 강탈 문제를 줄입니다.
        SetWindowPos(_hwnd, targetLayer, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    // 창 밖을 클릭했다가 다시 돌아올 때나 포커스가 바뀔 때 최상단을 다시 강제합니다.
    void OnApplicationFocus(bool hasFocus)
    {
#if !UNITY_EDITOR
        if (alwaysOnTop && _isInitialized)
        {
            ApplyAlwaysOnTop();
        }
#endif
    }

    // 인스펙터에서 값을 바꿀 때 즉시 반영
    void OnValidate()
    {
        if (Application.isPlaying && _isInitialized)
        {
            ApplyAlwaysOnTop();
        }
    }
}
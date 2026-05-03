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
    private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- 윈도우 상수 정의 ---
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);    // 항상 최상단
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // 최상단 해제

    private const uint SWP_NOMOVE = 0x0002;   // 위치 고정
    private const uint SWP_NOSIZE = 0x0001;   // 크기 고정
    private const uint SWP_SHOWWINDOW = 0x0040;

    private IntPtr _hwnd;
    private bool _isInitialized = false;

    [Header("Window Settings")]
    [Tooltip("Top")]
    public bool alwaysOnTop = true;

    [Tooltip("Width")]
    public int windowWidth;

    [Tooltip("length")]
    public int windowHeight;

    void Awake()
    {
#if UNITY_EDITOR
        return;
#endif
        Screen.fullScreen = false;
        Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);

        // 윈도우가 해상도 변경 처리를 마칠 시간을 줍니다.
        Invoke("InitializeWindow", 0.5f);
    }

    private void InitializeWindow()
    {
        _hwnd = GetActiveWindow();
        if (_hwnd != IntPtr.Zero)
        {
            ApplyAlwaysOnTop();
            _isInitialized = true;
        }
    }

    public void ApplyAlwaysOnTop()
    {
        if (_hwnd == IntPtr.Zero) return;

        IntPtr targetLayer = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(_hwnd, targetLayer, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    // 창 밖을 클릭했다가 다시 돌아올 때나 포커스가 바뀔 때 설정을 보장합니다.
    void OnApplicationFocus(bool hasFocus)
    {
#if !UNITY_EDITOR
        if (hasFocus && alwaysOnTop && _isInitialized)
        {
            ApplyAlwaysOnTop();
        }
#endif
    }

    // 인스펙터에서 체크박스를 실시간으로 누를 때 즉시 반영되도록 합니다.
    void OnValidate()
    {
        if (Application.isPlaying && _isInitialized)
        {
            ApplyAlwaysOnTop();
        }
    }
}
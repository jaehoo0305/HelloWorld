using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class WindowController : MonoBehaviour
{

    [DllImport("user32.dll")]
    public static extern int MessageBox(
        IntPtr hwnd,
        string text,
        string caption,
        uint type);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLong(
        IntPtr hwnd,
        int nIndex,
        uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(
        IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y,
        int cx, int xy, uint uflags);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(
        IntPtr hwnd, ref MARGINS margins);

    private static readonly int GWL_EXSTYLE = -20;
    private static readonly uint WS_EX_LAYERED = 0x0008_0000;
    private static readonly uint WS_EX_TRANSPARENT = 0x0000_0020;

    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cxTopHeight;
        public int cxBottomHeight;
    }

    private static void SetTransparent(IntPtr hwnd)
    {

        var margines = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margines);
    }

    private static void SetAlwaysOnTop(IntPtr hwnd)
    {

        IntPtr topmost = new IntPtr(-1);
        SetWindowPos(hwnd, topmost, 0, 0, 0, 0, 0);
    }

    public static void SetClickThrough(IntPtr hwnd, bool through)
    {
        if (through)
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        else
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED);
    }

    private IntPtr _hwnd;

    private void Awake()
    {

#if UNITY_EDITOR
        return;
#endif

        _hwnd = GetActiveWindow();
        SetClickThrough(_hwnd, false);
        SetTransparent(_hwnd);
        SetAlwaysOnTop(_hwnd);

        Application.runInBackground = true;
        Screen.fullScreen = true;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
    }

    private void Update()
    {

#if UNITY_EDITOR
        return;
#endif
        var worldMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        SetClickThrough(_hwnd, Physics2D.OverlapPoint(worldMousePos) == null);
    }
}
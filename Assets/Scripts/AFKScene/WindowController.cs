using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class WindowController : MonoBehaviour
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint uflags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    private struct MARGINS { public int cxLeftWidth, cxRightWidth, cxTopHeight, cxBottomHeight; }

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint LWA_COLORKEY = 0x00000001;

    private IntPtr _hwnd;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;

        // 1. 카메라 배경색을 완전 투명한 검은색(0, 0, 0, 0)으로 설정
        _mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    private void Start()
    {
#if !UNITY_EDITOR
        // 해상도를 시스템 최대 크기로 설정
        Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, false);
        Application.runInBackground = true;
        StartCoroutine(InitializeWindow());
#endif
    }

    private IEnumerator InitializeWindow()
    {
        yield return new WaitForSeconds(1f);

        // 창 핸들 찾기
        _hwnd = FindWindow("UnityWndClass", Application.productName);
        if (_hwnd == IntPtr.Zero)
            _hwnd = FindWindow(null, Application.productName);

        if (_hwnd == IntPtr.Zero) yield break;

        // 2. DWM 프레임 확장 (알파 채널 투명을 윈도우와 합성하기 위해 필수)
        var margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // 윈도우 스타일 설정
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_LAYERED);

        // 3. 검은색(0x00000000)을 컬러키로 지정하여 투명 처리
        SetLayeredWindowAttributes(_hwnd, 0x00000000, 255, LWA_COLORKEY);

        // 항상 위로 설정
        SetWindowPos(_hwnd, new IntPtr(-1), 0, 0,
                     Display.main.systemWidth, Display.main.systemHeight,
                     0x0020);
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (_hwnd == IntPtr.Zero) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldMousePos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -_mainCamera.transform.position.z));
        worldMousePos.z = 0;

        // 캐릭터가 있는 곳만 클릭을 받도록 설정 (나머지는 클릭 통과)
        bool isOverCharacter = Physics2D.OverlapPoint(worldMousePos) != null;
        SetClickThrough(_hwnd, !isOverCharacter);
#endif
    }

    private void SetClickThrough(IntPtr hwnd, bool through)
    {
        if (through)
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        else
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED);
    }
}
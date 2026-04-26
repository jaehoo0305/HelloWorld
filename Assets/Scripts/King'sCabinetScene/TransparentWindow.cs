using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

public class TransparentWindow : MonoBehaviour
{
    // --- Windows API 선언부 ---
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    private struct Margins { public int left, right, top, bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // --- 윈도우 상수 정의 ---
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const uint WS_POPUP = 0x80000000;
    const uint WS_VISIBLE = 0x10000000;
    const int WS_EX_LAYERED = 0x00080000;
    const int WS_EX_TRANSPARENT = 0x00000020;
    const int LWA_COLORKEY = 0x00000001;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private IntPtr hWnd = IntPtr.Zero;
    private bool isInitialized = false;

#if !UNITY_EDITOR
    private bool lastIsOverObject = false;
#endif

    // 투명화 키 색상 (순수 초록: 0, 255, 0)
    // 이 색상이 칠해진 영역을 윈도우가 투명하게 뚫어버립니다.
    private Color32 chromaKey = new Color32(0, 255, 0, 255);

    void Start()
    {
        // 1. 카메라 설정을 강제로 크로마키 배경으로 변경합니다.
        // 배경을 투명(Alpha 0)이 아닌 불투명한 초록색으로 채워야 윈도우가 인식합니다.
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = (Color)chromaKey;
            Camera.main.allowHDR = false;
            Camera.main.allowMSAA = false;
        }

        #if !UNITY_EDITOR
        // 유니티 창이 완전히 뜰 때까지 대기 후 핸들을 찾습니다.
        StartCoroutine(FindWindowByPIDRoutine());
        #endif
    }

    IEnumerator FindWindowByPIDRoutine()
    {
        uint myPid = GetCurrentProcessId();
        int attempts = 0;

        while (!isInitialized && attempts < 40)
        {
            // 여러 경로로 현재 프로세스의 창 핸들(hWnd)을 검색합니다.
            IntPtr tempHwnd = GetActiveWindow();
            if (CheckIfMyWindow(tempHwnd, myPid)) { hWnd = tempHwnd; }

            if (hWnd == IntPtr.Zero)
            {
                tempHwnd = GetForegroundWindow();
                if (CheckIfMyWindow(tempHwnd, myPid)) { hWnd = tempHwnd; }
            }

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
                ApplyTransparency();
                isInitialized = true;
                break;
            }

            attempts++;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private bool CheckIfMyWindow(IntPtr handle, uint myPid)
    {
        if (handle == IntPtr.Zero) return false;
        uint windowPid;
        GetWindowThreadProcessId(handle, out windowPid);
        return windowPid == myPid;
    }

    private void ApplyTransparency()
    {
        // 1. 테두리 및 타이틀 바 제거 (WS_POPUP)
        SetWindowLong(hWnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // 2. 레이어드 스타일 적용 (투명화 기능 활성화)
        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);

        // 3. DWM 프레임 확장 (바탕화면과 렌더링 합성)
        Margins margins = new Margins { left = -1, right = -1, top = -1, bottom = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        // 4. 초록색(0x00FF00)을 컬러 키로 지정하여 윈도우 레벨에서 배경을 뚫음
        SetLayeredWindowAttributes(hWnd, 0x00FF00, 255, LWA_COLORKEY);

        // 5. 창을 항상 위(TOPMOST)로 설정하고 전체 화면 크기로 고정
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, Screen.width, Screen.height, 0);
    }

    void Update()
    {
        #if !UNITY_EDITOR
        if (!isInitialized || hWnd == IntPtr.Zero) return;

        // 매 프레임 전역 마우스 위치를 확인하여 클릭 투과 여부를 결정합니다.
        bool isOverObject = IsMouseOverObjectGlobal();

        if (isOverObject != lastIsOverObject)
        {
            if (isOverObject)
            {
                // 버튼 위일 때: 유니티 창이 클릭을 받도록 설정
                SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
            }
            else
            {
                // 빈 공간일 때: 클릭을 통과시켜 바탕화면이 클릭되게 설정
                SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            lastIsOverObject = isOverObject;
        }
        #endif
    }

    private bool IsMouseOverObjectGlobal()
    {
        POINT p;
        if (GetCursorPos(out p))
        {
            // Windows 좌표를 유니티의 화면 좌표로 변환합니다.
            Vector2 mousePos = new Vector2(p.X, Screen.height - p.Y);

            // 1. UI 감지 (버튼, 이미지 등)
            if (EventSystem.current != null)
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current) { position = mousePos };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                if (results.Count > 0) return true;
            }

            // 2. 물리 오브젝트 감지 (2D 콜라이더 등)
            if (Camera.main != null)
            {
                RaycastHit2D hit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(mousePos), Vector2.zero);
                if (hit2D.collider != null) return true;
            }
        }
        return false;
    }
}
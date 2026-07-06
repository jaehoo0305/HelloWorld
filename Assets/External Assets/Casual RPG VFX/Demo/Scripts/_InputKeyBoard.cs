using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("1Enwer/Input/_KeyBoard")]
public class _InputKeyBoard : MonoBehaviour
{
    public PressType pressType = PressType.GetKeyDown;
    public KeyCodeEvent[] keyBoardGetKey;

    public enum PressType
    {
        GetKey,
        GetKeyDown,
        GetKeyUp
    }

    [System.Serializable]
    public class KeyCodeEvent
    {
        public KeyCode keyCode;
        public UnityEvent function;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < keyBoardGetKey.Length; i++)
        {
            if (KeyBoardIsPressedNew(pressType, keyBoardGetKey[i].keyCode))
            {
                keyBoardGetKey[i].function.Invoke();
            }
        }
#else
        for (int i = 0; i < keyBoardGetKey.Length; i++)
        {
            if (KeyBoardIsPressedOld(pressType, keyBoardGetKey[i].keyCode))
            {
                keyBoardGetKey[i].function.Invoke();
            }
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    public bool KeyBoardIsPressedNew(PressType _pressType, KeyCode keycode)
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return false;

        var key = ConvertKeyCode(keycode);
        if (key == null) return false;

        switch (_pressType)
        {
            case PressType.GetKey:
                return key.isPressed;
            case PressType.GetKeyDown:
                return key.wasPressedThisFrame;
            case PressType.GetKeyUp:
                return key.wasReleasedThisFrame;
            default:
                return false;
        }
    }

    private UnityEngine.InputSystem.Controls.KeyControl ConvertKeyCode(KeyCode keyCode)
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return null;

        switch (keyCode)
        {
            case KeyCode.A: return keyboard.aKey;
            case KeyCode.B: return keyboard.bKey;
            case KeyCode.C: return keyboard.cKey;
            case KeyCode.D: return keyboard.dKey;
            case KeyCode.E: return keyboard.eKey;
            case KeyCode.F: return keyboard.fKey;
            case KeyCode.G: return keyboard.gKey;
            case KeyCode.H: return keyboard.hKey;
            case KeyCode.I: return keyboard.iKey;
            case KeyCode.J: return keyboard.jKey;
            case KeyCode.K: return keyboard.kKey;
            case KeyCode.L: return keyboard.lKey;
            case KeyCode.M: return keyboard.mKey;
            case KeyCode.N: return keyboard.nKey;
            case KeyCode.O: return keyboard.oKey;
            case KeyCode.P: return keyboard.pKey;
            case KeyCode.Q: return keyboard.qKey;
            case KeyCode.R: return keyboard.rKey;
            case KeyCode.S: return keyboard.sKey;
            case KeyCode.T: return keyboard.tKey;
            case KeyCode.U: return keyboard.uKey;
            case KeyCode.V: return keyboard.vKey;
            case KeyCode.W: return keyboard.wKey;
            case KeyCode.X: return keyboard.xKey;
            case KeyCode.Y: return keyboard.yKey;
            case KeyCode.Z: return keyboard.zKey;
            case KeyCode.Space: return keyboard.spaceKey;
            case KeyCode.LeftShift: return keyboard.leftShiftKey;
            case KeyCode.RightShift: return keyboard.rightShiftKey;
            case KeyCode.LeftControl: return keyboard.leftCtrlKey;
            case KeyCode.RightControl: return keyboard.rightCtrlKey;
            case KeyCode.LeftAlt: return keyboard.leftAltKey;
            case KeyCode.RightAlt: return keyboard.rightAltKey;
            case KeyCode.Escape: return keyboard.escapeKey;
            case KeyCode.Return: return keyboard.enterKey;
            case KeyCode.Backspace: return keyboard.backspaceKey;
            case KeyCode.Tab: return keyboard.tabKey;
            case KeyCode.UpArrow: return keyboard.upArrowKey;
            case KeyCode.DownArrow: return keyboard.downArrowKey;
            case KeyCode.LeftArrow: return keyboard.leftArrowKey;
            case KeyCode.RightArrow: return keyboard.rightArrowKey;
            case KeyCode.Alpha0: return keyboard.digit0Key;
            case KeyCode.Alpha1: return keyboard.digit1Key;
            case KeyCode.Alpha2: return keyboard.digit2Key;
            case KeyCode.Alpha3: return keyboard.digit3Key;
            case KeyCode.Alpha4: return keyboard.digit4Key;
            case KeyCode.Alpha5: return keyboard.digit5Key;
            case KeyCode.Alpha6: return keyboard.digit6Key;
            case KeyCode.Alpha7: return keyboard.digit7Key;
            case KeyCode.Alpha8: return keyboard.digit8Key;
            case KeyCode.Alpha9: return keyboard.digit9Key;
            default: return null;
        }
    }
#else
    public bool KeyBoardIsPressedOld(PressType _pressType, KeyCode keycode)
    {
        switch (_pressType)
        {
            case PressType.GetKey:
                return Input.GetKey(keycode);
            case PressType.GetKeyDown:
                return Input.GetKeyDown(keycode);
            case PressType.GetKeyUp:
                return Input.GetKeyUp(keycode);
            default:
                return false;
        }
    }
#endif
}
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

internal sealed class FlapFlightInput
{
    Gamepad activePad;
    bool armed;

    internal static bool Allowed
    {
        get
        {
            var manager = GameManager.Instance;
            var input = InputManager.Instance;
            if (!manager || manager.Loading || manager.inMenu || manager.modalsOpen || Time.timeScale <= 0f ||
                GameManager.gameMode != GameMode.Flying || (input && input.currentInputBlocker)) return false;
            var menu = MenuDialog.Instance;
            if (menu && ((menu.settingsPanel && menu.settingsPanel.activeInHierarchy) ||
                (menu.menuDialog && menu.menuDialog.activeInHierarchy) ||
                (menu.saveDialog && menu.saveDialog.activeInHierarchy) ||
                (menu.quitDialog && menu.quitDialog.activeInHierarchy))) return false;
            var events = EventSystem.current;
            var selected = events ? events.currentSelectedGameObject : null;
            return !selected || !(selected.GetComponentInParent<TMP_InputField>()?.isFocused == true ||
                selected.GetComponentInParent<InputField>()?.isFocused == true);
        }
    }

    internal void Reset() => armed = false;

    internal int ReadStep(bool allowed)
    {
        var pad = Gamepad.current;
        if (pad != activePad)
        {
            activePad = pad;
            Reset();
        }
        if (!allowed)
        {
            Reset();
            return 0;
        }

        int stickStep = pad != null && pad.enabled ? ReadStick(pad.rightStick.ReadValue().y) : 0;
        if (pad == null || !pad.enabled) Reset();
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rightBracketKey.wasPressedThisFrame) return 1;
            if (keyboard.leftBracketKey.wasPressedThisFrame) return -1;
        }
        else
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket)) return 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket)) return -1;
        }
        return stickStep;
    }

    int ReadStick(float vertical)
    {
        float travel = Mathf.Abs(vertical);
        if (float.IsNaN(travel) || float.IsInfinity(travel)) { Reset(); return 0; }
        if (travel <= .25f) { armed = true; return 0; }
        if (!armed || travel < .65f) return 0;
        armed = false;
        return vertical < 0f ? 1 : -1;
    }
}

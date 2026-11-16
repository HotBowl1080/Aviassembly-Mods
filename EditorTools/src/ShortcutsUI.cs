using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    internal sealed class ShortcutsUI : IDisposable
    {
        private GameObject root;
        private PrecisionInputBlocker blocker;
        internal bool IsAlive => root != null;
        internal bool IsOpen => root != null && root.activeSelf;

        internal static ShortcutsUI TryCreate(string focusKey)
        {
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = native.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled || native.text == null || native.text.font == null) continue;
                var ui = new ShortcutsUI();
                ui.root = new GameObject("EditorTools Shortcuts", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PrecisionInputBlocker));
                ui.root.layer = native.gameObject.layer;
                ui.blocker = ui.root.GetComponent<PrecisionInputBlocker>();
                var rect = (RectTransform)ui.root.transform;
                rect.SetParent(canvas.rootCanvas.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                ui.root.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.23f, 0.96f);
                var textGo = new GameObject("Shortcut List", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textGo.layer = ui.root.layer; textGo.transform.SetParent(rect, false);
                var tr = (RectTransform)textGo.transform;
                tr.anchorMin = new Vector2(0.12f, 0.08f); tr.anchorMax = new Vector2(0.88f, 0.92f);
                tr.offsetMin = tr.offsetMax = Vector2.zero;
                var label = textGo.GetComponent<TextMeshProUGUI>();
                label.font = native.text.font; label.fontSharedMaterial = native.text.font.material;
                label.fontSize = 25; label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 25;
                label.color = Color.white; label.alignment = TextAlignmentOptions.TopLeft;
                label.raycastTarget = false;
                label.text = "<b>EDITOR SHORTCUTS</b>                 <color=#AFC5FF>K or Esc to close</color>\n\n" +
                    focusKey + "    Focused / Unfocused camera\n" +
                    "V    Hide selected part       I    Show all hidden parts\n\n" +
                    "<b>MOVE & ROTATE</b>\n" +
                    "Drag an arrow or ring    Move or rotate on that handle\n" +
                    "G    Move selected part       R    Rotate selected part\n" +
                    "R again    Toggle trackball rotation\n" +
                    "X / Y / Z    Constrain axis; repeat for local; repeat to clear\n" +
                    "Shift + X / Y / Z    Exclude that axis when moving\n" +
                    "Middle mouse    Choose axis from mouse direction\n" +
                    "Shift    Fine adjustment       Ctrl    Snap\n" +
                    "Ctrl + Shift    Fine snapping\n\n" +
                    "<b>EXACT VALUES</b>\n" +
                    "Type a number    Set total adjustment during or after a handle drag\n" +
                    "Minus    Toggle sign       /    Reciprocal       Backspace    Erase\n" +
                    "Tab / Ctrl + Tab    Next / previous numeric component\n" +
                    "Enter or left click    Confirm       Esc or right click    Cancel\n" +
                    "Release mouse    Confirm a handle drag with no typed number\n\n" +
                    "<color=#AFC5FF>Idle controls stay native; movement uses metres, rotation uses degrees</color>";
                ui.root.SetActive(false);
                return ui;
            }
            return null;
        }

        internal void SetOpen(bool open)
        {
            if (!IsAlive) return;
            if (open)
            {
                root.transform.SetAsLastSibling();
                if (InputManager.Instance != null) InputManager.Instance.currentInputBlocker = blocker;
            }
            else if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker == blocker)
                InputManager.Instance.currentInputBlocker = null;
            root.SetActive(open);
        }

        public void Dispose()
        {
            SetOpen(false);
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}

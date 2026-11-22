using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    internal sealed class ShortcutsUI : IDisposable
    {
        private GameObject root;
        private GameObject openButtonRoot;
        private Button openButton;
        private Canvas canvas;
        private RectTransform card;
        internal bool IsAlive => root != null && openButtonRoot != null && canvas != null;
        internal bool IsOpen => root != null && root.activeSelf;

        internal static ShortcutsUI TryCreate(string focusKey, Action open)
        {
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = native.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled || native.buttonImage == null || native.text == null || native.text.font == null) continue;
                var ui = new ShortcutsUI { canvas = canvas.rootCanvas };
                ui.openButtonRoot = HidePartsUI.CreateButton(native, "EditorTools Hotkeys", "Hotkeys [K]", 170, false,
                    () => open(), out ui.openButton, out _, withIcon: false);
                ui.root = new GameObject("EditorTools Shortcuts", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ui.root.SetActive(false);
                ui.root.layer = native.gameObject.layer;
                ui.root.AddComponent<PrecisionInputBlocker>();
                var rect = (RectTransform)ui.root.transform;
                rect.SetParent(canvas.rootCanvas.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                ui.root.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.14f, 0.55f);

                var panel = new GameObject("Hotkeys panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panel.layer = ui.root.layer;
                ui.card = (RectTransform)panel.transform;
                ui.card.SetParent(rect, false);
                ui.card.anchorMin = ui.card.anchorMax = ui.card.pivot = new Vector2(0.5f, 0.5f);
                ui.card.anchoredPosition = Vector2.zero;
                var background = panel.GetComponent<Image>();
                var sprite = native.buttonImage.sprite;
                if (sprite != null && sprite.border.sqrMagnitude > 0)
                {
                    background.sprite = sprite;
                    background.type = Image.Type.Sliced;
                    background.pixelsPerUnitMultiplier = 2.5f;
                }
                background.color = new Color(0.09f, 0.13f, 0.23f, 0.99f);
                background.raycastTarget = false;

                var title = MakeLabel(native, ui.card, "Hotkeys title", "Editor shortcuts", 36);
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontStyle = FontStyles.Bold;
                Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(34, -80), new Vector2(-176, -18));

                var left = MakeLabel(native, ui.card, "Part and fuselage shortcuts",
                    Section("Parts") +
                    Key(focusKey, "Toggle camera focus") +
                    Key("Shift + click", "Add or remove a part") +
                    Key("A / Alt + A", "Select all / clear selection") +
                    Key("B then drag", "Box select. Shift adds, Ctrl subtracts") +
                    Key("V / I", "Hide selection / show hidden parts") +
                    Key("Ctrl + C / Ctrl + V", "Copy active part / paste and place") +
                    Key("Ctrl + D", "Duplicate active part and move") +
                    Key("Backspace / Delete", "Delete active part") +
                    "\n" + Section("Fuselage") +
                    Key("S", "Scale fuselages and their spacing") +
                    Key("Drag a shape handle", "Resize, round or hollow") +
                    Key("Shift", "Fine adjustment") +
                    Key("Type a value", "Metres, percent or scale factor"), 24);
                Stretch(left.rectTransform, Vector2.zero, new Vector2(0.5f, 1), new Vector2(34, 30), new Vector2(-16, -94));

                var right = MakeLabel(native, ui.card, "Transform shortcuts",
                    Section("Move & rotate") +
                    Key("G / R", "Move / rotate the selection") +
                    Key("Drag a move arrow", "Hover an edge or corner to align on that axis") +
                    Key("R again", "Trackball rotation") +
                    Key("Drag a square", "Move in the plane between two arrows") +
                    Key("X / Y / Z", "Start on an axis in Move or Rotate. Repeat for local") +
                    Key("G, X, Y", "Move in XY. X + Z and Y + Z work too") +
                    Key("Middle mouse", "Choose an axis by mouse direction") +
                    Key("Shift / Ctrl / both", "Fine adjustment / snap / fine snap") +
                    "\n" + Section("Exact values") +
                    Key("Type a value", "Move in metres or rotate in degrees") +
                    Key("Minus / Slash", "Toggle sign / reciprocal") +
                    Key("Tab / Ctrl + Tab", "Next / previous value") +
                    Key("Backspace", "Erase a digit") +
                    Key("Enter / left click", "Apply. Release to finish a mouse drag") +
                    Key("Esc / right click", "Cancel"), 24);
                Stretch(right.rectTransform, new Vector2(0.5f, 0), Vector2.one, new Vector2(16, 30), new Vector2(-34, -94));

                var close = HidePartsUI.CreateButton(native, "EditorTools Close Hotkeys", "Close [Esc]", 148, false,
                    () => ui.SetOpen(false), out _, out _, withIcon: false, parent: ui.card);
                var closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
                closeRect.anchoredPosition = new Vector2(-24, -24);
                return ui;
            }
            return null;
        }

        private static string Section(string title) => "<color=#AFC5FF><b>" + title + "</b></color>\n";
        private static string Key(string shortcut, string action) => "<color=#FFC27A><b>" + shortcut + "</b></color>   " + action + "\n";

        internal static TextMeshProUGUI MakeLabel(MirrorButton native, RectTransform parent, string name, string text, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.font = native.text.font;
            label.fontSharedMaterial = native.text.font.material;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10;
            label.fontSizeMax = size;
            label.fontSize = size;
            label.lineSpacing = 9;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 paddingMin, Vector2 paddingMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = paddingMin;
            rect.offsetMax = paddingMax;
        }

        private void FitCard()
        {
            var viewport = ((RectTransform)root.transform).rect;
            card.sizeDelta = new Vector2(Mathf.Min(1230, viewport.width * 0.92f), Mathf.Min(744, viewport.height * 0.88f));
        }

        internal void RefreshButton(bool visible, bool interactable)
        {
            if (!IsAlive) return;
            openButtonRoot.SetActive(visible);
            openButton.interactable = interactable;
            if (IsOpen) FitCard();
        }

        internal bool ContainsButton(Vector2 point) => IsAlive && openButtonRoot.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)openButtonRoot.transform, point,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);

        internal void SetOpen(bool open)
        {
            if (!IsAlive) return;
            if (open)
            {
                root.transform.SetAsLastSibling();
                FitCard();
            }
            root.SetActive(open);
        }

        public void Dispose()
        {
            SetOpen(false);
            if (root != null) UnityEngine.Object.Destroy(root);
            if (openButtonRoot != null) UnityEngine.Object.Destroy(openButtonRoot);
        }
    }
}

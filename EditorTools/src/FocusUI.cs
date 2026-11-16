using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    // Copy visual properties only, never native button listeners or gameplay scripts.
    internal sealed class FocusUI
    {
        private GameObject root;
        private RectTransform rect;
        private Canvas canvas;
        private Button button;
        private Image background;
        private TextMeshProUGUI label;
        private Color focusedColor;
        private Color unfocusedColor;
        internal bool IsAlive => root != null && canvas != null;

        internal static FocusUI TryCreate(Action toggle)
        {
            foreach (var template in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (template.buttonImage == null || template.text == null || template.text.font == null) continue;
                var parentCanvas = template.GetComponentInParent<Canvas>();
                if (parentCanvas == null || !parentCanvas.isActiveAndEnabled) continue;
                return Create(template, parentCanvas.rootCanvas, toggle);
            }
            return null;
        }

        private static FocusUI Create(MirrorButton template, Canvas canvas, Action toggle)
        {
            var ui = new FocusUI { canvas = canvas, focusedColor = template.selectedColor, unfocusedColor = template.deselectedColor };
            ui.root = new GameObject("EditorTools Camera Mode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            ui.rect = (RectTransform)ui.root.transform;
            // Join the existing bottom toolbar so its layout and CanvasScaler own
            // placement, spacing and resolution scaling just like the other buttons.
            ui.rect.SetParent(template.transform.parent, false);
            float height = Mathf.Max(38, template.buttonImage.rectTransform.rect.height);
            ui.rect.sizeDelta = new Vector2(260, height);
            ui.root.layer = template.gameObject.layer;
            var layout = ui.root.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 260;
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = layout.flexibleHeight = 0;

            ui.background = ui.root.GetComponent<Image>();
            var nativeImage = template.buttonImage;
            ui.background.sprite = nativeImage.sprite;
            ui.background.type = nativeImage.type;
            ui.background.material = nativeImage.material;
            ui.background.pixelsPerUnitMultiplier = nativeImage.pixelsPerUnitMultiplier;
            ui.background.raycastTarget = true;
            ui.button = ui.root.GetComponent<Button>();
            ui.button.targetGraphic = ui.background;
            ui.button.navigation = new Navigation { mode = Navigation.Mode.None };
            ui.button.transition = Selectable.Transition.ColorTint;
            var nativeButton = template.GetComponent<Button>();
            if (nativeButton != null && nativeButton.transition == Selectable.Transition.ColorTint)
                ui.button.colors = nativeButton.colors;
            ui.button.onClick.AddListener(() => toggle());

            var textObject = new GameObject("Mode", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = ui.root.layer;
            textObject.transform.SetParent(ui.rect, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 3);
            textRect.offsetMax = new Vector2(-12, -3);
            ui.label = textObject.GetComponent<TextMeshProUGUI>();
            ui.label.font = template.text.font;
            ui.label.fontSharedMaterial = template.text.fontSharedMaterial;
            ui.label.fontStyle = template.text.fontStyle;
            ui.label.fontSize = template.text.fontSize;
            ui.label.enableAutoSizing = true;
            ui.label.fontSizeMin = 12;
            ui.label.fontSizeMax = ui.label.fontSize;
            ui.label.alignment = TextAlignmentOptions.Center;
            ui.label.textWrappingMode = TextWrappingModes.NoWrap;
            ui.label.raycastTarget = false;
            ui.Refresh(true, false, false, "F");
            return ui;
        }

        internal void Refresh(bool visible, bool focused, bool interactable, string shortcut)
        {
            if (!IsAlive) return;
            root.SetActive(visible);
            button.interactable = interactable;
            background.color = focused ? focusedColor : unfocusedColor;
            label.color = focused ? Color.white : Color.black;
            label.text = (focused ? "Focused" : "Unfocused") + "  [" + shortcut + "]";
        }

        internal bool Contains(Vector2 position) => IsAlive && root.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(rect, position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);

        internal void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}

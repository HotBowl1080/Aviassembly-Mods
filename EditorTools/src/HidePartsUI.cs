using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    public sealed class HidePartsUI : IDisposable
    {
        private GameObject hideRoot, showRoot;
        private Button hideButton, showButton;
        private TMP_Text showLabel;
        private Canvas canvas;
        public bool IsAlive => hideRoot != null && showRoot != null && canvas != null;

        public static HidePartsUI TryCreate(HideParts owner)
        {
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = native.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled || native.buttonImage == null || native.text == null || native.text.font == null) continue;
                var ui = new HidePartsUI { canvas = canvas.rootCanvas };
                TMP_Text hideText;
                ui.hideRoot = CreateButton(native, "EditorTools Hide Part", "Hide [V]", 180, true, () => owner.HideSelected(), out ui.hideButton, out hideText);
                ui.showRoot = CreateButton(native, "EditorTools Show Parts", "Show All (0) [I]", 250, false, owner.ShowAllFromUI, out ui.showButton, out ui.showLabel);
                ui.Refresh(true, false, 0, false);
                return ui;
            }
            return null;
        }

        private static GameObject CreateButton(MirrorButton native, string name, string text, float width, bool crossedEye, UnityEngine.Events.UnityAction action, out Button button, out TMP_Text label)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.layer = native.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(EditorToolRow.Get(native), false);
            float height = Mathf.Max(38, native.buttonImage.rectTransform.rect.height);
            rect.sizeDelta = new Vector2(width, height);
            var layout = root.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = layout.flexibleWidth = 0;
            var image = root.GetComponent<Image>();
            image.sprite = native.buttonImage.sprite;
            image.type = native.buttonImage.type;
            image.material = native.buttonImage.material;
            image.pixelsPerUnitMultiplier = native.buttonImage.pixelsPerUnitMultiplier;
            image.color = native.deselectedColor;
            button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            var iconObject = new GameObject(crossedEye ? "Hide eye icon" : "Show eye icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(HidePartsEyeIcon));
            iconObject.layer = root.layer;
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(27, 0);
            iconRect.sizeDelta = new Vector2(32, 32);
            var icon = iconObject.GetComponent<HidePartsEyeIcon>();
            icon.Crossed = crossedEye;
            icon.color = Color.black;
            icon.raycastTarget = false;
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = root.layer;
            textObject.transform.SetParent(rect, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(48, 3);
            textRect.offsetMax = new Vector2(-12, -3);
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = native.text.font;
            label.fontSharedMaterial = native.text.fontSharedMaterial;
            label.fontStyle = native.text.fontStyle;
            label.fontSize = native.text.fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = native.text.fontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
            label.text = text;
            return root;
        }

        public void Refresh(bool visible, bool canHide, int count, bool canInteract = true)
        {
            if (!IsAlive) return;
            hideRoot.SetActive(visible);
            showRoot.SetActive(visible);
            hideButton.interactable = canHide;
            showButton.interactable = canInteract && count > 0;
            showLabel.text = "Show All (" + count + ") [I]";
        }

        public bool Contains(Vector2 position)
        {
            if (!IsAlive) return false;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return (hideRoot.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)hideRoot.transform, position, camera)) ||
                (showRoot.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)showRoot.transform, position, camera));
        }

        public void Dispose()
        {
            if (hideRoot != null) UnityEngine.Object.Destroy(hideRoot);
            if (showRoot != null) UnityEngine.Object.Destroy(showRoot);
        }
    }

    /// <summary>Small monochrome UI geometry, using the native button's black icon treatment.</summary>
    public sealed class HidePartsEyeIcon : MaskableGraphic
    {
        public bool Crossed;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments, b = (float)(i + 1) / segments;
                var topA = new Vector2(-13 + 26 * a, Mathf.Sin(a * Mathf.PI) * 7);
                var topB = new Vector2(-13 + 26 * b, Mathf.Sin(b * Mathf.PI) * 7);
                Stroke(mesh, topA, topB, 2);
                Stroke(mesh, new Vector2(topA.x, -topA.y), new Vector2(topB.x, -topB.y), 2);
                float aa = a * Mathf.PI * 2, bb = b * Mathf.PI * 2;
                Stroke(mesh, new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * 3.3f,
                    new Vector2(Mathf.Cos(bb), Mathf.Sin(bb)) * 3.3f, 2);
            }
            if (Crossed) Stroke(mesh, new Vector2(-12, 11), new Vector2(12, -11), 2.5f);
        }
        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            Vector2 delta = b - a;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - side, color, Vector2.zero); mesh.AddVert(a + side, color, Vector2.zero);
            mesh.AddVert(b + side, color, Vector2.zero); mesh.AddVert(b - side, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}

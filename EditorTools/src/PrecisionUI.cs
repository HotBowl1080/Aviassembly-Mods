using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    // Context only; never takes input focus or intercepts editor clicks.
    internal sealed class PrecisionUI : IDisposable
    {
        private GameObject root;
        private TextMeshProUGUI label;
        private PrecisionController controller;
        internal bool IsAlive => root != null;
        internal static PrecisionUI TryCreate(PrecisionController owner)
        {
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = native.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled || native.text == null || native.text.font == null || native.buttonImage == null) continue;
                var ui = new PrecisionUI { controller = owner };
                ui.root = new GameObject("EditorTools Precision Readout", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                ui.root.layer = native.gameObject.layer;
                var rect = (RectTransform)ui.root.transform; rect.SetParent(canvas.rootCanvas.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-32, -150); rect.sizeDelta = new Vector2(660, 72);
                ui.label = ui.root.GetComponent<TextMeshProUGUI>();
                ui.label.font = native.text.font;
                ui.label.fontSharedMaterial = native.text.font.material;
                ui.label.fontSize = 32; ui.label.fontStyle = FontStyles.Bold; ui.label.color = Color.white;
                ui.label.alignment = TextAlignmentOptions.MidlineRight;
                ui.label.textWrappingMode = TextWrappingModes.NoWrap;
                ui.label.overflowMode = TextOverflowModes.Overflow;
                // Width creates the material instance; assign color afterwards so
                // the native font's transparent outline cannot replace it.
                ui.label.outlineWidth = 0.3f; ui.label.outlineColor = Color.black;
                ui.label.fontMaterial.SetColor("_OutlineColor", Color.black);
                ui.label.fontMaterial.SetFloat("_OutlineWidth", 0.3f);
                ui.label.UpdateMeshPadding();
                ui.label.raycastTarget = false;
                ui.root.SetActive(false); return ui;
            }
            return null;
        }
        internal void Refresh(bool visible, BuildingPart selected, bool editable)
        {
            if (!IsAlive) return;
            root.SetActive(visible && selected != null && controller.HasReadout);
            if (!root.activeSelf) return;
            label.text = controller.ReadoutLine;
        }
        internal bool Contains(Vector2 position) => false;
        internal void EndEdit() { }
        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    }
}

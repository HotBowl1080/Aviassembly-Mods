using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    internal enum SnapReference { Edge, Center }

    [HarmonyPatch(typeof(SnappingButton), nameof(SnappingButton.Press))]
    internal static class MagnetMenuPatch
    {
        private static bool Prefix(SnappingButton __instance) => Plugin.Instance?.OpenSnapMenu(__instance) != true;
    }

    internal sealed class SnapReferenceMenu
    {
        private GameObject root;
        private SnappingButton magnet;
        internal bool IsAlive => root != null && magnet != null && magnet.isActiveAndEnabled;

        internal static SnapReferenceMenu Create(SnappingButton magnet, Plugin host)
        {
            var canvas = magnet.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || magnet.buttonImage == null) return null;
            var native = canvas.GetComponentInChildren<MirrorButton>(true);
            if (native == null || native.buttonImage == null || native.text?.font == null) return null;

            var menu = new SnapReferenceMenu { magnet = magnet };
            menu.root = new GameObject("EditorTools Snap Reference", typeof(RectTransform), typeof(Image), typeof(Button));
            menu.root.layer = native.gameObject.layer;
            var overlay = (RectTransform)menu.root.transform;
            overlay.SetParent(canvas.transform, false);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var dismiss = menu.root.GetComponent<Button>();
            var backdrop = menu.root.GetComponent<Image>();
            backdrop.color = Color.clear;
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(host.CloseSnapMenu);
            menu.root.AddComponent<PrecisionInputBlocker>();

            var panel = new GameObject("Snap options", typeof(RectTransform), typeof(Image));
            panel.layer = menu.root.layer;
            var card = (RectTransform)panel.transform;
            card.SetParent(overlay, false);
            card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
            card.sizeDelta = new Vector2(342, 108);
            var anchor = magnet.buttonImage.rectTransform;
            var top = (Vector2)overlay.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMax))) - overlay.rect.min;
            card.anchoredPosition = new Vector2(Mathf.Clamp(top.x, 0, Mathf.Max(0, overlay.rect.width - card.sizeDelta.x)),
                Mathf.Clamp(top.y + 6, 0, Mathf.Max(0, overlay.rect.height - card.sizeDelta.y)));
            panel.GetComponent<Image>().color = new Color(0.085f, 0.105f, 0.15f, 0.98f);

            var title = ShortcutsUI.MakeLabel(native, card, "Snap reference label", "Snap from", 25);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(12, -44); titleRect.offsetMax = new Vector2(-12, -2);
            void Option(string name, int column, SnapReference? value)
            {
                var option = HidePartsUI.CreateButton(native, "Snap " + name, name, 100, false,
                    () => host.SelectSnapReference(magnet, value), out var button, out var label, withIcon: false, parent: card);
                var rect = (RectTransform)option.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(100, 48);
                rect.anchoredPosition = new Vector2(12 + column * 109, 10);
                bool selected = PartPlacer.snapMode ? value == host.Reference : !value.HasValue;
                button.targetGraphic.color = selected ? native.selectedColor : native.deselectedColor;
                label.color = selected ? Color.white : Color.black;
            }
            Option("Center", 0, SnapReference.Center);
            Option("Edge", 1, SnapReference.Edge);
            Option("Off", 2, null);
            return menu;
        }

        internal void Dispose()
        {
            if (root == null) return;
            root.SetActive(false);
            Object.Destroy(root);
        }
    }
}

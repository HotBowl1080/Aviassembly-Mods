using UnityEngine;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    // A second row preserves the native controls and right-hand aircraft overlays.
    internal sealed class EditorToolRow : MonoBehaviour
    {
        internal static Transform Get(MirrorButton template)
        {
            var nativeRow = (RectTransform)template.transform.parent;
            var parent = (RectTransform)nativeRow.parent;
            var existing = parent.Find("EditorTools Actions");
            if (existing != null) return existing;
            var go = new GameObject("EditorTools Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(EditorToolRow));
            go.layer = template.gameObject.layer;
            var row = (RectTransform)go.transform; row.SetParent(parent, false);
            row.anchorMin = row.anchorMax = row.pivot = Vector2.zero;
            var corners = new Vector3[4]; nativeRow.GetWorldCorners(corners);
            Vector2 bottom = parent.InverseTransformPoint(corners[0]);
            float height = Mathf.Max(38, template.buttonImage.rectTransform.rect.height);
            row.anchoredPosition = bottom - parent.rect.min + Vector2.up * (height + 12);
            row.sizeDelta = new Vector2(800, height);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            var nativeLayout = nativeRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = nativeLayout != null ? nativeLayout.spacing : 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }
        private void LateUpdate() { if (transform.childCount == 0) Destroy(gameObject); }
    }
}

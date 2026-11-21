using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MachWorks.BackCountryOperations
{
    internal static class WorkshopSettingsUi
    {
        internal const float Width = 256f;
        internal const float RowHeight = 41f;

        internal static EngineSettings StockControls =>
            PartPrefabs.GetPartPrefab("WoodEngine").GetComponentInChildren<Engine>(true)
                .settingsPrefab.GetComponent<EngineSettings>();

        internal static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static TMP_Text Label(Transform parent, string caption, float top, float height = 33f,
            bool detail = false, float left = 12f, float width = Width - 24f)
        {
            var stock = StockControls.invertToggle.transform.parent.Find("InvertLabel").GetComponent<TMP_Text>();
            var label = new GameObject("Setting label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            Place(label.rectTransform, left, top, width, height);
            label.font = stock.font;
            label.fontSharedMaterial = stock.fontSharedMaterial;
            label.fontSize = detail ? 19f : stock.fontSize;
            label.fontStyle = stock.fontStyle;
            label.characterSpacing = stock.characterSpacing;
            label.color = stock.color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = caption;
            return label;
        }

        internal static Toggle Checkbox(Transform parent, string caption, float top, UnityAction<bool> changed)
        {
            var toggle = Object.Instantiate(StockControls.invertToggle, parent, false);
            toggle.name = caption;
            toggle.group = null;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(changed);
            Place((RectTransform)toggle.transform, 12f, top, Width - 24f, 34f);
            if (toggle.targetGraphic)
                Place(toggle.targetGraphic.rectTransform, 0f, 0f, 34f, 34f);
            Label(toggle.transform, caption, 0f, 34f, left: 44f, width: Width - 68f).raycastTarget = true;
            return toggle;
        }

        internal static void SetCheckbox(Toggle toggle, bool selected, bool available = true)
        {
            toggle.SetIsOnWithoutNotify(selected);
            toggle.interactable = available;
            toggle.GetComponentInChildren<TMP_Text>().alpha = available || selected ? 1f : .48f;
        }
    }
}

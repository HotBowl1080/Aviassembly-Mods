using System;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MachWorks.BackCountryOperations
{
    internal sealed class TitleScreenBrand : IDisposable
    {
        private const string LabelName = "BackCountry Operations title";
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".title");
        private readonly ManualLogSource logger;
        private static TitleScreenBrand active;
        private GameObject badge;
        private RectTransform subtitle;
        private RectTransform menuRect;
        private Canvas rootCanvas;
        private MainMenu stockMenu;

        private TitleScreenBrand(ManualLogSource logger)
        {
            this.logger = logger;
        }

        internal static TitleScreenBrand Install(ManualLogSource logger)
        {
            var brand = new TitleScreenBrand(logger);
            try
            {
                var start = AccessTools.DeclaredMethod(typeof(MainMenu), "Start", Type.EmptyTypes);
                if (start == null)
                    throw new MissingMethodException(nameof(MainMenu), "Start");

                brand.harmony.Patch(start, postfix: new HarmonyMethod(typeof(TitleScreenBrand), nameof(AfterMenuStart)));
                active = brand;
                return brand;
            }
            catch (Exception error)
            {
                logger.LogWarning($"Could not enable the BackCountry title label: {error.Message}");
                return null;
            }
        }

        private static void AfterMenuStart(MainMenu __instance)
        {
            active?.AddBadge(__instance);
        }

        private void AddBadge(MainMenu instance)
        {
            var menu = instance.mainMenu;
            if (menu == null || badge != null)
                return;

            try
            {
                var canvas = menu.GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;

                rootCanvas = canvas.rootCanvas;
                menuRect = rootCanvas.transform as RectTransform;
                if (menuRect == null)
                    return;
                stockMenu = instance;
                var template = menu.GetComponentInChildren<TMP_Text>(true);
                badge = new GameObject(LabelName, typeof(RectTransform));
                subtitle = (RectTransform)badge.transform;
                subtitle.SetParent(menuRect, false);

                var font = template != null ? template.font : null;
                AddText("Subtitle shadow", font, new Color(0.04f, 0.20f, 0.32f, 0.85f), new Vector2(2f, -3f));
                AddText("Subtitle", font, new Color(1f, 0.92f, 0.74f, 1f), Vector2.zero);
                AddStripe("Left wing stripe", 0f, 0.085f);
                AddStripe("Right wing stripe", 0.915f, 1f);
                PositionSubtitle();
                Canvas.willRenderCanvases -= PositionSubtitle;
                Canvas.willRenderCanvases += PositionSubtitle;
                logger.LogInfo("BackCountry Operations title subtitle added.");
            }
            catch (Exception error)
            {
                if (badge != null)
                    UnityEngine.Object.Destroy(badge);
                badge = null;
                logger.LogWarning($"Could not add the BackCountry title label: {error.Message}");
            }
        }

        private void AddText(string name, TMP_FontAsset font, Color color, Vector2 offset)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(subtitle, false);
            label.rectTransform.anchorMin = new Vector2(0.105f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.895f, 1f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = offset;
            if (font != null)
                label.font = font;
            label.text = BackCountryOperationsPlugin.PluginName;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 6f;
            label.color = color;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 74f;
            label.raycastTarget = false;
        }

        private void AddStripe(string name, float left, float right)
        {
            var stripe = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            stripe.rectTransform.SetParent(subtitle, false);
            stripe.rectTransform.anchorMin = new Vector2(left, 0.46f);
            stripe.rectTransform.anchorMax = new Vector2(right, 0.54f);
            stripe.rectTransform.offsetMin = stripe.rectTransform.offsetMax = Vector2.zero;
            stripe.color = new Color(1f, 0.76f, 0.40f, 0.95f);
            stripe.raycastTarget = false;
        }

        private void PositionSubtitle()
        {
            if (subtitle == null || menuRect == null || rootCanvas == null || stockMenu == null)
                return;

            var show = (stockMenu.mainMenu != null && stockMenu.mainMenu.activeInHierarchy) ||
                (stockMenu.playGameMenu != null && stockMenu.playGameMenu.activeInHierarchy) ||
                (stockMenu.loadPanel != null && stockMenu.loadPanel.activeInHierarchy);
            if (badge.activeSelf != show)
                badge.SetActive(show);
            if (!show)
                return;

            // The stock logo and button group use different parents. Position in the canvas viewport.
            var viewport = rootCanvas.pixelRect;
            var scale = Mathf.Min(viewport.width / 1920f, viewport.height / 1080f);
            var center = new Vector2(viewport.center.x, viewport.yMin + viewport.height * 0.75f);
            var halfSize = new Vector2(348f, 29f) * scale;
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(menuRect, center - halfSize, camera, out var lower) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(menuRect, center + halfSize, camera, out var upper))
            {
                var localPosition = (Vector3)((lower + upper) * 0.5f);
                var size = upper - lower;
                if (subtitle.localPosition != localPosition)
                    subtitle.localPosition = localPosition;
                if (subtitle.sizeDelta != size)
                    subtitle.sizeDelta = size;
            }
        }

        public void Dispose()
        {
            Canvas.willRenderCanvases -= PositionSubtitle;
            if (active == this)
                active = null;
            if (badge != null)
                UnityEngine.Object.Destroy(badge);
            harmony.UnpatchSelf();
        }
    }
}

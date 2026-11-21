using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace Aviassembly.EditorTools
{
    // Preview only: native propeller sweep against this aircraft's colliders.
    internal sealed class PropClearance : IDisposable
    {
        private readonly Plugin host;
        private readonly HiddenXray highlights = new HiddenXray(new Color(1f, 0.15f, 0.08f, 0.25f), "EditorTools prop obstructions");
        private readonly System.Collections.Generic.List<Renderer> highlightRenderers = new System.Collections.Generic.List<Renderer>();
        internal int HighlightDrawCount => highlights.DrawCount;
        private GameObject probeObject, buttonRoot, textRoot;
        private MeshCollider probe;
        private Mesh mesh;
        private Vector3 builtU, builtV;
        private LineRenderer ring;
        private Material lineMaterial;
        private TMP_Text buttonLabel, label;
        private Button button;
        private Canvas canvas;
        private PlacementToolbar partMenu;
        private float nextCheck;
        private BuildingPart lastPart;
        private PartPlacer currentPlacer;
        private bool CanToggle => host.PrecisionContextValid && !host.TextEntryActive && host.Precision?.BlockGameInput != true &&
            host.ShapeEditor?.BlockInput != true && host.Selection?.BlockInput != true && currentPlacer != null && currentPlacer.currentMovingPart == null && FindProp(currentPlacer.currentSelectedPart) != null;
        internal bool Enabled { get; private set; } = true;
        internal bool HasProp { get; private set; }
        internal int Obstructions { get; private set; }
        internal float Gap { get; private set; } = float.PositiveInfinity;
        internal string Readout { get; private set; } = "";
        private const int Segments = 48;
        private const float SearchDistance = 2f;
        private const float HalfThickness = 0.01f;

        internal PropClearance(Plugin host) { this.host = host; }
        internal void Toggle()
        {
            if (!CanToggle) return;
            Enabled = !Enabled; nextCheck = 0;
            if (!Enabled) Clear();
        }
        internal static RotorCollider FindProp(BuildingPart part)
        {
            if (part == null) return null;
            foreach (var rotor in part.GetComponentsInChildren<RotorCollider>(true))
                if (!rotor.helicopter && rotor.rotorLength > 0 && rotor.GetComponentInParent<BuildingPart>() == part)
                    return rotor;
            return null;
        }
        internal void Tick(PartPlacer placer, bool force = false)
        {
            currentPlacer = placer;
            bool visible = host.InEditor && !GameManager.Instance.modalsOpen && !host.ShortcutsOpen;
            if (visible) EnsureUI();
            var part = placer != null ? placer.currentSelectedPart : null;
            if (buttonRoot != null)
            {
                var menuTarget = partMenu != null ? (GameObject)AccessTools.Field(typeof(PlacementToolbar), "targetObject").GetValue(partMenu) : null;
                buttonRoot.SetActive(visible && part != null && FindProp(part) != null && !host.HideParts.IsHidden(part) &&
                    placer.currentMovingPart == null && menuTarget == part.gameObject);
                button.interactable = visible && CanToggle;
                buttonLabel.text = Enabled ? "All Props\nCheck On" : "All Props\nCheck Off";
                buttonLabel.color = Enabled ? new Color(0.3f, 0.9f, 1f) : Color.white;
                // Blocking the world click also skips PartPlacer's menu heartbeat.
                // Keep the existing menu alive throughout a press on our button.
                KeepMenuAlive(partMenu);
            }
            if (!visible || !Enabled || part == null || host.HideParts.IsHidden(part)) { Clear(); return; }
            if (!force && part == lastPart && Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.1f; lastPart = part;
            var rotor = FindProp(part);
            if (rotor == null) { Clear(); return; }
            HasProp = true;
            Vector3 center = rotor.transform.TransformPoint(rotor.rotorPosition);
            Vector3 u = rotor.transform.TransformVector(Vector3.up) * rotor.rotorLength;
            Vector3 v = rotor.transform.TransformVector(Vector3.forward) * rotor.rotorLength;
            Vector3 normal = Vector3.Cross(u, v).normalized;
            if (normal.sqrMagnitude < 0.9f) { Clear(); return; }
            EnsureProbe();
            BuildSweep(u, v, normal);
            probeObject.transform.position = center;
            if (PlaneContainer.Instance != null && probeObject.scene != PlaneContainer.Instance.gameObject.scene)
                SceneManager.MoveGameObjectToScene(probeObject, PlaneContainer.Instance.gameObject.scene);
            Physics.SyncTransforms();
            Obstructions = 0; Gap = float.PositiveInfinity;
            float radius = Mathf.Max(u.magnitude, v.magnitude);
            var plane = PlaneContainer.Instance;
            var hitParts = new System.Collections.Generic.HashSet<BuildingPart>();
            if (plane != null)
            foreach (var obstacle in plane.GetComponentsInChildren<Collider>())
            {
                var owner = obstacle.GetComponentInParent<BuildingPart>();
                if (owner == null || owner == part || !obstacle.enabled || obstacle.isTrigger) continue;
                if (Vector3.Distance(obstacle.bounds.ClosestPoint(center), center) > radius + SearchDistance) continue;
                bool overlap;
                // Unity's query requires an enabled collider in the same physics
                // scene. Enable only synchronously around the query, never a frame.
                try
                {
                    probe.enabled = true;
                    overlap = Physics.ComputePenetration(probe, center, Quaternion.identity, obstacle,
                        obstacle.transform.position, obstacle.transform.rotation, out var direction, out var depth);
                }
                finally { probe.enabled = false; }
                if (!overlap && obstacle is MeshCollider nonConvex && !nonConvex.convex)
                {
                    // Native prop damage also uses radial linecasts. Check both
                    // directions to handle backfaces and rays starting inside.
                    for (int i = 0; i < 192 && !overlap; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 192;
                        var offset = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                        overlap = obstacle.Raycast(new Ray(center, offset.normalized), out var hit, offset.magnitude) ||
                            obstacle.Raycast(new Ray(center + offset, -offset.normalized), out hit, offset.magnitude);
                    }
                }
                if (overlap)
                { hitParts.Add(owner); Gap = 0; continue; }
                // Sample the whole sweep, not just the tips. This is an estimate,
                // not a certified minimum gap or a guarantee against flight damage.
                for (int radial = 0; radial <= 6; radial++)
                    for (int angular = 0; angular < (radial == 0 ? 1 : Segments); angular++)
                    {
                        float angle = angular * Mathf.PI * 2 / Segments;
                        Vector3 point = center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * (radial / 6f);
                        // Non-convex mesh colliders do not support ClosestPoint;
                        // bounds give a conservative estimate without log spam.
                        var meshObstacle = obstacle as MeshCollider;
                        var closest = meshObstacle != null && !meshObstacle.convex ? obstacle.bounds.ClosestPoint(point) : obstacle.ClosestPoint(point);
                        Gap = Mathf.Min(Gap, Vector3.Distance(point, closest));
                    }
            }
            Obstructions = hitParts.Count;
            highlightRenderers.Clear();
            foreach (var obstruction in hitParts)
                foreach (var renderer in obstruction.GetComponentsInChildren<Renderer>())
                    if (renderer.GetComponentInParent<BuildingPart>() == obstruction && host.HideParts.WasVisible(renderer)) highlightRenderers.Add(renderer);
            highlights.Render(BuildingCamera.cam, highlightRenderers, true);
            var color = Obstructions > 0 ? new Color(1f, 0.3f, 0.25f) : Gap < 0.1f ? new Color(1f, 0.75f, 0.2f) : new Color(0.3f, 0.9f, 1f);
            if (ring != null)
            {
                ring.gameObject.SetActive(true); ring.startColor = ring.endColor = color;
                ring.startWidth = ring.endWidth = Mathf.Clamp(radius * 0.007f, 0.008f, 0.03f);
                for (int i = 0; i < ring.positionCount; i++)
                { float a = i * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(i, center + u * Mathf.Cos(a) + v * Mathf.Sin(a)); }
            }
            Readout = Obstructions > 0 ? "Prop sweep: " + Obstructions + " obstruction" + (Obstructions == 1 ? "" : "s") :
                "Prop sweep: no overlap detected | Approx gap: " + (Gap > SearchDistance ? "> 2" : Gap.ToString("0.000", CultureInfo.InvariantCulture)) + " m";
            if (label != null) { textRoot.SetActive(true); label.text = Readout; label.color = color; }
        }
        private void EnsureProbe()
        {
            if (probe != null) return;
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (ring != null) UnityEngine.Object.Destroy(ring.gameObject);
            if (lineMaterial != null) UnityEngine.Object.Destroy(lineMaterial);
            // Separate from the aircraft and disabled between synchronous queries.
            probeObject = new GameObject("EditorTools prop sweep query");
            probeObject.layer = 2;
            probe = probeObject.AddComponent<MeshCollider>(); probe.convex = true; probe.isTrigger = true; probe.enabled = false;
            probe.cookingOptions = MeshColliderCookingOptions.None;
            mesh = new Mesh { name = "EditorTools prop sweep", hideFlags = HideFlags.HideAndDontSave };
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var go = new GameObject("EditorTools prop sweep ring") { hideFlags = HideFlags.HideAndDontSave };
            ring = go.AddComponent<LineRenderer>(); ring.sharedMaterial = lineMaterial;
            ring.useWorldSpace = true; ring.loop = true; ring.positionCount = 96;
            ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
        }
        private void BuildSweep(Vector3 u, Vector3 v, Vector3 normal)
        {
            if (mesh.vertexCount > 0 && u == builtU && v == builtV) return;
            builtU = u; builtV = v;
            var vertices = new Vector3[Segments * 2 + 2]; var triangles = new int[Segments * 12];
            float inflation = 1f / Mathf.Cos(Mathf.PI / Segments);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2 / Segments;
                var p = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * inflation;
                vertices[i] = p - normal * HalfThickness; vertices[i + Segments] = p + normal * HalfThickness;
                int j = (i + 1) % Segments, t = i * 12;
                triangles[t] = Segments * 2; triangles[t + 1] = j; triangles[t + 2] = i;
                triangles[t + 3] = Segments * 2 + 1; triangles[t + 4] = i + Segments; triangles[t + 5] = j + Segments;
                triangles[t + 6] = i; triangles[t + 7] = j; triangles[t + 8] = j + Segments;
                triangles[t + 9] = i; triangles[t + 10] = j + Segments; triangles[t + 11] = i + Segments;
            }
            vertices[Segments * 2] = -normal * HalfThickness; vertices[Segments * 2 + 1] = normal * HalfThickness;
            probe.sharedMesh = null; mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
            Physics.BakeMesh(mesh.GetInstanceID(), true, probe.cookingOptions);
            probe.sharedMesh = mesh;
        }
        private void EnsureUI()
        {
            if (buttonRoot == null)
                foreach (var menu in UnityEngine.Object.FindObjectsByType<PlacementToolbar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var menuCanvas = menu.GetComponentInParent<Canvas>();
                    if (menuCanvas == null || !menuCanvas.isActiveAndEnabled || menu.moveButton == null) continue;
                    partMenu = menu;
                    buttonRoot = new GameObject("EditorTools Prop Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
                    buttonRoot.layer = menu.gameObject.layer;
                    var rect = (RectTransform)buttonRoot.transform; rect.SetParent(menu.moveButton.transform.parent, false);
                    float height = Mathf.Max(38, ((RectTransform)menu.moveButton.transform).rect.height);
                    rect.sizeDelta = new Vector2(108, height);
                    var layout = buttonRoot.GetComponent<LayoutElement>(); layout.preferredWidth = layout.minWidth = 108; layout.preferredHeight = layout.minHeight = height;
                    var background = buttonRoot.GetComponent<Image>(); background.color = new Color(1, 1, 1, 0.04f);
                    button = buttonRoot.GetComponent<Button>(); button.targetGraphic = background; button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(Toggle);
                    var text = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); text.layer = buttonRoot.layer; text.transform.SetParent(rect, false);
                    var tr = (RectTransform)text.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(4, 2); tr.offsetMax = new Vector2(-4, -2);
                    buttonLabel = text.GetComponent<TextMeshProUGUI>(); buttonLabel.fontSize = 18; buttonLabel.enableAutoSizing = true; buttonLabel.fontSizeMin = 12; buttonLabel.fontSizeMax = 18;
                    buttonLabel.alignment = TextAlignmentOptions.Center; buttonLabel.raycastTarget = false;
                    buttonRoot.SetActive(false); break;
                }
            if (textRoot != null) { if (buttonLabel != null) buttonLabel.font = label.font; return; }
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var parent = native.GetComponentInParent<Canvas>();
                if (parent == null || !parent.isActiveAndEnabled || native.text == null || native.text.font == null || native.buttonImage == null) continue;
                canvas = parent.rootCanvas;
                if (buttonLabel != null) buttonLabel.font = native.text.font;
                textRoot = new GameObject("EditorTools prop clearance text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textRoot.layer = native.gameObject.layer;
                var rect = (RectTransform)textRoot.transform; rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-32, -230); rect.sizeDelta = new Vector2(900, 64);
                label = textRoot.GetComponent<TextMeshProUGUI>(); label.font = native.text.font;
                label.fontSharedMaterial = native.text.font.material; label.fontSize = 24;
                label.alignment = TextAlignmentOptions.MidlineRight; label.raycastTarget = false;
                label.fontMaterial.SetColor("_OutlineColor", Color.black); label.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
                label.UpdateMeshPadding(); textRoot.SetActive(false); return;
            }
        }
        internal bool Contains(Vector2 point) => buttonRoot != null && buttonRoot.activeInHierarchy && canvas != null &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)buttonRoot.transform, point, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
        internal void KeepMenuAlive(PlacementToolbar menu)
        {
            if (menu != null && menu == partMenu && CanToggle && Mouse.current != null && Contains(Mouse.current.position.ReadValue()) &&
                (GameObject)AccessTools.Field(typeof(PlacementToolbar), "targetObject").GetValue(menu) == currentPlacer.currentSelectedPart.gameObject)
                menu.EnableToolbar(currentPlacer.currentSelectedPart.gameObject);
        }
        internal void Clear()
        {
            HasProp = false; Obstructions = 0; Gap = float.PositiveInfinity; Readout = ""; lastPart = null;
            highlights.Clear();
            if (ring != null) ring.gameObject.SetActive(false);
            if (textRoot != null) textRoot.SetActive(false);
        }
        public void Dispose()
        {
            Clear();
            highlights.Dispose();
            if (probeObject != null) UnityEngine.Object.Destroy(probeObject);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (ring != null) UnityEngine.Object.Destroy(ring.gameObject);
            if (lineMaterial != null) UnityEngine.Object.Destroy(lineMaterial);
            if (buttonRoot != null) UnityEngine.Object.Destroy(buttonRoot);
            if (textRoot != null) UnityEngine.Object.Destroy(textRoot);
        }
    }
    [HarmonyPatch(typeof(PlacementToolbar), "Update")]
    internal static class PropMenuHeartbeatPatch
    {
        // Run before the native menu calculates isEnabled, independent of which
        // LateUpdate runs first, while world picking is blocked over our button.
        private static void Prefix(PlacementToolbar __instance) => Plugin.Instance?.Clearance?.KeepMenuAlive(__instance);
    }
}

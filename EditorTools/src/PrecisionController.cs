using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Aviassembly.EditorTools
{
    public sealed class PrecisionInputBlocker : InputBlocker
    {
        private void OnEnable() { if (InputManager.Instance != null) InputManager.Instance.currentInputBlocker = this; }
        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
        private void Release() { if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker == this) InputManager.Instance.currentInputBlocker = null; }
    }
    internal sealed partial class PrecisionController : IDisposable
    {
        internal readonly TransformSession Session = new TransformSession();
        internal PrecisionUI UI;
        private readonly DragProjection projection = new DragProjection();
        internal Camera ProjectionCamera => Session.Active ? projection.Camera ?? BuildingCamera.cam : BuildingCamera.cam;
        internal bool BlockGameInput => Session.Active || Time.frameCount == consumedFrame;
        internal bool Modal { get; private set; }
        internal bool Rotating { get; private set; }
        internal int Axis { get; private set; } = -1;
        internal bool Local { get; private set; }
        internal bool PlaneLock { get; private set; }
        internal bool Trackball { get; private set; }
        internal bool ValidInput { get; private set; } = true;
        internal string Status { get; private set; } = "Select a placed part";
        private readonly Plugin host;
        private PartPlacer placer;
        private InputBlocker blocker;
        private Keyboard keyboard;
        private int consumedFrame = -1, tickFrame = -1, beginFrame;
        private Vector2 startMouse;
        private float lastMouseAngle, mouseAngle;
        private readonly string[] numbers = { "", "", "" };
        private readonly bool[] inverse = new bool[3];
        private int component;
        private bool numeric;
        private string queuedText = "";
        private readonly LineRenderer[] guides = new LineRenderer[3];
        private Material guideMaterial;

        internal PrecisionController(Plugin plugin) { host = plugin; }
        internal bool OwnsBlocker(InputBlocker value) => value != null && value == blocker;
        internal bool CanStart => host.CanStartPrecision && placer != null && placer.currentSelectedPart != null &&
            placer.currentSelectedPart.hasBeenPlaced && placer.currentMovingPart == null;

        internal void Tick(PartPlacer source)
        {
            placer = source;
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            if (!Session.Active && Time.frameCount != consumedFrame) ReleaseBlocker();
            if (keyboard != Keyboard.current)
            {
                if (keyboard != null) keyboard.onTextInput -= OnText;
                keyboard = Keyboard.current;
                if (keyboard != null) keyboard.onTextInput += OnText;
            }
            ValidateRecentAction();
            if (Session.Active && (!host.PrecisionContextValid || Session.Target == null ||
                source == null || source.currentSelectedPart != Session.Target || source.currentMovingPart != null || !Application.isFocused))
            { Cancel(); return; }
            if (!Application.isFocused || keyboard == null) return;
            ProcessInput();
        }
        internal void ProcessInput()
        {
            if (keyboard == null) return;
            if (Session.Active)
            {
                bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
                if (keyboard.escapeKey.wasPressedThisFrame || rightClick) { Cancel(); return; }
                if (handleOperation)
                {
                    if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
                    foreach (char c in queuedText) Type(c);
                    queuedText = "";
                    UpdateHandle();
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Apply();
                    else if (handleDragging && Mouse.current != null && !Mouse.current.leftButton.isPressed)
                    {
                        handleDragging = false;
                        if (!numeric) Apply();
                    }
                    else if (!handleDragging && Time.frameCount != beginFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Apply();
                    return;
                }
                if (keyboard.xKey.wasPressedThisFrame) Constrain(0, keyboard.shiftKey.isPressed);
                if (keyboard.yKey.wasPressedThisFrame) Constrain(1, keyboard.shiftKey.isPressed);
                if (keyboard.zKey.wasPressedThisFrame) Constrain(2, keyboard.shiftKey.isPressed);
                if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame && !Trackball)
                    ConstrainByMouse(Mouse.current.position.ReadValue(), keyboard.shiftKey.isPressed);
                if (Rotating && keyboard.rKey.wasPressedThisFrame && Time.frameCount != beginFrame)
                { Trackball = !Trackball; Axis = -1; PlaneLock = false; Local = false; }
                if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    int count = (Rotating && !Trackball) || (Axis >= 0 && !PlaneLock) ? 1 : (PlaneLock || Trackball ? 2 : 3);
                    component = (component + (keyboard.ctrlKey.isPressed ? count - 1 : 1)) % count;
                }
                foreach (char c in queuedText) Type(c);
                queuedText = "";
                UpdateMouse();
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                    (Time.frameCount != beginFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                     !(UI?.Contains(Mouse.current.position.ReadValue()) ?? false))) Apply();
            }
            else if (host.InEditor && !host.TextEntryActive && !BlockGameInput)
            {
                if (TryBeginHandleFromPointer()) return;
                if (queuedText.Length > 0)
                {
                    string input = queuedText; queuedText = "";
                    if (BeginRecentAdjustment()) { foreach (char c in input) Type(c); UpdateHandle(); return; }
                }
                if (keyboard.escapeKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) ClearRecentAction();
                if (!keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed && CanStart &&
                    !(MouseInput.Instance != null && MouseInput.Instance.PointerIsOverUI))
                {
                    if (keyboard.gKey.wasPressedThisFrame) Begin(false, true);
                    else if (keyboard.rKey.wasPressedThisFrame) Begin(true, true);
                }
            }
        }
        private void OnText(char c)
        {
            if (!IsNumberCharacter(c) || (keyboard != null && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed))) return;
            if ((Session.Active && Modal) || (!BlockGameInput && recent != null && CanStart && !host.TextEntryActive)) queuedText += c;
        }
        private static bool IsNumberCharacter(char c) => (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '/';
        internal bool Begin(bool rotation, bool modal, bool fromHandle = false)
        {
            bool allowed = fromHandle ? host.CanStartHandle && placer != null && placer.currentSelectedPart != null && placer.currentMovingPart == null : CanStart;
            if (!allowed || (rotation && placer.currentSelectedPart.lockRotation)) return false;
            if (!Session.Begin(placer)) return false;
            projection.Capture(BuildingCamera.cam);
            handleOperation = false; handleDragging = false; recent = null; toolHint = null;
            Rotating = rotation; Modal = modal; Axis = -1; Local = false; PlaneLock = false; Trackball = false;
            numeric = false; component = 0; queuedText = ""; ValidInput = true;
            readoutNumbers = Vector3.zero;
            for (int i = 0; i < 3; i++) { numbers[i] = ""; inverse[i] = false; }
            startMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            lastMouseAngle = Polar(startMouse); mouseAngle = 0; beginFrame = Time.frameCount;
            if (blocker == null) blocker = new GameObject("EditorTools Precision Input").AddComponent<PrecisionInputBlocker>();
            if (InputManager.Instance != null) InputManager.Instance.currentInputBlocker = blocker;
            if (modal && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            host.FocusOperation(Session.Target);
            Status = rotation ? "Rotate" : "Move";
            return true;
        }
        internal void ObserveSelection(PartPlacer source, BuildingPart next)
        {
            if (Session.Active && (source != placer || next != Session.Target)) Cancel();
            if (source == placer && !Session.Active) ClearRecentAction();
        }
        internal void Constrain(int axis, bool plane)
        {
            if (!Session.Active || !Modal || Trackball) return;
            plane &= !Rotating;
            if (Axis != axis || PlaneLock != plane) { Axis = axis; Local = false; PlaneLock = plane; }
            else if (!Local) Local = true;
            else { Axis = -1; Local = false; PlaneLock = false; }
            component = 0;
        }
        internal void Type(char c)
        {
            if (c >= '0' && c <= '9' || c == '.')
            {
                if (numbers[component].Length >= 32 || (c == '.' && numbers[component].Contains("."))) return;
                numbers[component] += c; numeric = true;
            }
            else if (c == '-') { numbers[component] = numbers[component].StartsWith("-") ? numbers[component].Substring(1) : "-" + numbers[component]; numeric = true; }
            else if (c == '/') { inverse[component] = !inverse[component]; numeric = true; }
        }
        internal void Backspace()
        {
            if (numbers[component].Length > 0) numbers[component] = numbers[component].Substring(0, numbers[component].Length - 1);
            else if (numeric) { numeric = false; inverse[component] = false; }
        }
        private Vector3 Direction(int i) => Local ? Session.StartRotation * TransformMath.Axis(i) : TransformMath.Axis(i);
        internal void ConstrainByMouse(Vector2 mouse, bool plane)
        {
            if (!Session.Active || ProjectionCamera == null) return;
            var cam = ProjectionCamera;
            Vector2 center = cam.WorldToScreenPoint(Session.StartPosition);
            Vector2 motion = (mouse - startMouse).normalized;
            int best = 0; float score = -1;
            for (int i = 0; i < 3; i++)
            {
                Vector2 projected = (Vector2)cam.WorldToScreenPoint(Session.StartPosition + Direction(i)) - center;
                float candidate = Mathf.Abs(Vector2.Dot(motion, projected.normalized));
                if (projected.sqrMagnitude > 0.01f && candidate > score) { score = candidate; best = i; }
            }
            Axis = best; PlaneLock = plane && !Rotating; component = 0;
        }
        private float Polar(Vector2 mouse)
        {
            var cam = ProjectionCamera;
            if (cam == null || Session.Target == null) return 0;
            Vector2 center = cam.WorldToScreenPoint(Session.StartPosition);
            var delta = mouse - center;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }
        private Vector3 PlanePoint(Vector2 mouse, Vector3 normal)
        {
            var ray = ProjectionCamera.ScreenPointToRay(mouse);
            var plane = new Plane(normal, Session.StartPosition);
            return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : Session.StartPosition;
        }
        private float AxisPoint(Vector2 mouse, Vector3 direction)
        {
            var cam = ProjectionCamera;
            var ray = cam.ScreenPointToRay(mouse);
            float b = Vector3.Dot(direction, ray.direction), denom = 1 - b * b;
            if (denom > 0.001f)
            {
                var w = ray.origin - Session.StartPosition;
                return (Vector3.Dot(direction, w) - b * Vector3.Dot(ray.direction, w)) / denom;
            }
            return mouse.y * (2 * Vector3.Distance(cam.transform.position, Session.StartPosition) * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Mathf.Max(1, cam.pixelHeight));
        }
        internal void UpdateMouse()
        {
            if (handleOperation) { UpdateHandle(); return; }
            if (!Session.Active || !Modal || ProjectionCamera == null) return;
            var cam = ProjectionCamera;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : startMouse;
            float currentAngle = Polar(mouse);
            mouseAngle += Mathf.DeltaAngle(lastMouseAngle, currentAngle); lastMouseAngle = currentAngle;
            bool fine = keyboard != null && keyboard.shiftKey.isPressed, snap = keyboard != null && keyboard.ctrlKey.isPressed;
            float factor = fine ? 0.1f : 1;
            var values = Vector3.zero; ValidInput = true;
            if (numeric)
            {
                for (int i = 0; i < 3; i++)
                {
                    float v = 0;
                    if (numbers[i] != "" && !TransformMath.Number(numbers[i], out v)) ValidInput = false;
                    if (inverse[i]) { if (Mathf.Abs(v) < 0.0000001f) ValidInput = false; else v = 1 / v; }
                    values[i] = v;
                }
                if (!ValidInput) { Status = "Finish entering a valid number"; return; }
            }
            Vector3 position = Session.StartPosition;
            Quaternion rotation = Session.StartRotation;
            if (!Rotating)
            {
                Vector3 delta;
                if (numeric)
                {
                    if (PlaneLock)
                    {
                        delta = Vector3.zero; int valueIndex = 0;
                        for (int i = 0; i < 3; i++) if (i != Axis) delta += Direction(i) * values[valueIndex++];
                    }
                    else delta = Axis >= 0 ? Direction(Axis) * values.x :
                        Direction(0) * values.x + Direction(1) * values.y + Direction(2) * values.z;
                }
                else
                {
                    if (Axis >= 0 && !PlaneLock) delta = Direction(Axis) * (AxisPoint(mouse, Direction(Axis)) - AxisPoint(startMouse, Direction(Axis))) * factor;
                    else
                    {
                        Vector3 normal = PlaneLock ? Direction(Axis) : cam.transform.forward;
                        delta = (PlanePoint(mouse, normal) - PlanePoint(startMouse, normal)) * factor;
                    }
                    if (snap)
                    {
                        float step = fine ? 0.1f : 1;
                        if (Axis >= 0 && !PlaneLock) delta = Direction(Axis) * TransformMath.Snap(Vector3.Dot(delta, Direction(Axis)), step);
                        else
                        {
                            Vector3 components = new Vector3(Vector3.Dot(delta, Direction(0)), Vector3.Dot(delta, Direction(1)), Vector3.Dot(delta, Direction(2)));
                            components = TransformMath.Snap(components, step);
                            delta = Direction(0) * components.x + Direction(1) * components.y + Direction(2) * components.z;
                        }
                    }
                }
                position += delta;
                readoutNumbers = Vector3.zero;
                if (Axis >= 0 && !PlaneLock) readoutNumbers.x = Vector3.Dot(delta, Direction(Axis));
                else
                {
                    int valueIndex = 0;
                    for (int i = 0; i < 3; i++) if (!PlaneLock || i != Axis) readoutNumbers[valueIndex++] = Vector3.Dot(delta, Direction(i));
                }
            }
            else if (Trackball)
            {
                Vector2 delta = (mouse - startMouse) * (0.35f * factor);
                if (numeric) delta = new Vector2(values.x, values.y);
                if (snap && !numeric) delta = new Vector2(TransformMath.Snap(delta.x, fine ? 1 : 5), TransformMath.Snap(delta.y, fine ? 1 : 5));
                rotation = Quaternion.AngleAxis(delta.x, cam.transform.up) * Quaternion.AngleAxis(-delta.y, cam.transform.right) * rotation;
                readoutNumbers = new Vector3(delta.x, delta.y, 0);
            }
            else
            {
                Vector3 direction = Axis >= 0 ? Direction(Axis) : cam.transform.forward;
                float sign = Mathf.Sign(Vector3.Dot(direction, cam.transform.forward));
                float angle = numeric ? -values.x : mouseAngle * factor * sign;
                if (snap && !numeric) angle = TransformMath.Snap(angle, fine ? 1 : 5);
                rotation = Quaternion.AngleAxis(angle, direction) * rotation;
                readoutNumbers = new Vector3(-angle, 0, 0);
            }
            if (!TransformMath.Finite(position) || position.magnitude > 1000000) { ValidInput = false; Status = "Value exceeds supported coordinate range"; return; }
            Session.Preview(position, rotation);
            Status = (Rotating ? (Trackball ? "Trackball" : "Rotate") : "Move") +
                (Axis >= 0 ? "  " + (PlaneLock ? "Except " : "") + "XYZ"[Axis] + (Local ? " Local" : " Global") : "  View") +
                (numeric ? "  " + string.Join(" / ", numbers) : "");
            UpdateGuides();
        }
        internal void Apply()
        {
            if (!Session.Active || !ValidInput) return;
            var remember = handleOperation ? CaptureRecentAction() : null;
            Session.Commit(); End(); recent = remember;
        }
        internal void Cancel() { ClearRecentAction(); if (!Session.Active) return; Session.Cancel(); End(); }
        private void End()
        {
            consumedFrame = Time.frameCount; Modal = false; queuedText = "";
            handleOperation = false; handleDragging = false;
            ClearHandleSelection();
            Status = "Drag an arrow or ring, then type";
            foreach (var guide in guides) if (guide != null) guide.enabled = false;
            UI?.EndEdit();
        }
        private void ReleaseBlocker()
        {
            if (blocker == null) return;
            if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker == blocker) InputManager.Instance.currentInputBlocker = null;
            UnityEngine.Object.Destroy(blocker.gameObject); blocker = null;
        }
        private void UpdateGuides()
        {
            if (guideMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                guideMaterial = new Material(shader);
            }
            float extent = Mathf.Max(5, Vector3.Distance(BuildingCamera.cam.transform.position, Session.StartPosition));
            for (int i = 0; i < 3; i++)
            {
                if (guides[i] == null)
                {
                    guides[i] = new GameObject("Precision " + "XYZ"[i]).AddComponent<LineRenderer>();
                    guides[i].sharedMaterial = guideMaterial; guides[i].useWorldSpace = true; guides[i].positionCount = 2;
                }
                var line = guides[i];
                line.enabled = Axis >= 0 && !Trackball && (PlaneLock ? i != Axis : i == Axis);
                line.startColor = line.endColor = i == 0 ? new Color(0.9f, 0.2f, 0.2f) : i == 1 ? new Color(0.25f, 0.8f, 0.25f) : new Color(0.25f, 0.45f, 1);
                line.startWidth = line.endWidth = extent * 0.0015f;
                line.SetPosition(0, Session.StartPosition - Direction(i) * extent);
                line.SetPosition(1, Session.StartPosition + Direction(i) * extent);
            }
        }
        public void Dispose()
        {
            Cancel(); ReleaseBlocker();
            projection.Dispose();
            if (keyboard != null) keyboard.onTextInput -= OnText;
            foreach (var line in guides) if (line != null) UnityEngine.Object.Destroy(line.gameObject);
            if (guideMaterial != null) UnityEngine.Object.Destroy(guideMaterial);
            UI?.Dispose();
        }
    }
}

using System;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    // Input rays remain fixed while the visible camera follows the edited part.
    internal sealed class DragProjection : IDisposable
    {
        internal Camera Camera { get; private set; }
        internal void Capture(Camera source)
        {
            if (source == null) return;
            if (Camera == null)
            {
                var go = new GameObject("EditorTools drag projection");
                go.hideFlags = HideFlags.HideAndDontSave;
                Camera = go.AddComponent<Camera>();
            }
            Camera.CopyFrom(source); Camera.enabled = false;
            Camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        }
        public void Dispose() { if (Camera != null) UnityEngine.Object.Destroy(Camera.gameObject); Camera = null; }
    }
}

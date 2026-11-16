using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aviassembly.EditorTools
{
    // Command-buffer draws never add selectable geometry or change source materials.
    internal sealed class HiddenXray : IDisposable
    {
        private Camera camera;
        private CommandBuffer commands;
        private Material material;
        private readonly Color tint;
        private readonly string name;
        internal HiddenXray(Color? tint = null, string name = "EditorTools hidden part X-ray")
        { this.tint = tint ?? new Color(0.25f, 0.8f, 1f, 0.12f); this.name = name; }
        internal int DrawCount { get; private set; }
        internal void Render(Camera next, IEnumerable<Renderer> renderers, bool visible)
        {
            DrawCount = 0;
            if (camera != next) { Detach(); camera = next; }
            if (commands != null) commands.Clear();
            if (!visible || camera == null) return;
            if (material == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_Color", tint);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0); material.SetInt("_ZTest", (int)CompareFunction.Always);
                material.SetInt("_Cull", (int)CullMode.Back);
            }
            if (commands == null)
            {
                commands = new CommandBuffer { name = name };
                camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, commands);
            }
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var skinned = renderer as SkinnedMeshRenderer;
                var mesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                { commands.DrawRenderer(renderer, material, i); DrawCount++; }
            }
        }
        private void Detach()
        {
            if (commands != null)
            {
                if (camera != null) camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, commands);
                commands.Release(); commands = null;
            }
            camera = null;
        }
        internal void Clear() { commands?.Clear(); DrawCount = 0; }
        public void Dispose() { Detach(); if (material != null) UnityEngine.Object.Destroy(material); material = null; }
    }
}

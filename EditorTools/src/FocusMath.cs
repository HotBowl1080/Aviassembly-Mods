using System;

namespace Aviassembly.EditorTools
{
    internal static class FocusMath
    {
        // Fit a bounding sphere against whichever viewport dimension is tighter.
        internal static float FrameDistance(float radius, float verticalFov, float aspect, float nearClip)
        {
            double halfVertical = Math.Max(1, Math.Min(179, verticalFov)) * Math.PI / 360;
            double halfHorizontal = Math.Atan(Math.Tan(halfVertical) * Math.Max(0.01, aspect));
            double halfAngle = Math.Min(halfVertical, halfHorizontal);
            double safeRadius = Math.Max(0.05, radius);
            return (float)Math.Max(1, Math.Max(safeRadius * 1.15 / Math.Sin(halfAngle), safeRadius + nearClip + 0.1));
        }

        internal static float Ease(float elapsed, float duration)
        {
            float t = Math.Max(0, Math.Min(1, elapsed / Math.Max(0.01f, duration)));
            return t * t * (3 - 2 * t);
        }
    }
}

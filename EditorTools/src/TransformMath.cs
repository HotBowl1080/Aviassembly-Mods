using System;
using System.Globalization;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    internal static class TransformMath
    {
        // Blender coordinates: X right, Y forward, Z up. Swapping Y/Z also
        // changes handedness, hence the negative quaternion angles below.
        internal static Vector3 FromBlender(Vector3 v) => new Vector3(v.x, v.z, v.y);
        internal static Vector3 ToBlender(Vector3 v) => new Vector3(v.x, v.z, v.y);
        internal static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.forward : Vector3.up;
        internal static Quaternion EulerXYZ(Vector3 e) => Quaternion.AngleAxis(-e.z, Vector3.up) *
            Quaternion.AngleAxis(-e.y, Vector3.forward) * Quaternion.AngleAxis(-e.x, Vector3.right);
        internal static Vector3 EulerXYZ(Quaternion q)
        {
            Vector3 x = ToBlender(q * Vector3.right), y = ToBlender(q * Vector3.forward), z = ToBlender(q * Vector3.up);
            float b = Mathf.Asin(Mathf.Clamp(-x.z, -1, 1));
            float a, c;
            if (Mathf.Abs(x.z) < 0.999999f) { a = Mathf.Atan2(y.z, z.z); c = Mathf.Atan2(x.y, x.x); }
            else { b = -Mathf.Sign(x.z) * Mathf.PI / 2; a = Mathf.Atan2(-z.y, y.y); c = 0; }
            return new Vector3(a, b, c) * Mathf.Rad2Deg;
        }
        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        internal static bool Number(string s, out float value)
        {
            bool valid = float.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && Finite(value);
            return valid;
        }
        internal static string Format(float x) => x.ToString("G9", CultureInfo.InvariantCulture);
        internal static float Snap(float value, float step) => Mathf.Round(value / step) * step;
        internal static Vector3 Snap(Vector3 value, float step) => new Vector3(Snap(value.x, step), Snap(value.y, step), Snap(value.z, step));
    }
}

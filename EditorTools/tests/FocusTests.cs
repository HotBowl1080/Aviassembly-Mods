using System;
using Aviassembly.EditorTools;

internal static class FocusTests
{
    private static int checks;
    private static void Check(bool result, string label)
    {
        checks++;
        if (!result) throw new Exception(label);
    }
    private static void Main()
    {
        foreach (float radius in new[] { 0.05f, 0.1f, 1f, 5f, 30f })
        foreach (float fov in new[] { 20f, 60f, 90f, 120f })
        foreach (float aspect in new[] { 0.5f, 1f, 1.777778f, 3.555556f })
        {
            float d = FocusMath.FrameDistance(radius, fov, aspect, 0.3f);
            double halfV = fov * Math.PI / 360;
            double halfH = Math.Atan(Math.Tan(halfV) * aspect);
            Check(d * Math.Sin(Math.Min(halfV, halfH)) >= radius, "Part clips against viewport");
            Check(d - radius > 0.3, "Part clips against near plane");
            Check(d >= 1, "Conflicts with the native camera minimum distance");
        }
        Check(FocusMath.Ease(-1, 0.28f) == 0, "Transition begins before zero");
        Check(FocusMath.Ease(0, 0.28f) == 0, "Transition must start without a jump");
        Check(FocusMath.Ease(0.28f, 0.28f) == 1, "Transition must reach target");
        Check(FocusMath.Ease(10, 0.28f) == 1, "Transition must not overshoot");
        float previous = 0;
        for (int i = 0; i <= 100; i++)
        {
            float next = FocusMath.Ease(i * 0.0028f, 0.28f);
            Check(next >= previous && next <= 1, "Transition reverses or overshoots");
            previous = next;
        }
        Console.WriteLine($"PASS: {checks} framing and transition checks");
    }
}

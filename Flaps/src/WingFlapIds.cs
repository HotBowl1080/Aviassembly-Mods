using System;

internal static class WingFlapIds
{
    internal const byte BigWing = 1;
    internal const byte StraightWing = 2;

    // Array order is part of the save format. Append new entries without reordering.
    static readonly string[] Prefabs = {
        "BigWing", "StraightWing", "SmallRecreationalWing", "GliderWing", "SmallJetWing",
        "ElectronWing", "DeltaWing02", "Wing", "DeltaWing01", "TailWing", "Tailwing02",
        "Tailwing03", "Tailwing04"
    };

    internal static byte Of(string name)
    {
        // Preserve the original save identities for existing Big and Straight Wing copies.
        if (name.StartsWith("BigWing", StringComparison.Ordinal)) return BigWing;
        if (name.StartsWith("StraightWing", StringComparison.Ordinal)) return StraightWing;
        int suffix = name.IndexOf('(');
        string prefab = suffix < 0 ? name : name.Substring(0, suffix).TrimEnd();
        return (byte)(Array.IndexOf(Prefabs, prefab) + 1);
    }

    internal static bool IsValid(byte id) => id > 0 && id <= Prefabs.Length;
}

using System;
using System.Numerics;

namespace MaplesEcho.Services;

/// <summary>
/// Color helpers for the accessibility surface: a stable auto-hash fallback
/// color for any not-yet-assigned speaker, and WCAG contrast math so the config
/// window can warn when a picked color won't be legible against the background.
/// (Plan §7)
/// </summary>
public static class ColorUtil
{
    /// <summary>
    /// Deterministic fallback color for a speaker name. Same name → same color
    /// every session, so the mapping stays stable until she overrides it.
    /// Hue is hashed; saturation/value are fixed high enough to read on a dark
    /// background.
    /// </summary>
    public static Vector4 HashColor(string speaker)
    {
        var hash = 2166136261u; // FNV-1a
        foreach (var c in speaker)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        var hue = hash % 360u / 360f;
        return HsvToRgb(hue, 0.55f, 0.95f);
    }

    /// <summary>
    /// WCAG contrast ratio (1.0–21.0) between two opaque colors. Used to flag
    /// combinations that fall below the 7:1 AAA target. Alpha is ignored — a
    /// translucent window still composites text over a near-solid backdrop.
    /// </summary>
    public static float ContrastRatio(Vector4 a, Vector4 b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var lighter = MathF.Max(la, lb);
        var darker = MathF.Min(la, lb);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(Vector4 c)
        => 0.2126f * Linearize(c.X) + 0.7152f * Linearize(c.Y) + 0.0722f * Linearize(c.Z);

    private static float Linearize(float channel)
        => channel <= 0.03928f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    private static Vector4 HsvToRgb(float h, float s, float v)
    {
        var i = (int)MathF.Floor(h * 6f);
        var f = h * 6f - i;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        return (i % 6) switch
        {
            0 => new Vector4(v, t, p, 1f),
            1 => new Vector4(q, v, p, 1f),
            2 => new Vector4(p, v, t, 1f),
            3 => new Vector4(p, q, v, 1f),
            4 => new Vector4(t, p, v, 1f),
            _ => new Vector4(v, p, q, 1f),
        };
    }
}

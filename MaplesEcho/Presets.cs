using System.Numerics;

namespace MaplesEcho;

/// <summary>
/// One-click appearance starting points so Maple can get to something usable in
/// a click, then fine-tune. Presets touch only visual fields — never the token
/// or channel. Defaults are checked to clear the 7:1 contrast target. (Plan §6, §7)
/// </summary>
public static class Presets
{
    public static void DarkBlueHighContrast(Configuration c)
    {
        c.WindowBackgroundColor = new Vector4(0.06f, 0.11f, 0.18f, 1f); // #0F1B2D
        c.BackgroundOpacity = 0.92f;
        c.TextColor = new Vector4(0.941f, 0.949f, 0.961f, 1f);          // #F0F2F5
        c.AuthorColor = new Vector4(0.62f, 0.74f, 0.90f, 1f);
        c.TimestampColor = new Vector4(0.50f, 0.56f, 0.66f, 1f);
        c.FontSize = 18f;
        c.LineSpacing = 1.2f;
    }

    public static void LargeText(Configuration c)
    {
        DarkBlueHighContrast(c);
        c.FontSize = 28f;
        c.LineSpacing = 1.4f;
    }

    public static void Minimal(Configuration c)
    {
        c.WindowBackgroundColor = new Vector4(0.02f, 0.02f, 0.03f, 1f);
        c.BackgroundOpacity = 0.75f;
        c.TextColor = new Vector4(0.92f, 0.92f, 0.92f, 1f);
        c.AuthorColor = new Vector4(0.65f, 0.65f, 0.70f, 1f);
        c.TimestampColor = new Vector4(0.45f, 0.45f, 0.50f, 1f);
        c.FontSize = 18f;
        c.LineSpacing = 1.1f;
        c.ShowTimestamps = false;
        c.HideTitleBar = true;
    }
}

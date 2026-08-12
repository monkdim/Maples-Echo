using System;
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

    /// <summary>
    /// Seed the keyword and glossary lists with a curated FFXIV starter set so
    /// both features show value before any manual data entry. Merges only —
    /// existing entries are never overwritten or duplicated, and everything
    /// added is editable/removable like any hand-made rule. Colors are bright
    /// enough to read on the shipped dark backgrounds.
    /// </summary>
    public static (int KeywordsAdded, int GlossaryAdded) ApplyStarterPack(Configuration c)
    {
        var starterKeywords = new KeywordRule[]
        {
            new() { Word = "stack",       Color = new(0.35f, 0.90f, 0.45f, 1f) }, // green
            new() { Word = "spread",      Color = new(1.00f, 0.85f, 0.30f, 1f) }, // yellow
            new() { Word = "in",          Color = new(0.40f, 0.85f, 1.00f, 1f) }, // cyan
            new() { Word = "out",         Color = new(1.00f, 0.60f, 0.25f, 1f) }, // orange
            new() { Word = "stop",        Color = new(1.00f, 0.35f, 0.35f, 1f) }, // red
            new() { Word = "tank buster", Color = new(0.95f, 0.50f, 0.90f, 1f) }, // magenta
            new() { Word = "tower",       Color = new(0.75f, 0.60f, 1.00f, 1f) }, // purple
            new() { Word = "bait",        Color = new(1.00f, 0.55f, 0.75f, 1f) }, // pink
        };

        var starterGlossary = new GlossaryRule[]
        {
            new() { Find = "a o e", Replace = "AOE" },
            new() { Find = "tank bust her", Replace = "tank buster" },
            new() { Find = "limit brake", Replace = "limit break" },
        };

        var kw = 0;
        foreach (var rule in starterKeywords)
        {
            if (c.KeywordRules.Exists(r => r.Word.Equals(rule.Word, StringComparison.OrdinalIgnoreCase)))
                continue;
            c.KeywordRules.Add(rule);
            kw++;
        }

        var gl = 0;
        foreach (var rule in starterGlossary)
        {
            if (c.Glossary.Exists(r => r.Find.Equals(rule.Find, StringComparison.OrdinalIgnoreCase)))
                continue;
            c.Glossary.Add(rule);
            gl++;
        }

        return (kw, gl);
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

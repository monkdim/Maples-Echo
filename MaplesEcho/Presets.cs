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
    /// Starter keywords with two palettes: the default bright set, and a
    /// colorblind-friendly set based on Okabe–Ito (the blue brightened to stay
    /// legible on the shipped dark backgrounds). Words carry the meaning either
    /// way — color is reinforcement, never the only signal.
    /// </summary>
    private static readonly (string Word, Vector4 Default, Vector4 Colorblind)[] StarterKeywords =
    {
        ("stack",       new(0.35f, 0.90f, 0.45f, 1f), new(0.00f, 0.62f, 0.45f, 1f)),
        ("spread",      new(1.00f, 0.85f, 0.30f, 1f), new(0.94f, 0.89f, 0.26f, 1f)),
        ("in",          new(0.40f, 0.85f, 1.00f, 1f), new(0.34f, 0.71f, 0.91f, 1f)),
        ("out",         new(1.00f, 0.60f, 0.25f, 1f), new(0.90f, 0.62f, 0.00f, 1f)),
        ("stop",        new(1.00f, 0.35f, 0.35f, 1f), new(0.84f, 0.37f, 0.00f, 1f)),
        ("tank buster", new(0.95f, 0.50f, 0.90f, 1f), new(0.80f, 0.47f, 0.65f, 1f)),
        ("tower",       new(0.75f, 0.60f, 1.00f, 1f), new(0.25f, 0.56f, 0.87f, 1f)),
        ("bait",        new(1.00f, 0.55f, 0.75f, 1f), new(0.93f, 0.93f, 0.93f, 1f)),
    };

    /// <summary>
    /// Seed the keyword and glossary lists with a curated FFXIV starter set so
    /// both features show value before any manual data entry. Merge-only for
    /// the default palette — existing entries are never overwritten or
    /// duplicated. The colorblind variant additionally recolors starter words
    /// already in the list (that's its whole point); custom words are never
    /// touched. Everything added is editable/removable like any hand-made rule.
    /// </summary>
    public static (int KeywordsAdded, int KeywordsRecolored, int GlossaryAdded) ApplyStarterPack(
        Configuration c, bool colorblindFriendly = false)
    {
        var starterGlossary = new GlossaryRule[]
        {
            new() { Find = "a o e", Replace = "AOE" },
            new() { Find = "tank bust her", Replace = "tank buster" },
            new() { Find = "limit brake", Replace = "limit break" },
        };

        var kw = 0;
        var recolored = 0;
        foreach (var (word, defaultColor, colorblindColor) in StarterKeywords)
        {
            var color = colorblindFriendly ? colorblindColor : defaultColor;
            var existing = c.KeywordRules.Find(r => r.Word.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                c.KeywordRules.Add(new KeywordRule { Word = word, Color = color });
                kw++;
            }
            else if (colorblindFriendly && existing.Color != color)
            {
                existing.Color = color;
                recolored++;
            }
        }

        var gl = 0;
        foreach (var rule in starterGlossary)
        {
            if (c.Glossary.Exists(r => r.Find.Equals(rule.Find, StringComparison.OrdinalIgnoreCase)))
                continue;
            c.Glossary.Add(rule);
            gl++;
        }

        return (kw, recolored, gl);
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

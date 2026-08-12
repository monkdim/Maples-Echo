using System;
using System.Numerics;

namespace MaplesEcho;

/// <summary>
/// A single keyword emphasized on screen when it appears. Whole-word matching
/// (with phrase support) exists because the FFXIV callouts that matter most —
/// "in", "out" — are unusable as substrings, and per-keyword color lets the
/// highlight itself say which mechanic it is before the word is read. Kept free
/// of any Dalamud dependency so matching can be unit-tested off the game.
/// </summary>
[Serializable]
public class KeywordRule
{
    public string Word { get; set; } = string.Empty;

    /// <summary>Require non-alphanumeric (or the string edge) on both sides of
    /// the match. Off preserves the old substring behavior for migrated rules.</summary>
    public bool WholeWord { get; set; } = true;

    public bool Enabled { get; set; } = true;

    /// <summary>Highlight color for this keyword's lines and pulse. Defaults to
    /// the shipped keyword-highlight yellow.</summary>
    public Vector4 Color { get; set; } = new(1.0f, 0.85f, 0.30f, 1.0f);
}

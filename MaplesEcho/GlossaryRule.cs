using System;

namespace MaplesEcho;

/// <summary>
/// A single find → replace glossary rule, applied before display to correct
/// common mistranscriptions of FFXIV jargon. Kept free of any Dalamud
/// dependency so the transform pipeline can be unit-tested off the game. (Plan §8b)
/// </summary>
[Serializable]
public class GlossaryRule
{
    public string Find { get; set; } = string.Empty;
    public string Replace { get; set; } = string.Empty;
    public bool CaseSensitive { get; set; } = false;
    public bool Enabled { get; set; } = true;

    /// <summary>Replace only at word boundaries (non-alphanumeric or string edge
    /// on both sides). Off by default — existing rules matched substrings.</summary>
    public bool WholeWord { get; set; } = false;

    /// <summary>Treat Find as a .NET regular expression; Replace may use $1
    /// captures. Invalid or pathological patterns are skipped at apply time —
    /// a bad rule must never eat or stall a callout.</summary>
    public bool IsRegex { get; set; } = false;
}

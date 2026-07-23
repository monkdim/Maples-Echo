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
}

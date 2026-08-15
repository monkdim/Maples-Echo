using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace MaplesEcho;

/// <summary>
/// Persisted settings for Maple's Echo. Saved via Dalamud's
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.SavePluginConfig"/>.
///
/// Everything visual here is meant to be adjusted by the user at runtime — the
/// accessibility surface is tuned by her, on her monitor, not baked by us.
/// (Plan §6, §7, §10b)
/// </summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // ----- Connection ----------------------------------------------------
    /// <summary>Discord bot token. Entered at runtime only — never hardcode,
    /// never commit. Stored unencrypted in Dalamud's config folder. (Plan §10)</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Target Discord text channel the transcription bot posts into.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>Only relay webhook-sourced messages. Excludes the transcription
    /// bot's own command-reply embeds and any human typing in the channel. (Plan §5)</summary>
    public bool WebhookMessagesOnly { get; set; } = true;

    /// <summary>Optional pin to a single webhook id. 0 = any webhook in the channel.</summary>
    public ulong SourceWebhookId { get; set; }

    /// <summary>Prefix stripped from the webhook author username to recover the
    /// speaker name. Configurable so a bot swap doesn't require a rebuild. (Plan §5)</summary>
    public string SpeakerPrefix { get; set; } = "[Scriptly] ";

    // ----- Window appearance --------------------------------------------
    /// <summary>Default dark blue (#0F1B2D). Opacity is applied separately. (Plan §7)</summary>
    public Vector4 WindowBackgroundColor { get; set; } = new(0.06f, 0.11f, 0.18f, 1.0f);

    /// <summary>Near-white, slightly warm (#F0F2F5). Must clear 7:1 vs background.</summary>
    public Vector4 TextColor { get; set; } = new(0.941f, 0.949f, 0.961f, 1.0f);

    public Vector4 AuthorColor { get; set; } = new(0.62f, 0.74f, 0.90f, 1.0f);

    public Vector4 TimestampColor { get; set; } = new(0.50f, 0.56f, 0.66f, 1.0f);

    /// <summary>Highlight color for keyword hits. (Plan §8b)</summary>
    public Vector4 KeywordColor { get; set; } = new(1.0f, 0.85f, 0.30f, 1.0f);

    public float BackgroundOpacity { get; set; } = 0.92f;

    // ----- Typography ----------------------------------------------------
    public float FontSize { get; set; } = 18f;

    public float LineSpacing { get; set; } = 1.2f;

    // ----- Speaker identification ---------------------------------------
    /// <summary>Attribution IS the content here — who said it carries as much
    /// information as what was said. (Plan §7)</summary>
    public bool ShowSpeakerName { get; set; } = true;

    public bool UseSpeakerColors { get; set; } = true;

    /// <summary>Manual speaker → color map. Populated from the seen-speaker list.</summary>
    public Dictionary<string, Vector4> SpeakerColors { get; set; } = new();

    /// <summary>Every speaker name ever seen, so the config window can offer real
    /// names to assign rather than making her type them. (Plan §7)</summary>
    public List<string> KnownSpeakers { get; set; } = new();

    /// <summary>Group a run of messages from the same speaker within the window.</summary>
    public bool MergeConsecutive { get; set; } = true;

    public int MergeWindowSeconds { get; set; } = 8;

    // ----- Transform pipeline (v1.5-ready, see Plan §8b) ----------------
    /// <summary>Find → replace rules applied before display, to correct common
    /// mistranscriptions of FFXIV jargon. Grows over time.</summary>
    public List<GlossaryRule> Glossary { get; set; } = new();

    /// <summary>Keywords emphasized on screen when they appear (her name, "stack",
    /// "spread", "tank buster", "stop", …), each with its own color and
    /// whole-word setting.</summary>
    public List<KeywordRule> KeywordRules { get; set; } = new();

    /// <summary>Legacy plain-string keywords (config v1). Migrated into
    /// <see cref="KeywordRules"/> on load; kept so old configs and backup
    /// blobs still deserialize.</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Convert legacy v1 keywords into rules. Old keywords matched as
    /// substrings, so migrated rules keep WholeWord off — behavior is
    /// preserved exactly; the new default only applies to newly added rules.
    /// Returns true if anything changed (caller should save).
    /// </summary>
    public bool MigrateLegacyKeywords()
    {
        if (Keywords.Count == 0)
            return false;

        foreach (var word in Keywords)
        {
            var trimmed = word?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                continue;
            if (KeywordRules.Exists(r => r.Word.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                continue;

            KeywordRules.Add(new KeywordRule { Word = trimmed, WholeWord = false, Color = KeywordColor });
        }

        Keywords.Clear();
        Version = 2;
        return true;
    }

    // ----- Combat mode ---------------------------------------------------
    /// <summary>Auto-apply a combat profile while InCombat: bigger text, fewer
    /// distractions, optionally only the freshest callouts. Off by default —
    /// the window never changes behavior unless she opted in.</summary>
    public bool CombatModeEnabled { get; set; } = false;

    public float CombatFontSize { get; set; } = 28f;

    public bool CombatHideTimestamps { get; set; } = true;

    /// <summary>In combat, only show messages younger than this. 0 = show all.</summary>
    public int CombatRecentSeconds { get; set; } = 0;

    // ----- Behavior ------------------------------------------------------
    public bool ShowTimestamps { get; set; } = true;

    public bool Use24HourTime { get; set; } = false;

    public bool AutoScroll { get; set; } = true;

    /// <summary>Briefly flash the window background when a message arrives —
    /// the visual stand-in for hearing someone start talking. Keyword hits
    /// flash stronger, in the keyword color.</summary>
    public bool FlashOnNewMessage { get; set; } = true;

    /// <summary>Play a chat sound effect when a keyword message arrives. For
    /// hearing users who can't run Discord audio; off by default — the primary
    /// user may not hear it at all.</summary>
    public bool PlaySoundOnKeyword { get; set; } = false;

    /// <summary>Which chat sound to play: the game's &lt;se.1&gt;–&lt;se.16&gt;.</summary>
    public int KeywordSoundId { get; set; } = 6;

    public int MaxMessages { get; set; } = 200;

    public bool LockWindowPosition { get; set; } = false;

    /// <summary>Window ignores the mouse entirely — clicks and camera drags pass
    /// through to the game. Turned off from the settings window (which is never
    /// click-through) when the window needs to be moved or scrolled.</summary>
    public bool ClickThrough { get; set; } = false;

    public bool HideDuringCutscenes { get; set; } = true;

    /// <summary>Whether the relay window was open last session, so a deliberate
    /// close survives a relog instead of the window force-opening every launch.</summary>
    public bool RelayWindowOpen { get; set; } = true;

    /// <summary>Secondary fallback: also mirror into the native game chat log
    /// via IChatGui.Print(). Off by default. (Plan §2)</summary>
    public bool AlsoPrintToGameChat { get; set; } = false;

    /// <summary>Hide the window title bar so a new message can never steal input
    /// mid-pull. (Plan §7 — "a window grabbing focus during a mechanic is a wipe")</summary>
    public bool HideTitleBar { get; set; } = false;
}

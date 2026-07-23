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
    public int Version { get; set; } = 1;

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

    /// <summary>Font face selector. "Default" uses Dalamud's default font. (Plan §7)</summary>
    public string FontFace { get; set; } = "Default";

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

    /// <summary>Words emphasized on screen when they appear (her name, "stack",
    /// "spread", "tank buster", "stop", …).</summary>
    public List<string> Keywords { get; set; } = new();

    // ----- Behavior ------------------------------------------------------
    public bool ShowTimestamps { get; set; } = true;

    public bool AutoScroll { get; set; } = true;

    public int MaxMessages { get; set; } = 200;

    public bool LockWindowPosition { get; set; } = false;

    public bool HideDuringCutscenes { get; set; } = true;

    /// <summary>Secondary fallback: also mirror into the native game chat log
    /// via IChatGui.Print(). Off by default. (Plan §2)</summary>
    public bool AlsoPrintToGameChat { get; set; } = false;

    /// <summary>Hide the window title bar so a new message can never steal input
    /// mid-pull. (Plan §7 — "a window grabbing focus during a mechanic is a wipe")</summary>
    public bool HideTitleBar { get; set; } = false;
}

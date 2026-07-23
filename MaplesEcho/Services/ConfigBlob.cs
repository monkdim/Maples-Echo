using System;
using System.Text;
using System.Text.Json;

namespace MaplesEcho.Services;

/// <summary>
/// Serialize/restore the full configuration as one base64 string — colors,
/// fonts, speaker assignments, glossary, AND the token — so the developer can
/// prepare Maple's entire setup and hand it over as one paste, and so she has a
/// backup. The blob contains the token; the UI warns on export. (Plan §10b)
/// </summary>
public static class ConfigBlob
{
    private const string Prefix = "MAPLESECHO1:";

    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = false,
    };

    public static string Export(Configuration config)
    {
        var json = JsonSerializer.Serialize(config, Options);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return Prefix + b64;
    }

    /// <summary>
    /// Restore into <paramref name="target"/> in place, so the live instance the
    /// rest of the plugin holds is updated. Returns false on any malformed input.
    /// </summary>
    public static bool Import(string blob, Configuration target)
    {
        if (string.IsNullOrWhiteSpace(blob))
            return false;

        try
        {
            var trimmed = blob.Trim();
            if (trimmed.StartsWith(Prefix, StringComparison.Ordinal))
                trimmed = trimmed[Prefix.Length..];

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
            var parsed = JsonSerializer.Deserialize<Configuration>(json, Options);
            if (parsed is null)
                return false;

            CopyInto(parsed, target);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void CopyInto(Configuration from, Configuration to)
    {
        to.Version = from.Version;
        to.BotToken = from.BotToken;
        to.ChannelId = from.ChannelId;
        to.WebhookMessagesOnly = from.WebhookMessagesOnly;
        to.SourceWebhookId = from.SourceWebhookId;
        to.SpeakerPrefix = from.SpeakerPrefix;

        to.WindowBackgroundColor = from.WindowBackgroundColor;
        to.TextColor = from.TextColor;
        to.AuthorColor = from.AuthorColor;
        to.TimestampColor = from.TimestampColor;
        to.KeywordColor = from.KeywordColor;
        to.BackgroundOpacity = from.BackgroundOpacity;

        to.FontSize = from.FontSize;
        to.FontFace = from.FontFace;
        to.LineSpacing = from.LineSpacing;

        to.ShowSpeakerName = from.ShowSpeakerName;
        to.UseSpeakerColors = from.UseSpeakerColors;
        to.SpeakerColors = from.SpeakerColors ?? new();
        to.KnownSpeakers = from.KnownSpeakers ?? new();
        to.MergeConsecutive = from.MergeConsecutive;
        to.MergeWindowSeconds = from.MergeWindowSeconds;

        to.Glossary = from.Glossary ?? new();
        to.Keywords = from.Keywords ?? new();

        to.ShowTimestamps = from.ShowTimestamps;
        to.AutoScroll = from.AutoScroll;
        to.MaxMessages = from.MaxMessages;
        to.LockWindowPosition = from.LockWindowPosition;
        to.HideDuringCutscenes = from.HideDuringCutscenes;
        to.AlsoPrintToGameChat = from.AlsoPrintToGameChat;
        to.HideTitleBar = from.HideTitleBar;
    }
}

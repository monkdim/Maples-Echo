using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MaplesEcho.Services;

namespace MaplesEcho.Windows;

/// <summary>
/// The accessibility surface: a dedicated, resizable ImGui window with full
/// control over background, text color, font size and spacing. Position and
/// size persist automatically via Dalamud's imgui.ini. (Plan §2, §7)
/// </summary>
public sealed class RelayWindow : Window, IDisposable
{
    // FontSize value that maps to scale 1.0. The default Dalamud font is ~17px;
    // 18 keeps the default config at a hair above baseline.
    private const float FontScaleBaseline = 18f;

    private readonly Configuration config;
    private readonly MessageStore store;
    private readonly DiscordRelayService discord;

    private bool stickToBottom = true;

    public RelayWindow(Configuration config, MessageStore store, DiscordRelayService discord)
        : base("Maple's Echo##MaplesEchoRelay")
    {
        this.config = config;
        this.store = store;
        this.discord = discord;

        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 120),
            MaximumSize = new Vector2(4000, 4000),
        };
        Size = new Vector2(420, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        // Window behavior flags, recomputed each frame from config. Resizable is
        // intentional (never NoResize). NoFocusOnAppearing so a message arriving
        // mid-mechanic can never steal keyboard input — that is a wipe. (Plan §7)
        var flags = ImGuiWindowFlags.NoFocusOnAppearing;
        if (config.HideTitleBar)
            flags |= ImGuiWindowFlags.NoTitleBar;
        if (config.LockWindowPosition)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        Flags = flags;

        // Background + opacity. On disconnect, tint the background red so the
        // failure is impossible to miss — there's no audio cue to fall back on. (Plan §9)
        var bg = config.WindowBackgroundColor;
        var opacity = Math.Clamp(config.BackgroundOpacity, 0f, 1f);
        if (IsDisconnected())
            bg = new Vector4(0.28f, 0.06f, 0.07f, 1f); // dark red alarm tint
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(bg.X, bg.Y, bg.Z, opacity));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void Draw()
    {
        // Live font sizing without an atlas rebuild. Crisp custom-font sizing via
        // ManagedFontAtlas is a later refinement; scale gets the accessibility
        // win (readable size) now, safely. (Plan §7 — "ship size + colors first")
        ImGui.SetWindowFontScale(Math.Max(0.5f, config.FontSize / FontScaleBaseline));

        DrawStatusBar();
        ImGui.Separator();
        DrawMessages();

        ImGui.SetWindowFontScale(1f);
    }

    private void DrawStatusBar()
    {
        var (dot, label) = discord.State switch
        {
            RelayState.Connected => (new Vector4(0.30f, 0.85f, 0.40f, 1f), "Connected"),
            RelayState.Connecting => (new Vector4(0.95f, 0.80f, 0.30f, 1f), "Connecting…"),
            RelayState.ConnectedNoContent => (new Vector4(0.95f, 0.55f, 0.20f, 1f), "No content — check intent"),
            RelayState.InvalidToken => (new Vector4(0.90f, 0.25f, 0.25f, 1f), "Invalid token"),
            _ => (new Vector4(0.90f, 0.25f, 0.25f, 1f), "Disconnected"),
        };

        ImGui.TextColored(dot, "●"); // ● indicator
        ImGui.SameLine();
        ImGui.TextUnformatted(label);

        if (discord.LastMessageReceived is { } last)
        {
            var age = DateTime.Now - last;
            ImGui.SameLine();
            ImGui.TextColored(config.TimestampColor, $"  (last message {FormatAge(age)} ago)");
        }
    }

    private void DrawMessages()
    {
        if (!ImGui.BeginChild("##messages", new Vector2(0, 0), false, ImGuiWindowFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        var messages = store.Snapshot();
        var extraSpacing = MathF.Max(0f, (config.LineSpacing - 1f) * config.FontSize);

        string? lastSpeaker = null;
        DateTime lastTime = DateTime.MinValue;

        foreach (var m in messages)
        {
            var mergeable = config.MergeConsecutive
                            && lastSpeaker == m.Speaker
                            && (m.ReceivedAt - lastTime).TotalSeconds <= config.MergeWindowSeconds;

            if (!mergeable)
            {
                if (lastSpeaker != null)
                    ImGui.Dummy(new Vector2(0, extraSpacing));
                DrawHeader(m);
            }

            DrawBody(m);

            lastSpeaker = m.Speaker;
            lastTime = m.ReceivedAt;
        }

        // Auto-scroll: stick to the bottom only while the user is already there.
        // Manual scroll-up pauses; scrolling back to the bottom resumes. (Plan §7)
        if (config.AutoScroll && stickToBottom)
            ImGui.SetScrollHereY(1f);
        stickToBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f;

        ImGui.EndChild();
    }

    private void DrawHeader(RelayMessage m)
    {
        if (config.ShowSpeakerName)
        {
            var color = SpeakerColor(m.Speaker);
            ImGui.TextColored(color, m.Speaker);
            if (config.ShowTimestamps)
            {
                ImGui.SameLine();
                ImGui.TextColored(config.TimestampColor, $"  {m.ReceivedAt:h:mm tt}");
            }
        }
        else if (config.ShowTimestamps)
        {
            ImGui.TextColored(config.TimestampColor, $"{m.ReceivedAt:h:mm tt}");
        }
    }

    private void DrawBody(RelayMessage m)
    {
        var hasKeyword = ContainsKeyword(m.Text);
        if (hasKeyword)
        {
            // Marker + whole-line tint so the critical word is catchable without
            // parsing the sentence. Word-level inline color is a later refinement
            // (hard to combine with wrapping). (Plan §8b)
            ImGui.TextColored(config.KeywordColor, "▸"); // ▸
            ImGui.SameLine();
            PushWrappedColored(config.KeywordColor, m.Text);
        }
        else
        {
            PushWrappedColored(config.TextColor, m.Text);
        }
    }

    private static void PushWrappedColored(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private Vector4 SpeakerColor(string speaker)
    {
        if (!config.UseSpeakerColors)
            return config.AuthorColor;

        if (config.SpeakerColors.TryGetValue(speaker, out var assigned))
            return assigned;

        return ColorUtil.HashColor(speaker);
    }

    private bool ContainsKeyword(string text)
    {
        foreach (var kw in config.Keywords)
        {
            if (!string.IsNullOrWhiteSpace(kw)
                && text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsDisconnected()
        => discord.State is RelayState.Disconnected or RelayState.InvalidToken;

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalSeconds < 60) return $"{(int)age.TotalSeconds}s";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m";
        return $"{(int)age.TotalHours}h";
    }

    public void Dispose()
    {
    }
}

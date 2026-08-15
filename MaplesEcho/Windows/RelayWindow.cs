using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
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
    private readonly IFontAtlas fontAtlas;
    private readonly Func<bool> inCombat;
    private readonly Action save;
    private readonly Action<string> addGlossaryRule;

    // Real font handles built at the configured pixel size — crisp text instead
    // of bilinear-scaled glyphs. One slot per profile so the combat transition
    // never triggers a rebuild; while a build is in flight (or failed), drawing
    // falls back to the old scale path so text is never missing.
    private sealed class FontSlot
    {
        public IFontHandle? Handle;
        public float BuiltSize;
        public float PendingSize;
        public double PendingSince;
    }

    private readonly FontSlot normalFont = new();
    private readonly FontSlot combatFont = new();

    private const double FontRebuildDebounceSeconds = 0.35;

    private bool stickToBottom = true;

    // "N new messages ↓" pill state: the newest id that was on screen while at
    // the bottom, and a one-shot jump requested by clicking the pill.
    private ulong lastSeenAtBottomId;
    private bool jumpToBottom;

    // New-message pulse state: which message id last triggered a flash, when,
    // and the matched keyword's color (null = plain white arrival pulse).
    private ulong lastPulsedId;
    private double pulseStart = double.NegativeInfinity;
    private Vector4? pulseKeywordColor;

    private const float PulseSeconds = 0.4f;
    private const float KeywordPulseSeconds = 0.9f;

    public RelayWindow(
        Configuration config,
        MessageStore store,
        DiscordRelayService discord,
        IFontAtlas fontAtlas,
        Func<bool> inCombat,
        Action save,
        Action<string> addGlossaryRule)
        : base("Maple's Echo##MaplesEchoRelay")
    {
        this.config = config;
        this.store = store;
        this.discord = discord;
        this.fontAtlas = fontAtlas;
        this.inCombat = inCombat;
        this.save = save;
        this.addGlossaryRule = addGlossaryRule;

        IsOpen = config.RelayWindowOpen;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 120),
            MaximumSize = new Vector2(4000, 4000),
        };
        Size = new Vector2(420, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    // Persist open/closed across sessions so a deliberate close sticks. OnOpen/
    // OnClose fire on every path — command toggle, title-bar X, server tab.
    public override void OnOpen()
    {
        if (!config.RelayWindowOpen)
        {
            config.RelayWindowOpen = true;
            save();
        }
    }

    public override void OnClose()
    {
        if (config.RelayWindowOpen)
        {
            config.RelayWindowOpen = false;
            save();
        }
    }

    public override void PreDraw()
    {
        // Window behavior flags, recomputed each frame from config. Resizable is
        // intentional (never NoResize). NoFocusOnAppearing so a message arriving
        // mid-mechanic can never steal keyboard input — that is a wipe. (Plan §7)
        // NoScrollbar on the parent: the message child owns all scrolling, so a
        // parent scrollbar could only ever appear as a layout glitch.
        var flags = ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;
        if (config.HideTitleBar)
            flags |= ImGuiWindowFlags.NoTitleBar;
        if (config.LockWindowPosition)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        // Click-through: the window ignores the mouse entirely so clicks and
        // camera drags pass to the game. Escape hatch is the settings window,
        // which never gets this flag.
        if (config.ClickThrough)
            flags |= ImGuiWindowFlags.NoInputs;
        Flags = flags;

        // Background + opacity. On disconnect, tint the background red so the
        // failure is impossible to miss — there's no audio cue to fall back on. (Plan §9)
        var bg = config.WindowBackgroundColor;
        var opacity = Math.Clamp(config.BackgroundOpacity, 0f, 1f);
        if (IsDisconnected())
        {
            bg = new Vector4(0.28f, 0.06f, 0.07f, 1f); // dark red alarm tint
        }
        else
        {
            TrackNewestForPulse();
            bg = ApplyPulse(bg);
        }

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(bg.X, bg.Y, bg.Z, opacity));
    }

    /// <summary>Arm the pulse when a new message lands. Runs even with the flash
    /// disabled so enabling it later can't retro-flash an old message. In-place
    /// edits keep their id and so never re-pulse.</summary>
    private void TrackNewestForPulse()
    {
        if (store.Newest() is not { } newest || newest.SourceMessageId == lastPulsedId)
            return;

        lastPulsedId = newest.SourceMessageId;
        pulseStart = ImGui.GetTime();
        pulseKeywordColor = MessageTransform.FirstKeywordMatch(newest.Text, config.KeywordRules)?.Color;
    }

    /// <summary>
    /// The visual stand-in for hearing someone start talking: blend the window
    /// background toward a flash color for a beat after each arrival, fading out.
    /// Keyword hits flash stronger and longer, in that keyword's own color, so
    /// the pulse itself says which callout it is from peripheral vision. (Plan §7)
    /// </summary>
    private Vector4 ApplyPulse(Vector4 bg)
    {
        if (!config.FlashOnNewMessage)
            return bg;

        var isKeyword = pulseKeywordColor.HasValue;
        var duration = isKeyword ? KeywordPulseSeconds : PulseSeconds;
        var elapsed = (float)(ImGui.GetTime() - pulseStart);
        if (elapsed < 0f || elapsed >= duration)
            return bg;

        var intensity = 1f - elapsed / duration;
        var flash = pulseKeywordColor ?? new Vector4(1f, 1f, 1f, 1f);
        var strength = isKeyword ? 0.45f : 0.25f;
        return Vector4.Lerp(bg, flash, intensity * strength);
    }

    // ----- Combat mode: effective values while InCombat --------------------
    private bool CombatActive => config.CombatModeEnabled && inCombat();

    private float EffectiveFontSize => CombatActive ? config.CombatFontSize : config.FontSize;

    private bool TimestampsVisible => config.ShowTimestamps && !(CombatActive && config.CombatHideTimestamps);

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void Draw()
    {
        HandleFontZoom();
        UpdateFontSlot(normalFont, config.FontSize);
        if (config.CombatModeEnabled)
            UpdateFontSlot(combatFont, config.CombatFontSize);

        // Crisp text: push a real font built at the configured size. Until the
        // async atlas build lands (first frames, or right after a size change),
        // fall back to scaled drawing so text is never missing. (Plan §7)
        var handle = (CombatActive ? combatFont : normalFont).Handle;
        var crisp = handle is { Available: true };
        if (crisp)
            handle!.Push();
        else
            ImGui.SetWindowFontScale(Math.Max(0.5f, EffectiveFontSize / FontScaleBaseline));

        DrawStatusBar();
        ImGui.Separator();
        DrawMessages();

        if (crisp)
            handle!.Pop();
        else
            ImGui.SetWindowFontScale(1f);
    }

    /// <summary>Build (or debounced-rebuild) a slot's font handle at the wanted
    /// size. The debounce keeps slider drags and Ctrl+scroll from queueing an
    /// atlas rebuild per tick; the old handle keeps rendering until the new one
    /// is ready, then is disposed.</summary>
    private void UpdateFontSlot(FontSlot slot, float wantSize)
    {
        wantSize = MathF.Max(8f, wantSize);
        if (slot.Handle != null && wantSize == slot.BuiltSize)
        {
            slot.PendingSize = 0f;
            return;
        }

        if (slot.Handle != null)
        {
            if (wantSize != slot.PendingSize)
            {
                slot.PendingSize = wantSize;
                slot.PendingSince = ImGui.GetTime();
                return;
            }

            if (ImGui.GetTime() - slot.PendingSince < FontRebuildDebounceSeconds)
                return;
        }

        var old = slot.Handle;
        slot.Handle = fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(wantSize, null)));
        slot.BuiltSize = wantSize;
        slot.PendingSize = 0f;
        old?.Dispose();
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

        // Right-aligned Clear, for wiping last pull's callouts between pulls.
        // Unreachable in click-through mode by design; /mapleecho clear remains.
        var clearWidth = ImGui.CalcTextSize("Clear").X + ImGui.GetStyle().FramePadding.X * 2f;
        ImGui.SameLine(MathF.Max(0f, ImGui.GetContentRegionMax().X - clearWidth));
        if (ImGui.SmallButton("Clear"))
            store.Clear();
    }

    private string TimeFormat => config.Use24HourTime ? "HH:mm" : "h:mm tt";

    private void DrawMessages()
    {
        if (!ImGui.BeginChild("##messages", new Vector2(0, 0), false, ImGuiWindowFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        var messages = store.Snapshot();
        var extraSpacing = MathF.Max(0f, (config.LineSpacing - 1f) * EffectiveFontSize);

        // In combat, optionally show only the freshest callouts — last fight's
        // lines are noise exactly when reading time is scarcest.
        var cutoff = CombatActive && config.CombatRecentSeconds > 0
            ? DateTime.Now - TimeSpan.FromSeconds(config.CombatRecentSeconds)
            : DateTime.MinValue;

        string? lastSpeaker = null;
        DateTime lastTime = DateTime.MinValue;

        foreach (var m in messages)
        {
            if (m.ReceivedAt < cutoff)
                continue;

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
        // Manual scroll-up pauses; scrolling back to the bottom resumes — or the
        // pill click forces one jump regardless of the auto-scroll setting. (Plan §7)
        if ((config.AutoScroll && stickToBottom) || jumpToBottom)
        {
            ImGui.SetScrollHereY(1f);
            jumpToBottom = false;
        }

        stickToBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f;
        if (stickToBottom && messages.Count > 0)
            lastSeenAtBottomId = messages[^1].SourceMessageId;

        ImGui.EndChild();

        DrawNewMessagesPill(messages);
    }

    /// <summary>
    /// Floating "N new messages ↓" pill over the bottom of the log while
    /// scrolled up and callouts are arriving below — the paused state must
    /// never read as silence. Click jumps to the newest. In click-through mode
    /// it's informative only (visible, not clickable). (Plan §7)
    /// </summary>
    private void DrawNewMessagesPill(IReadOnlyList<RelayMessage> messages)
    {
        if (stickToBottom)
            return;

        var unseen = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].SourceMessageId == lastSeenAtBottomId)
                break;
            unseen++;
        }

        if (unseen == 0)
            return;

        // The just-ended messages child is the last item; center over its bottom.
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var label = unseen == 1 ? "1 new message ↓" : $"{unseen} new messages ↓";
        var size = ImGui.CalcTextSize(label);
        var pos = new Vector2(min.X + (max.X - min.X - size.X) * 0.5f, max.Y - size.Y - 10f);
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.SmallButton(label))
            jumpToBottom = true;
    }

    /// <summary>
    /// Ctrl+scroll over the window resizes the text — the fastest possible
    /// "I can't read this right now" recovery, no settings trip. Adjusts the
    /// combat size while the combat profile is active, the normal size
    /// otherwise, and persists like the sliders do.
    /// </summary>
    private void HandleFontZoom()
    {
        var io = ImGui.GetIO();
        if (!io.KeyCtrl || io.MouseWheel == 0f || !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            return;

        var delta = io.MouseWheel;
        if (CombatActive)
            config.CombatFontSize = Math.Clamp(config.CombatFontSize + delta, 10f, 48f);
        else
            config.FontSize = Math.Clamp(config.FontSize + delta, 10f, 48f);
        save();

        io.MouseWheel = 0f; // consumed — don't also scroll the log
    }

    private void DrawHeader(RelayMessage m)
    {
        if (config.ShowSpeakerName)
        {
            var color = SpeakerColor(m.Speaker);
            ImGui.TextColored(color, m.Speaker);
            if (TimestampsVisible)
            {
                ImGui.SameLine();
                ImGui.TextColored(config.TimestampColor, $"  {m.ReceivedAt.ToString(TimeFormat)}");
            }
        }
        else if (TimestampsVisible)
        {
            ImGui.TextColored(config.TimestampColor, m.ReceivedAt.ToString(TimeFormat));
        }
    }

    private void DrawBody(RelayMessage m)
    {
        if (MessageTransform.FirstKeywordMatch(m.Text, config.KeywordRules) is { } kw)
        {
            // Marker + whole-line tint in the matched keyword's own color, so
            // the highlight says which mechanic it is before the word is read.
            // Word-level inline color is a later refinement (hard to combine
            // with wrapping). (Plan §8b)
            ImGui.TextColored(kw.Color, "▸"); // ▸
            ImGui.SameLine();
            PushWrappedColored(kw.Color, m.Text);
        }
        else
        {
            PushWrappedColored(config.TextColor, m.Text);
        }

        DrawMessageContextMenu(m);
    }

    /// <summary>
    /// Right-click a line to act on it in the moment: copy it, or turn a
    /// mistranscription into a glossary rule while it's on screen — corrections
    /// happen when the failure is seen, not from memory after the raid.
    /// Unreachable in click-through mode, like every mouse interaction here.
    /// </summary>
    private void DrawMessageContextMenu(RelayMessage m)
    {
        if (!ImGui.BeginPopupContextItem($"##msgctx{m.SourceMessageId}"))
            return;

        if (ImGui.MenuItem("Copy text"))
            ImGui.SetClipboardText(m.Text);
        if (ImGui.MenuItem("Add glossary rule from this line…"))
            addGlossaryRule(m.Text);

        ImGui.EndPopup();
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
        normalFont.Handle?.Dispose();
        combatFont.Handle?.Dispose();
    }
}

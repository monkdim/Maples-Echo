using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MaplesEcho.Services;

namespace MaplesEcho.Windows;

/// <summary>
/// Settings UI. Everything visual is adjustable here at runtime — Maple tunes
/// it, we don't. Includes connect-on-save with distinct failure diagnostics,
/// per-speaker colors, glossary/keyword rules, one-click presets, and a
/// config export/import blob. (Plan §6, §9, §10b)
/// </summary>
public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly DiscordRelayService discord;
    private readonly MessageStore store;
    private readonly Action save;

    private bool revealToken;
    private string channelIdBuffer = string.Empty;
    private string webhookIdBuffer = string.Empty;
    private string fetchResult = string.Empty;

    private string newGlossaryFind = string.Empty;
    private string newGlossaryReplace = string.Empty;
    private string newKeyword = string.Empty;
    private string newSpeaker = string.Empty;

    private string importBuffer = string.Empty;
    private string backupNotice = string.Empty;
    private string presetNotice = string.Empty;
    private bool includeTokenInExport;

    public ConfigWindow(Configuration config, DiscordRelayService discord, MessageStore store, Action save)
        : base("Maple's Echo — Settings##MaplesEchoConfig")
    {
        this.config = config;
        this.discord = discord;
        this.store = store;
        this.save = save;

        Size = new Vector2(560, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(1600, 1600),
        };

        channelIdBuffer = config.ChannelId == 0 ? string.Empty : config.ChannelId.ToString();
        webhookIdBuffer = config.SourceWebhookId == 0 ? string.Empty : config.SourceWebhookId.ToString();
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##mapleTabs"))
        {
            if (ImGui.BeginTabItem("Connection")) { DrawConnection(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Appearance")) { DrawAppearance(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Combat")) { DrawCombat(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Speakers")) { DrawSpeakers(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Glossary & Keywords")) { DrawGlossary(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Presets")) { DrawPresets(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Backup")) { DrawBackup(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    // ---------------------------------------------------------------- Connection
    private void DrawConnection()
    {
        DrawStatusLine();
        ImGui.Separator();

        ImGui.TextWrapped("Bot token — entered here only, never committed. Stored unencrypted in " +
                          "Dalamud's config folder, so don't share config files or screenshots of this panel.");

        var token = config.BotToken;
        var flags = revealToken ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
        if (ImGui.InputText("Bot token", ref token, 200, flags))
            config.BotToken = token;
        ImGui.SameLine();
        ImGui.Checkbox("Reveal", ref revealToken);

        if (ImGui.InputText("Channel ID", ref channelIdBuffer, 40))
        {
            if (ulong.TryParse(channelIdBuffer.Trim(), out var id))
                config.ChannelId = id;
        }

        var webhookOnly = config.WebhookMessagesOnly;
        if (ImGui.Checkbox("Webhook messages only (recommended — hides bot command replies)", ref webhookOnly))
            config.WebhookMessagesOnly = webhookOnly;

        if (ImGui.InputText("Pin to webhook ID (optional, 0 = any)", ref webhookIdBuffer, 40))
            config.SourceWebhookId = ulong.TryParse(webhookIdBuffer.Trim(), out var wid) ? wid : 0;

        var prefix = config.SpeakerPrefix;
        if (ImGui.InputText("Speaker prefix (stripped from author name)", ref prefix, 60))
            config.SpeakerPrefix = prefix;

        ImGui.Separator();

        if (ImGui.Button("Save & Connect"))
        {
            // Trim happens inside StartAsync; connect immediately so the result
            // is visible now, not after a restart. (Plan §10b)
            config.BotToken = config.BotToken.Trim();
            save();
            discord.StartAsync(config.BotToken, config.ChannelId);
        }

        ImGui.SameLine();
        if (ImGui.Button("Fetch last 5 (test pipeline)"))
        {
            fetchResult = "Fetching…";
            _ = RunFetchAsync();
        }

        if (!string.IsNullOrEmpty(fetchResult))
            ImGui.TextWrapped(fetchResult);
    }

    private async Task RunFetchAsync()
    {
        var result = await discord.FetchRecentAsync(5);
        fetchResult = result;
    }

    private void DrawStatusLine()
    {
        var (color, label) = discord.State switch
        {
            RelayState.Connected => (new Vector4(0.30f, 0.85f, 0.40f, 1f), "Connected"),
            RelayState.Connecting => (new Vector4(0.95f, 0.80f, 0.30f, 1f), "Connecting…"),
            RelayState.ConnectedNoContent => (new Vector4(0.95f, 0.55f, 0.20f, 1f), "Connected, no content"),
            RelayState.InvalidToken => (new Vector4(0.90f, 0.25f, 0.25f, 1f), "Invalid token"),
            _ => (new Vector4(0.90f, 0.25f, 0.25f, 1f), "Disconnected"),
        };
        ImGui.TextColored(color, "●");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.TextWrapped(discord.StatusDetail);
    }

    // ---------------------------------------------------------------- Appearance
    private void DrawAppearance()
    {
        ColorEdit("Background", () => config.WindowBackgroundColor, v => config.WindowBackgroundColor = v);
        var opacity = config.BackgroundOpacity;
        if (ImGui.SliderFloat("Background opacity", ref opacity, 0f, 1f))
            config.BackgroundOpacity = opacity;
        SaveOnRelease();

        ColorEditContrast("Message text", () => config.TextColor, v => config.TextColor = v);
        ColorEdit("Speaker name", () => config.AuthorColor, v => config.AuthorColor = v);
        ColorEdit("Timestamp", () => config.TimestampColor, v => config.TimestampColor = v);
        ColorEdit("Keyword highlight (default for new keywords)", () => config.KeywordColor, v => config.KeywordColor = v);

        ImGui.Separator();

        var fontSize = config.FontSize;
        if (ImGui.SliderFloat("Font size", ref fontSize, 10f, 42f))
            config.FontSize = fontSize;
        SaveOnRelease();

        var spacing = config.LineSpacing;
        if (ImGui.SliderFloat("Line spacing", ref spacing, 1f, 2.5f))
            config.LineSpacing = spacing;
        SaveOnRelease();

        ImGui.Separator();

        CheckSave("Show speaker names", () => config.ShowSpeakerName, v => config.ShowSpeakerName = v);
        CheckSave("Show timestamps", () => config.ShowTimestamps, v => config.ShowTimestamps = v);
        CheckSave("24-hour timestamps", () => config.Use24HourTime, v => config.Use24HourTime = v);
        CheckSave("Auto-scroll to newest", () => config.AutoScroll, v => config.AutoScroll = v);
        CheckSave("Flash background on new message (stronger on keyword hits)", () => config.FlashOnNewMessage, v => config.FlashOnNewMessage = v);
        CheckSave("Merge consecutive from same speaker", () => config.MergeConsecutive, v => config.MergeConsecutive = v);

        var mergeWindow = config.MergeWindowSeconds;
        if (ImGui.SliderInt("Merge window (seconds)", ref mergeWindow, 1, 30))
            config.MergeWindowSeconds = mergeWindow;
        SaveOnRelease();

        var maxMessages = config.MaxMessages;
        if (ImGui.SliderInt("Max messages kept", ref maxMessages, 20, 1000))
        {
            config.MaxMessages = maxMessages;
            store.SetCapacity(maxMessages);
        }

        SaveOnRelease();

        ImGui.Separator();

        CheckSave("Hide title bar (prevents focus theft mid-fight)", () => config.HideTitleBar, v => config.HideTitleBar = v);
        CheckSave("Lock window position", () => config.LockWindowPosition, v => config.LockWindowPosition = v);
        CheckSave("Click-through — window ignores the mouse (uncheck here to move or scroll it)", () => config.ClickThrough, v => config.ClickThrough = v);
        CheckSave("Hide during cutscenes", () => config.HideDuringCutscenes, v => config.HideDuringCutscenes = v);
        CheckSave("Also mirror to game chat log", () => config.AlsoPrintToGameChat, v => config.AlsoPrintToGameChat = v);
    }

    // ---------------------------------------------------------------- Combat
    private void DrawCombat()
    {
        ImGui.TextWrapped("While in combat, the window can switch to a combat profile automatically: " +
                          "bigger text, fewer distractions, only the freshest callouts. It reverts the " +
                          "moment combat ends.");
        ImGui.Separator();

        CheckSave("Enable combat mode", () => config.CombatModeEnabled, v => config.CombatModeEnabled = v);

        var combatFont = config.CombatFontSize;
        if (ImGui.SliderFloat("Combat font size", ref combatFont, 10f, 48f))
            config.CombatFontSize = combatFont;
        SaveOnRelease();

        CheckSave("Hide timestamps in combat", () => config.CombatHideTimestamps, v => config.CombatHideTimestamps = v);

        var recent = config.CombatRecentSeconds;
        if (ImGui.SliderInt("Only show the last N seconds (0 = all)", ref recent, 0, 120))
            config.CombatRecentSeconds = recent;
        SaveOnRelease();
    }

    // ---------------------------------------------------------------- Speakers
    private void DrawSpeakers()
    {
        CheckSave("Use per-speaker colors", () => config.UseSpeakerColors, v => config.UseSpeakerColors = v);
        ImGui.TextWrapped("Assign a color to each speaker so you can map colors to roles or party " +
                          "positions. Unassigned speakers get an automatic fallback until you override it.");
        ImGui.Separator();

        foreach (var speaker in config.KnownSpeakers.ToArray())
        {
            ImGui.PushID(speaker);
            var color = config.SpeakerColors.TryGetValue(speaker, out var c) ? c : ColorUtil.HashColor(speaker);
            if (ImGui.ColorEdit4($"{speaker}##spk", ref color, ImGuiColorEditFlags.NoInputs))
            {
                config.SpeakerColors[speaker] = color;
                save();
            }

            // Forget a stale name (bot renames, one-time guests). A speaker
            // still active in the channel simply reappears on their next line.
            ImGui.SameLine();
            if (ImGui.SmallButton("X"))
            {
                config.KnownSpeakers.Remove(speaker);
                config.SpeakerColors.Remove(speaker);
                save();
                ImGui.PopID();
                continue;
            }

            var contrast = ColorUtil.ContrastRatio(color, config.WindowBackgroundColor);
            if (contrast < 4.5f)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.95f, 0.6f, 0.2f, 1f), $"low contrast ({contrast:0.0}:1)");
            }

            ImGui.PopID();
        }

        ImGui.Separator();
        ImGui.InputText("Add speaker manually", ref newSpeaker, 60);
        ImGui.SameLine();
        if (ImGui.Button("Add##spk") && !string.IsNullOrWhiteSpace(newSpeaker))
        {
            if (!config.KnownSpeakers.Contains(newSpeaker))
                config.KnownSpeakers.Add(newSpeaker);
            newSpeaker = string.Empty;
            save();
        }
    }

    // ---------------------------------------------------------------- Glossary
    private void DrawGlossary()
    {
        ImGui.TextWrapped("Glossary rules fix common mistranscriptions (find → replace), applied " +
                          "before display. Grow the list as the static finds failures.");

        for (var i = 0; i < config.Glossary.Count; i++)
        {
            var rule = config.Glossary[i];
            ImGui.PushID(i);
            var enabled = rule.Enabled;
            if (ImGui.Checkbox("##en", ref enabled)) { rule.Enabled = enabled; save(); }
            ImGui.SameLine();
            var find = rule.Find;
            if (ImGui.InputText("find", ref find, 100)) { rule.Find = find; save(); }
            ImGui.SameLine();
            var repl = rule.Replace;
            if (ImGui.InputText("replace", ref repl, 100)) { rule.Replace = repl; save(); }
            ImGui.SameLine();
            if (ImGui.Button("X")) { config.Glossary.RemoveAt(i); save(); ImGui.PopID(); break; }
            ImGui.PopID();
        }

        ImGui.InputText("New find", ref newGlossaryFind, 100);
        ImGui.InputText("New replace", ref newGlossaryReplace, 100);
        if (ImGui.Button("Add rule") && !string.IsNullOrWhiteSpace(newGlossaryFind))
        {
            config.Glossary.Add(new GlossaryRule { Find = newGlossaryFind, Replace = newGlossaryReplace });
            newGlossaryFind = string.Empty;
            newGlossaryReplace = string.Empty;
            save();
        }

        ImGui.Separator();
        ImGui.TextWrapped("Keywords are emphasized on screen when they appear (your name, \"stack\", " +
                          "\"spread\", \"tank buster\", \"stop\"). Each keyword has its own highlight " +
                          "color; the first match in the list wins, so put the most important at the top. " +
                          "\"Whole word\" keeps short callouts like \"in\" from lighting up \"point\".");

        for (var i = 0; i < config.KeywordRules.Count; i++)
        {
            var rule = config.KeywordRules[i];
            ImGui.PushID(1000 + i);
            var enabled = rule.Enabled;
            if (ImGui.Checkbox("##kwen", ref enabled)) { rule.Enabled = enabled; save(); }
            ImGui.SameLine();
            var color = rule.Color;
            if (ImGui.ColorEdit4("##kwcol", ref color, ImGuiColorEditFlags.NoInputs)) { rule.Color = color; save(); }
            ImGui.SameLine();
            var word = rule.Word;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputText("##kwword", ref word, 60)) { rule.Word = word; save(); }
            ImGui.SameLine();
            var whole = rule.WholeWord;
            if (ImGui.Checkbox("whole word", ref whole)) { rule.WholeWord = whole; save(); }
            ImGui.SameLine();
            if (ImGui.Button("X")) { config.KeywordRules.RemoveAt(i); save(); ImGui.PopID(); break; }
            ImGui.PopID();
        }

        ImGui.InputText("New keyword", ref newKeyword, 60);
        ImGui.SameLine();
        if (ImGui.Button("Add##kw") && !string.IsNullOrWhiteSpace(newKeyword))
        {
            config.KeywordRules.Add(new KeywordRule { Word = newKeyword.Trim(), Color = config.KeywordColor });
            newKeyword = string.Empty;
            save();
        }
    }

    // ---------------------------------------------------------------- Presets
    private void DrawPresets()
    {
        ImGui.TextWrapped("One-click starting points. Apply one, then fine-tune on the other tabs.");
        if (ImGui.Button("Dark Blue / High Contrast")) { Presets.DarkBlueHighContrast(config); save(); }
        if (ImGui.Button("Large Text")) { Presets.LargeText(config); save(); }
        if (ImGui.Button("Minimal")) { Presets.Minimal(config); save(); }

        ImGui.Separator();
        ImGui.TextWrapped("Starter content: a curated FFXIV keyword and glossary set (stack, spread, " +
                          "in, out, …). Merges into your lists without touching anything you've added — " +
                          "edit or delete freely afterwards on the Glossary & Keywords tab.");
        if (ImGui.Button("Load FFXIV starter keywords & glossary"))
        {
            var (kw, gl) = Presets.ApplyStarterPack(config);
            save();
            presetNotice = kw + gl == 0
                ? "Nothing to add — the starter set is already in your lists."
                : $"Added {kw} keyword(s) and {gl} glossary rule(s).";
        }

        if (!string.IsNullOrEmpty(presetNotice))
            ImGui.TextWrapped(presetNotice);
    }

    // ---------------------------------------------------------------- Backup
    private void DrawBackup()
    {
        ImGui.TextWrapped("Export your setup — colors, fonts, speakers, glossary, keywords — as one " +
                          "string. By default the bot token is NOT included, so the string is safe to " +
                          "share as a settings pack. Import restores everything from a pasted string; " +
                          "a token-free import keeps your current token and connection.");

        ImGui.Checkbox("Include bot token (treat the exported string as a secret!)", ref includeTokenInExport);
        if (ImGui.Button("Export to clipboard"))
        {
            var blob = ConfigBlob.Export(config, includeTokenInExport);
            ImGui.SetClipboardText(blob);
            backupNotice = includeTokenInExport
                ? "Exported to clipboard. It contains your token — keep it private."
                : "Exported to clipboard (token-free — safe to share).";
        }

        ImGui.Separator();
        ImGui.InputTextMultiline("##import", ref importBuffer, 8000, new Vector2(-1, 120));
        if (ImGui.Button("Import from box"))
        {
            if (ConfigBlob.Import(importBuffer, config))
            {
                channelIdBuffer = config.ChannelId == 0 ? string.Empty : config.ChannelId.ToString();
                webhookIdBuffer = config.SourceWebhookId == 0 ? string.Empty : config.SourceWebhookId.ToString();
                store.SetCapacity(config.MaxMessages);
                save();
                discord.StartAsync(config.BotToken, config.ChannelId);
                backupNotice = "Imported and reconnecting.";
            }
            else
            {
                backupNotice = "Import failed — the string wasn't a valid Maple's Echo backup.";
            }
        }

        if (!string.IsNullOrEmpty(backupNotice))
            ImGui.TextWrapped(backupNotice);
    }

    // ---------------------------------------------------------------- Helpers
    private void ColorEdit(string label, Func<Vector4> get, Action<Vector4> set)
    {
        var v = get();
        if (ImGui.ColorEdit4(label, ref v))
        {
            set(v);
            save();
        }
    }

    private void ColorEditContrast(string label, Func<Vector4> get, Action<Vector4> set)
    {
        ColorEdit(label, get, set);
        var contrast = ColorUtil.ContrastRatio(get(), config.WindowBackgroundColor);
        var ok = contrast >= 7f;
        ImGui.SameLine();
        ImGui.TextColored(ok ? new Vector4(0.3f, 0.85f, 0.4f, 1f) : new Vector4(0.95f, 0.6f, 0.2f, 1f),
            ok ? $"{contrast:0.0}:1 AAA" : $"{contrast:0.0}:1 low");
    }

    /// <summary>Persist the slider drawn immediately above, once its drag ends.
    /// Sliders apply live for preview but only hit disk on release.</summary>
    private void SaveOnRelease()
    {
        if (ImGui.IsItemDeactivatedAfterEdit())
            save();
    }

    private void CheckSave(string label, Func<bool> get, Action<bool> set)
    {
        var v = get();
        if (ImGui.Checkbox(label, ref v))
        {
            set(v);
            save();
        }
    }

    public void Dispose()
    {
    }
}

using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MaplesEcho.Services;
using MaplesEcho.Windows;

namespace MaplesEcho;

/// <summary>
/// Maple's Echo — a one-way Discord → in-game accessibility relay.
///
/// In FFXIV lore the Echo lets its bearer understand speech they otherwise
/// could not; that is exactly this plugin's job. Internal name MaplesEcho —
/// never bare "Echo" (collides with /echo and XivChatType.Echo).
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mapleecho";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly IChatGui chatGui;

    private readonly Configuration config;
    private readonly MessageStore store;
    private readonly DiscordRelayService discord;

    private readonly WindowSystem windowSystem = new("MaplesEcho");
    private readonly RelayWindow relayWindow;
    private readonly ConfigWindow configWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IFramework framework,
        IChatGui chatGui)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.framework = framework;
        this.chatGui = chatGui;

        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        store = new MessageStore(config.MaxMessages);

        // The relay: receives Discord events, marshals to the framework thread,
        // transforms, and pushes into the store. (Plan §8)
        discord = new DiscordRelayService(config, store, framework, log)
        {
            OnSpeakerSeen = RememberSpeaker,
            OnMirrorToGameChat = MirrorToGameChat,
        };

        relayWindow = new RelayWindow(config, store, discord);
        configWindow = new ConfigWindow(config, discord, Save);
        windowSystem.AddWindow(relayWindow);
        windowSystem.AddWindow(configWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the relay window. \"/mapleecho config\" opens settings.",
        });

        pluginInterface.UiBuilder.Draw += DrawUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;

        // Connect on load if a token is already configured. Silent failure is
        // indistinguishable from a quiet channel with no audio cue, so the status
        // indicator is the source of truth. (Plan §9, §10b)
        if (!string.IsNullOrWhiteSpace(config.BotToken))
            discord.StartAsync(config.BotToken, config.ChannelId);
    }

    private void DrawUi()
    {
        // Respect cutscene hiding when the option is on. (Plan §7)
        if (config.HideDuringCutscenes && pluginInterface.UiBuilder.CutsceneActive)
            return;

        windowSystem.Draw();
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            configWindow.Toggle();
            return;
        }

        relayWindow.Toggle();
    }

    private void OpenConfig() => configWindow.Toggle();

    private void OpenMain() => relayWindow.Toggle();

    /// <summary>
    /// Remember a speaker so the config window can offer real names to assign.
    /// Runs on the framework thread (marshalled by the relay). (Plan §7)
    /// </summary>
    private void RememberSpeaker(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return;

        if (!config.KnownSpeakers.Contains(speaker))
        {
            config.KnownSpeakers.Add(speaker);
            Save();
        }
    }

    /// <summary>Optional secondary mirror into the native chat log. (Plan §2)</summary>
    private void MirrorToGameChat(RelayMessage message)
    {
        if (!config.AlsoPrintToGameChat)
            return;

        try
        {
            var line = config.ShowSpeakerName
                ? $"[{message.Speaker}] {message.Text}"
                : message.Text;
            chatGui.Print(line);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to mirror message to game chat.");
        }
    }

    public void Save() => pluginInterface.SavePluginConfig(config);

    public void Dispose()
    {
        // Order matters: stop the gateway first so no event fires into a
        // half-disposed plugin, then tear down UI. A leaked gateway connection
        // per reload gets the bot rate-limited fast. (Plan §8)
        discord.Dispose();

        pluginInterface.UiBuilder.Draw -= DrawUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMain;

        windowSystem.RemoveAllWindows();
        relayWindow.Dispose();
        configWindow.Dispose();

        commandManager.RemoveHandler(CommandName);
    }
}

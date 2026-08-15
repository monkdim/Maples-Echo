using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Dalamud.Plugin.Services;

namespace MaplesEcho.Services;

public enum RelayState
{
    Disconnected,
    Connecting,
    Connected,
    InvalidToken,
    /// <summary>Connected, but arriving messages have empty content — almost
    /// always the Message Content privileged intent not enabled. (Plan §10b)</summary>
    ConnectedNoContent,
}

/// <summary>
/// Owns the Discord.Net gateway connection and turns raw messages into
/// <see cref="RelayMessage"/>s in the store. Discord.Net fires on its own async
/// context, so anything touching game/plugin state is marshalled to the
/// framework thread. Handler bodies are wrapped in try/catch because Discord.Net
/// swallows exceptions thrown inside them. (Plan §5, §8)
/// </summary>
public sealed class DiscordRelayService : IDisposable
{
    private readonly Configuration config;
    private readonly MessageStore store;
    private readonly IFramework framework;
    private readonly IPluginLog log;

    private DiscordSocketClient? client;
    private bool disposed;

    /// <summary>Called (on the framework thread) with each speaker name seen.</summary>
    public Action<string>? OnSpeakerSeen { get; set; }

    /// <summary>Called (on the framework thread) with each newly stored message —
    /// the optional native-chat mirror and keyword sound hang off this. In-place
    /// edits do not re-fire it, so corrections never re-ping.</summary>
    public Action<RelayMessage>? OnMessageRelayed { get; set; }

    // --- Status, read from the draw thread ---
    public RelayState State { get; private set; } = RelayState.Disconnected;
    public string StatusDetail { get; private set; } = "Not connected. Paste a bot token in settings.";
    public DateTime? LastMessageReceived { get; private set; }

    public DiscordRelayService(Configuration config, MessageStore store, IFramework framework, IPluginLog log)
    {
        this.config = config;
        this.store = store;
        this.framework = framework;
        this.log = log;
    }

    /// <summary>
    /// Connect (or reconnect) with the given token. Fire-and-forget from the
    /// caller's perspective; result is reported through <see cref="State"/>.
    /// Token whitespace is trimmed — portal copy/paste picks up stray spaces
    /// constantly and the resulting failure looks like a mystery. (Plan §10b)
    /// </summary>
    public async void StartAsync(string token, ulong channelId)
    {
        token = token?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            SetState(RelayState.Disconnected, "No token entered.");
            return;
        }

        try
        {
            await StopInternalAsync();

            SetState(RelayState.Connecting, "Connecting…");

            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds
                                 | GatewayIntents.GuildMessages
                                 | GatewayIntents.MessageContent,
                MessageCacheSize = 0,
                LogLevel = LogSeverity.Info,
            });

            client.Log += OnClientLog;
            client.MessageReceived += OnMessageReceived;
            client.MessageUpdated += OnMessageUpdated;
            client.Ready += OnReady;
            client.Disconnected += OnDisconnected;

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
        }
        catch (Discord.Net.HttpException http) when (http.HttpCode == System.Net.HttpStatusCode.Unauthorized)
        {
            log.Error(http, "Discord login rejected the token.");
            SetState(RelayState.InvalidToken, "Invalid token — check the paste, or regenerate it in the Developer Portal.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to start Discord relay.");
            SetState(RelayState.InvalidToken, $"Login failed: {ex.Message}");
        }
    }

    private Task OnReady()
    {
        // Connected and the gateway is ready. Whether messages actually arrive
        // (Message Content intent) is proven only when one does.
        SetState(RelayState.Connected, "Connected. Waiting for messages…");
        return Task.CompletedTask;
    }

    private Task OnDisconnected(Exception ex)
    {
        // Discord.Net auto-reconnects; just reflect it. Make it obvious — there's
        // no audio cue when the relay dies, and an empty window reads as silence. (Plan §9)
        if (!disposed)
            SetState(RelayState.Disconnected, "Disconnected — reconnecting…");
        return Task.CompletedTask;
    }

    private Task OnClientLog(LogMessage msg)
    {
        log.Debug($"[Discord] {msg.Severity}: {msg.Message} {msg.Exception}");
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(SocketMessage msg)
    {
        try
        {
            if (!PassesFilter(msg))
                return Task.CompletedTask;

            // Empty body after transform, while the message passed the filter,
            // is the signature of the Message Content intent being disabled. (Plan §10b)
            if (BuildRelay(msg) is not { } relay)
            {
                SetState(RelayState.ConnectedNoContent,
                    "Connected, but messages arrive empty — enable the Message Content intent in the Developer Portal.");
                return Task.CompletedTask;
            }

            Enqueue(relay);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error handling a Discord message.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Transcription bots commonly post a partial line and then EDIT the message
    /// as the transcription finalizes. Without this, the correction never reaches
    /// the screen and a wrong callout stays wrong. The edited message runs through
    /// the same filter + transform as a new one, then patches the stored line by
    /// id in place — no reordering, no duplicate. Edits to messages that have
    /// already left the ring buffer (or predate the session) are ignored rather
    /// than appended out of order. (MessageCacheSize is 0, so the "before" state
    /// is unavailable — and unnecessary, the id is enough.)
    /// </summary>
    private Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
    {
        try
        {
            if (!PassesFilter(after))
                return Task.CompletedTask;

            if (BuildRelay(after) is not { } relay)
                return Task.CompletedTask;

            _ = framework.RunOnFrameworkThread(() => store.UpdateText(after.Id, relay.Text));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error handling a Discord message edit.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Filter + transform a message into a display-ready <see cref="RelayMessage"/>,
    /// or null if it has no readable body. Pure and thread-safe; shared by the live
    /// gateway path and the manual fetch so both relay identically. (Plan §8b)
    /// </summary>
    private RelayMessage? BuildRelay(IMessage msg)
    {
        var body = MessageTransform.TransformBody(msg.Content, config.Glossary);

        // Defensive embed fallback: some bots put text in embeds. (Plan §5)
        if (string.IsNullOrWhiteSpace(body) && msg.Embeds.Count > 0)
        {
            var embedText = string.Join(" ",
                msg.Embeds.Select(e => e.Description ?? e.Title ?? string.Empty));
            body = MessageTransform.TransformBody(embedText, config.Glossary);
        }

        if (string.IsNullOrWhiteSpace(body))
            return null;

        return new RelayMessage
        {
            Speaker = MessageTransform.ExtractSpeaker(msg.Author.Username, config.SpeakerPrefix),
            Text = body,
            ReceivedAt = DateTime.Now,
            SourceMessageId = msg.Id,
        };
    }

    /// <summary>Push a built message into the store, on the framework thread. (Plan §8)</summary>
    private void Enqueue(RelayMessage relay)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            store.Add(relay);
            LastMessageReceived = relay.ReceivedAt;
            if (State != RelayState.Connected)
                SetState(RelayState.Connected, "Connected.");
            OnSpeakerSeen?.Invoke(relay.Speaker);
            OnMessageRelayed?.Invoke(relay);
        });
    }

    private bool PassesFilter(IMessage msg)
    {
        if (config.ChannelId != 0 && msg.Channel.Id != config.ChannelId)
            return false;

        if (config.WebhookMessagesOnly)
        {
            var isWebhook = msg.Source == MessageSource.Webhook || msg.Author.IsWebhook;
            if (!isWebhook)
                return false;

            // Optional pin to a specific webhook id. For a webhook-sourced
            // message the author implements IWebhookUser.
            if (config.SourceWebhookId != 0)
            {
                var webhookId = (msg.Author as IWebhookUser)?.WebhookId ?? 0UL;
                if (webhookId != config.SourceWebhookId)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Diagnostic: pull the last few messages from the configured channel so the
    /// pipeline can be verified end-to-end without waiting for someone to talk.
    /// Distinguishes channel-not-found / no-access from other failures. (Plan §10b)
    /// </summary>
    public async Task<string> FetchRecentAsync(int count = 5)
    {
        if (client is null || State == RelayState.Disconnected || State == RelayState.InvalidToken)
            return "Not connected — connect first.";

        try
        {
            if (await client.GetChannelAsync(config.ChannelId) is not IMessageChannel channel)
                return "Channel not found or the bot has no access — check the channel id and the invite permissions.";

            var messages = await channel.GetMessagesAsync(count).FlattenAsync();
            var list = messages.ToList();
            if (list.Count == 0)
                return "Channel reachable, but no recent messages.";

            var withContent = list.Count(m => !string.IsNullOrEmpty(m.Content));
            if (withContent == 0)
                return $"Fetched {list.Count} message(s), but all had empty content — enable the Message Content intent in the Developer Portal.";

            // GetMessagesAsync returns newest-first; relay oldest-first so they
            // land in the window in the right order — and actually SHOW, so this
            // button demonstrates the relay, not just reports on it.
            list.Reverse();
            var relayed = 0;
            foreach (var m in list)
            {
                if (!PassesFilter(m))
                    continue;
                if (BuildRelay(m) is { } relay)
                {
                    Enqueue(relay);
                    relayed++;
                }
            }

            if (relayed == 0)
                return $"Fetched {list.Count} message(s), but none passed the filter (webhook-only is on, so plain user/bot messages are skipped). Nothing to show.";

            return $"Fetched {list.Count} message(s) and relayed {relayed} into the window.";
        }
        catch (Exception ex)
        {
            log.Error(ex, "FetchRecentAsync failed.");
            return $"Fetch failed: {ex.Message}";
        }
    }

    private void SetState(RelayState state, string detail)
    {
        State = state;
        StatusDetail = detail;
    }

    private async Task StopInternalAsync()
    {
        if (client is null)
            return;

        client.MessageReceived -= OnMessageReceived;
        client.MessageUpdated -= OnMessageUpdated;
        client.Ready -= OnReady;
        client.Disconnected -= OnDisconnected;
        client.Log -= OnClientLog;

        try
        {
            await client.StopAsync();
            await client.LogoutAsync();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Error stopping Discord client.");
        }

        client.Dispose();
        client = null;
    }

    public void Dispose()
    {
        disposed = true;
        // Best-effort synchronous teardown on unload.
        try
        {
            StopInternalAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Error during Discord relay dispose.");
        }

        SetState(RelayState.Disconnected, "Stopped.");
    }
}

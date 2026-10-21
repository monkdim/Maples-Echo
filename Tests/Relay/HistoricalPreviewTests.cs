using System.Reflection;
using Dalamud.Plugin.Services;
using Discord;
using MaplesEcho.Services;
using Moq;
using Xunit;

namespace MaplesEcho.RelayTests;

public sealed class HistoricalPreviewTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 17, 20, 0, 0, TimeSpan.FromHours(-7));

    private static IMessage Message(ulong id, string text, ulong channelId = 42, bool webhook = true)
    {
        var author = new Mock<IUser>();
        author.SetupGet(x => x.Username).Returns("[Scriptly] Maple");
        author.SetupGet(x => x.IsWebhook).Returns(webhook);
        var channel = new Mock<IMessageChannel>();
        channel.SetupGet(x => x.Id).Returns(channelId);
        var message = new Mock<IMessage>();
        message.SetupGet(x => x.Id).Returns(id);
        message.SetupGet(x => x.Content).Returns(text);
        message.SetupGet(x => x.Timestamp).Returns(Created);
        message.SetupGet(x => x.Author).Returns(author.Object);
        message.SetupGet(x => x.Channel).Returns(channel.Object);
        message.SetupGet(x => x.Source).Returns(webhook ? MessageSource.Webhook : MessageSource.User);
        message.SetupGet(x => x.Embeds).Returns(Array.Empty<IEmbed>());
        return message.Object;
    }

    private sealed class Fixture
    {
        public Configuration Config { get; } = new() { ChannelId = 42 };
        public MessageStore Store { get; } = new(20);
        public DiscordRelayService Relay { get; }
        public int Arrivals { get; private set; }
        public int Speakers { get; private set; }
        public int StateChanges { get; private set; }

        public Fixture()
        {
            var framework = new Mock<IFramework>(MockBehavior.Strict);
            framework.Setup(x => x.RunOnFrameworkThread(It.IsAny<Action>()))
                .Returns((Action action) => { action(); return Task.CompletedTask; });
            Relay = new DiscordRelayService(Config, Store, framework.Object, Mock.Of<IPluginLog>());
            Relay.OnMessageRelayed = _ => Arrivals++;
            Relay.OnSpeakerSeen = _ => Speakers++;
            Relay.OnStateChanged = (_, _) => StateChanges++;
        }

        // Seed a gateway state without opening a network connection.
        public void SetState(RelayState state) =>
            typeof(DiscordRelayService).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Relay, [state, "fixture"]);

        public void DeliverLive(IMessage message)
        {
            var relay = typeof(DiscordRelayService).GetMethod("BuildRelay", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Relay, [message]);
            typeof(DiscordRelayService).GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Relay, [relay]);
        }
    }

    [Theory]
    [InlineData(RelayState.ConnectedNoContent)]
    [InlineData(RelayState.Connected)]
    public async Task RepeatedPreview_PreservesLiveStoreStatusAndArrivalCallbacks(RelayState state)
    {
        var f = new Fixture();
        f.Config.Glossary.Add(new GlossaryRule { Find = "spred", Replace = "spread", Enabled = true });
        f.Config.KeywordRules.Add(new KeywordRule { Word = "spread" });
        f.Config.PlaySoundOnKeyword = true;
        f.Config.AlsoPrintToGameChat = true;
        f.DeliverLive(Message(1, "earlier"));
        f.SetState(state);
        var before = f.Store.Snapshot();
        var lastLive = f.Relay.LastMessageReceived;
        var statusDetail = f.Relay.StatusDetail;
        var transitions = f.StateChanges;

        for (var i = 0; i < 3; i++)
        {
            var result = await f.Relay.FetchRecentAsync(() =>
                Task.FromResult<IReadOnlyList<IMessage>>([Message(1, "**spred**")]));
            var preview = Assert.Single(result.Messages);
            Assert.Equal("spread", preview.Text);
            Assert.Equal("Maple", preview.Speaker);
            Assert.Equal(Created, preview.CreatedAt);
            Assert.Equal(1UL, preview.SourceMessageId);
            Assert.Equal(before, f.Store.Snapshot());
            Assert.Equal(lastLive, f.Relay.LastMessageReceived);
            Assert.Equal(state, f.Relay.State);
            Assert.Equal(statusDetail, f.Relay.StatusDetail);
            Assert.Equal(transitions, f.StateChanges);
            Assert.Equal(1, f.Arrivals);
            Assert.Equal(1, f.Speakers);
        }
    }

    [Fact]
    public async Task Preview_ReportsFetchedMatchingAndReadableSeparately()
    {
        var f = new Fixture();
        var result = await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([
                Message(1, "stack"), Message(2, "", channelId: 42),
                Message(3, "other channel", channelId: 99), Message(4, "human", webhook: false)]));
        Assert.Single(result.Messages);
        Assert.Contains("Fetched 4; matched filters 2; readable 1", result.Summary);
        Assert.Contains("does not verify live delivery", result.Summary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("remove")]
    public async Task EmptyReadableContent_DoesNotDiagnoseIntentOrChangeLiveState(string text)
    {
        var f = new Fixture();
        f.Config.Glossary.Add(new GlossaryRule { Find = "remove", Replace = "", Enabled = true });
        f.SetState(RelayState.Connected);
        var result = await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([Message(1, text)]));
        Assert.Empty(result.Messages);
        Assert.Contains("readable 0", result.Summary);
        Assert.DoesNotContain("enable", result.Summary);
        Assert.Equal(RelayState.Connected, f.Relay.State);
        Assert.Null(f.Relay.LastMessageReceived);
    }

    [Fact]
    public async Task EmptyOrFailedHistory_DoesNotClaimRecovery()
    {
        var f = new Fixture();
        f.SetState(RelayState.ConnectedNoContent);
        var empty = await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([]));
        Assert.Empty(empty.Messages);
        Assert.Contains("Fetched 0", empty.Summary);
        var failed = await f.Relay.FetchRecentAsync(() =>
            Task.FromException<IReadOnlyList<IMessage>>(new InvalidOperationException("No access")));
        Assert.Empty(failed.Messages);
        Assert.Contains("failed: No access", failed.Summary);
        Assert.Equal(RelayState.ConnectedNoContent, f.Relay.State);
        Assert.Null(f.Relay.LastMessageReceived);
        Assert.Empty(f.Store.Snapshot());
        Assert.Equal(0, f.Arrivals);
    }

    [Fact]
    public async Task Preview_UsesEmbedFallbackAndPreservesInputOrder()
    {
        var f = new Fixture();
        var embed = new Mock<IEmbed>();
        embed.SetupGet(x => x.Description).Returns("**spread**");
        var embedded = Mock.Get(Message(1, ""));
        embedded.SetupGet(x => x.Embeds).Returns(new[] { embed.Object });
        var result = await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([embedded.Object, Message(2, "stack")]));
        Assert.Equal(new ulong[] { 1, 2 }, result.Messages.Select(x => x.SourceMessageId));
        Assert.Equal("spread", result.Messages[0].Text);
        Assert.Empty(f.Store.Snapshot());
        Assert.Equal(0, f.Arrivals);
    }

    [Fact]
    public async Task Preview_HonorsPinnedWebhook()
    {
        var f = new Fixture();
        f.Config.SourceWebhookId = 123;
        var author = new Mock<IWebhookUser>();
        author.SetupGet(x => x.Username).Returns("[Scriptly] Maple");
        author.SetupGet(x => x.WebhookId).Returns(123UL);
        var pinned = Mock.Get(Message(1, "stack"));
        pinned.SetupGet(x => x.Author).Returns(author.Object);
        var result = await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([pinned.Object, Message(2, "other webhook")]));
        Assert.Equal(1UL, Assert.Single(result.Messages).SourceMessageId);
        Assert.Contains("Fetched 2; matched filters 1; readable 1", result.Summary);
    }

    [Fact]
    public async Task PendingPreview_DoesNotBlockOrModifyALiveArrival()
    {
        var f = new Fixture();
        var source = new TaskCompletionSource<IReadOnlyList<IMessage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Relay.FetchRecentAsync(() => source.Task);
        Assert.False(pending.IsCompleted);
        f.DeliverLive(Message(2, "live stack"));
        var live = Assert.Single(f.Store.Snapshot());
        var lastLive = f.Relay.LastMessageReceived;
        source.SetResult([Message(1, "old spread")]);
        Assert.Single((await pending).Messages);
        Assert.Same(live, Assert.Single(f.Store.Snapshot()));
        Assert.Equal(lastLive, f.Relay.LastMessageReceived);
        Assert.Equal(1, f.Arrivals);
    }

    [Fact]
    public async Task DisconnectedPublicPreview_ReturnsAnExplanationWithoutChangingState()
    {
        var f = new Fixture();
        var result = await f.Relay.FetchRecentAsync();
        Assert.Empty(result.Messages);
        Assert.Contains("Not connected", result.Summary);
        Assert.Equal(RelayState.Disconnected, f.Relay.State);
        Assert.Equal(0, f.StateChanges);
        Assert.Empty(f.Store.Snapshot());
    }

    [Fact]
    public async Task LiveArrivalAfterPreview_StillArrivesOnceAndCanBeEditedInPlace()
    {
        var f = new Fixture();
        await f.Relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([Message(1, "old spread")]));
        f.DeliverLive(Message(2, "stack"));
        var live = Assert.Single(f.Store.Snapshot());
        Assert.Equal(2UL, live.SourceMessageId);
        Assert.Equal("stack", live.Text);
        Assert.Equal(1, f.Arrivals);
        Assert.NotNull(f.Relay.LastMessageReceived);
        Assert.True(f.Store.UpdateText(2, "stack on Maple"));
        var edited = Assert.Single(f.Store.Snapshot());
        Assert.Equal("stack on Maple", edited.Text);
        Assert.Equal(live.ReceivedAt, edited.ReceivedAt);
        Assert.Equal(1, f.Arrivals);
    }
}

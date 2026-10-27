using System.Reflection;
using Dalamud.Plugin.Services;
using Discord;
using MaplesEcho.Services;
using Moq;
using Xunit;

namespace MaplesEcho.RelayTests;

public sealed class PlayerMessagesTests
{
    private static IMessage Message(bool bot = false, bool webhook = false, ulong channelId = 42,
        string? nickname = "UwU Mommy", string? globalName = "Global Maple", MessageType type = MessageType.Default)
    {
        var author = new Mock<IGuildUser>();
        author.SetupGet(x => x.Username).Returns(webhook ? "[Scriptly] lassethgrey" : "account_username");
        author.SetupGet(x => x.Nickname).Returns(nickname!);
        author.SetupGet(x => x.GlobalName).Returns(globalName!);
        author.SetupGet(x => x.IsBot).Returns(bot);
        author.SetupGet(x => x.IsWebhook).Returns(webhook);
        var channel = new Mock<IMessageChannel>();
        channel.SetupGet(x => x.Id).Returns(channelId);
        var message = new Mock<IMessage>();
        message.SetupGet(x => x.Id).Returns(123UL);
        message.SetupGet(x => x.Author).Returns(author.Object);
        message.SetupGet(x => x.Channel).Returns(channel.Object);
        message.SetupGet(x => x.Source).Returns(webhook ? MessageSource.Webhook : bot ? MessageSource.Bot : MessageSource.User);
        message.SetupGet(x => x.Type).Returns(type);
        message.SetupGet(x => x.Content).Returns("stack on me");
        message.SetupGet(x => x.Embeds).Returns(Array.Empty<IEmbed>());
        message.SetupGet(x => x.Timestamp).Returns(DateTimeOffset.UtcNow);
        return message.Object;
    }

    private static DiscordRelayService Relay(Configuration config)
        => new(config, new MessageStore(20), Mock.Of<IFramework>(), Mock.Of<IPluginLog>());

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public async Task Mode_FiltersHumansAndBotsButPreservesWebhookTranscriptions(
        bool onlyWebhooks, bool bot, bool webhook, bool expected)
    {
        var relay = Relay(new Configuration { ChannelId = 42, WebhookMessagesOnly = onlyWebhooks });
        var result = await relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([Message(bot, webhook)]));
        Assert.Equal(expected ? 1 : 0, result.Messages.Count);
    }

    [Theory]
    [InlineData("MapleCakes", "Global Maple", "MapleCakes")]
    [InlineData(null, "Global Maple", "Global Maple")]
    [InlineData("", null, "account_username")]
    public async Task TypedMessages_UseServerNicknameThenGlobalNameThenUsername(
        string? nickname, string? globalName, string expected)
    {
        var relay = Relay(new Configuration { ChannelId = 42 });
        var message = Message(nickname: nickname, globalName: globalName);
        var result = await relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([message]));
        Assert.Equal(expected, Assert.Single(result.Messages).Speaker);
        var live = (RelayMessage)typeof(DiscordRelayService)
            .GetMethod("BuildRelay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(relay, [message])!;
        Assert.Equal(expected, live.Speaker);
    }

    [Fact]
    public async Task WebhookPin_DoesNotExcludeHumanMessagesOrAllowOtherWebhooks()
    {
        var relay = Relay(new Configuration { ChannelId = 42, SourceWebhookId = 999 });
        var result = await relay.FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([
                Message(), Message(bot: true, webhook: true), Message(channelId: 99)]));
        Assert.Equal("UwU Mommy", Assert.Single(result.Messages).Speaker);
    }

    [Fact]
    public async Task Webhooks_KeepTranscriptionSpeakerInsteadOfBotNickname()
    {
        var result = await Relay(new Configuration { ChannelId = 42 }).FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([Message(bot: true, webhook: true)]));
        Assert.Equal("lassethgrey", Assert.Single(result.Messages).Speaker);
    }

    [Fact]
    public async Task HumanRepliesAreIncludedButSystemMessagesAreNot()
    {
        var result = await Relay(new Configuration { ChannelId = 42 }).FetchRecentAsync(() =>
            Task.FromResult<IReadOnlyList<IMessage>>([
                Message(type: MessageType.Reply), Message(type: MessageType.ChannelPinnedMessage)]));
        Assert.Single(result.Messages);
    }

    [Fact]
    public void SharingSettings_PreservesCombinedMode()
    {
        var target = new Configuration { WebhookMessagesOnly = true };
        Assert.True(ConfigBlob.Import(ConfigBlob.Export(new Configuration { WebhookMessagesOnly = false }), target));
        Assert.False(target.WebhookMessagesOnly);
    }
}

using System.Text.Json;
using MaplesEcho.Services;
using Xunit;

namespace MaplesEcho.RelayTests;

public sealed class ReleaseNotesTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("invalid", true)]
    [InlineData("0.3.0.0", true)]
    [InlineData("0.4.0.0", false)]
    [InlineData("0.5.0.0", false)]
    public void Welcome_ShowsForFirstInstallOrUpgradeOnly(string? seen, bool expected)
        => Assert.Equal(expected, ReleaseNotes.ShouldShow(seen, new Version(0, 4, 0, 0)));

    [Fact]
    public void Acknowledgement_SurvivesConfigReload()
    {
        var config = new Configuration { LastSeenWelcomeVersion = ReleaseNotes.CurrentVersion.ToString() };
        var reloaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        Assert.False(ReleaseNotes.ShouldShow(reloaded.LastSeenWelcomeVersion, ReleaseNotes.CurrentVersion));
    }

    [Fact]
    public void SharedSettings_DoNotTransferAcknowledgement()
    {
        var source = new Configuration { LastSeenWelcomeVersion = "9.0.0.0" };
        var target = new Configuration { LastSeenWelcomeVersion = "0.3.0.0" };
        Assert.True(ConfigBlob.Import(ConfigBlob.Export(source), target));
        Assert.Equal("0.3.0.0", target.LastSeenWelcomeVersion);
    }
}

using MaplesEcho.Windows;
using Xunit;

namespace MaplesEcho.RelayTests;

public sealed class WelcomeAssetTests
{
    [Fact]
    public void VieraArtwork_IsBundledInThePluginAsAPng()
    {
        using var stream = typeof(WelcomeWindow).Assembly.GetManifestResourceStream(WelcomeWindow.MascotResource);
        Assert.NotNull(stream);
        var header = new byte[26];
        stream.ReadExactly(header);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, header[..8]);
        Assert.Equal(6, header[25]); // RGBA: transparent background, not a solid rectangle.
    }
}

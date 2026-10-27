using System.Text.Json;
using MaplesEcho.Services;
using Xunit;

namespace MaplesEcho.RelayTests;

public sealed class WelcomeAnimationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.8, 1)]
    [InlineData(2.87, 2)]
    [InlineData(2.97, 1)]
    [InlineData(3.04, 0)]
    [InlineData(6.5, 3)]
    [InlineData(6.58, 4)]
    [InlineData(6.7, 5)]
    [InlineData(6.78, 3)]
    [InlineData(6.84, 0)]
    [InlineData(12, 0)]
    [InlineData(14.9, 2)]
    public void Timing_MatchesApprovedPreview(double time, int expected)
        => Assert.Equal(expected, WelcomeAnimation.FrameAt(time, false));

    [Fact]
    public void ReduceMotion_AlwaysShowsNeutral()
    {
        for (var t = 0d; t < 48; t += 0.01)
            Assert.Equal(0, WelcomeAnimation.FrameAt(t, true));
        Assert.Equal(0, WelcomeAnimation.FrameAt(double.NaN, false));
        Assert.Equal(0, WelcomeAnimation.FrameAt(-1, false));
    }

    [Fact]
    public void Frames_StayInsideSixEqualSpriteCells()
    {
        for (var i = 0; i < 6; i++)
        {
            var (min, max) = WelcomeAnimation.UvFor(i);
            Assert.InRange(min.X, 0, 1);
            Assert.InRange(min.Y, 0, 1);
            Assert.InRange(max.X, 0, 1);
            Assert.InRange(max.Y, 0, 1);
            Assert.Equal(1f / 3, max.X - min.X, 6);
            Assert.Equal(0.5f, max.Y - min.Y, 6);
        }
    }

    [Fact]
    public void MotionPreference_PersistsAndIsNotOverwrittenBySharedPacks()
    {
        var config = new Configuration { ReduceWelcomeMotion = true };
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        Assert.True(restored.ReduceWelcomeMotion);
        Assert.True(ConfigBlob.Import(ConfigBlob.Export(new Configuration()), restored));
        Assert.True(restored.ReduceWelcomeMotion);
    }

    [Fact]
    public void AnimatedArtwork_IsBundledWithSixSquareFrames()
    {
        using var stream = typeof(WelcomeAnimation).Assembly.GetManifestResourceStream(WelcomeAnimation.Resource);
        Assert.NotNull(stream);
        var header = new byte[24];
        stream.ReadExactly(header);
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
        Assert.Equal(width / 3, height / 2);
        Assert.Equal(0, width % 3);
        Assert.Equal(0, height % 2);
    }
}

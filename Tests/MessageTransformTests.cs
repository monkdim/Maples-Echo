using System.Collections.Generic;
using System.Numerics;
using MaplesEcho;
using MaplesEcho.Services;
using Xunit;

namespace MaplesEcho.Tests;

public class MessageTransformTests
{
    [Fact]
    public void ExtractSpeaker_StripsConfiguredPrefix()
    {
        Assert.Equal("chronoschoros",
            MessageTransform.ExtractSpeaker("[Scriptly] chronoschoros", "[Scriptly] "));
    }

    [Fact]
    public void ExtractSpeaker_FallsBackToRawWhenPrefixMissing()
    {
        // Unknown-format names must fall back, never vanish. (Plan §5, test item 5)
        Assert.Equal("someone_else",
            MessageTransform.ExtractSpeaker("someone_else", "[Scriptly] "));
    }

    [Fact]
    public void ExtractSpeaker_EmptyAuthorBecomesUnknown()
    {
        Assert.Equal("Unknown", MessageTransform.ExtractSpeaker("", "[Scriptly] "));
    }

    [Theory]
    [InlineData("**Stack** on me", "Stack on me")]
    [InlineData("go to <@123456789> now", "go to  now")]
    [InlineData("nice <:blobwave:987654321>", "nice :blobwave:")]
    [InlineData("~~strike~~ and `code`", "strike and code")]
    public void CleanMarkdown_StripsMarkup(string input, string expected)
    {
        Assert.Equal(expected, MessageTransform.CleanMarkdown(input));
    }

    [Fact]
    public void ApplyGlossary_ReplacesCaseInsensitivelyInOrder()
    {
        var rules = new List<GlossaryRule>
        {
            new() { Find = "purple 65", Replace = "purple marker", Enabled = true },
        };
        Assert.Equal("It's a purple marker.",
            MessageTransform.ApplyGlossary("It's a Purple 65.", rules));
    }

    [Fact]
    public void ApplyGlossary_RespectsDisabledAndCaseSensitiveRules()
    {
        var rules = new List<GlossaryRule>
        {
            new() { Find = "aoe", Replace = "AOE", Enabled = false },
            new() { Find = "TB", Replace = "tank buster", CaseSensitive = true, Enabled = true },
        };
        // disabled rule leaves "aoe" untouched; case-sensitive rule replaces the
        // uppercase "TB" but leaves lowercase "tb" alone
        Assert.Equal("aoe tank buster tb",
            MessageTransform.ApplyGlossary("aoe TB tb", rules));
    }

    [Fact]
    public void TransformBody_CleansThenAppliesGlossary()
    {
        var rules = new List<GlossaryRule>
        {
            new() { Find = "stack", Replace = "STACK", Enabled = true },
        };
        Assert.Equal("STACK now", MessageTransform.TransformBody("**stack** now", rules));
    }
}

public class ColorUtilTests
{
    [Fact]
    public void HashColor_IsDeterministic()
    {
        Assert.Equal(ColorUtil.HashColor("lassethgrey"), ColorUtil.HashColor("lassethgrey"));
    }

    [Fact]
    public void HashColor_DiffersBetweenSpeakers()
    {
        Assert.NotEqual(ColorUtil.HashColor("alice"), ColorUtil.HashColor("bob"));
    }

    [Fact]
    public void ContrastRatio_DefaultTextOnDarkBlue_ClearsAAA()
    {
        // The shipped default pairing must clear 7:1. (Plan §7)
        var background = new Vector4(0.06f, 0.11f, 0.18f, 1f);
        var text = new Vector4(0.941f, 0.949f, 0.961f, 1f);
        Assert.True(ColorUtil.ContrastRatio(text, background) >= 7f);
    }

    [Fact]
    public void ContrastRatio_WhiteOnBlack_IsMaximal()
    {
        var ratio = ColorUtil.ContrastRatio(new Vector4(1, 1, 1, 1), new Vector4(0, 0, 0, 1));
        Assert.True(ratio > 20f);
    }
}

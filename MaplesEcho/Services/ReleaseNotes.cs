using System;

namespace MaplesEcho.Services;

public static class ReleaseNotes
{
    // Assembly version is already maintained by the release process.
    public static Version CurrentVersion => typeof(ReleaseNotes).Assembly.GetName().Version ?? new Version(0, 0);
    public static string DisplayVersion => CurrentVersion.ToString(3);

    public static bool ShouldShow(string? lastSeen, Version current)
        => !Version.TryParse(lastSeen, out var seen) || seen < current;

    public sealed record Change(string Title, string Description);

    public static readonly Change[] Changes =
    [
        new("A little hello from Maple",
            "Maple now gives an occasional blink and a tiny ear twitch in the welcome window."),
        new("Comfort comes first",
            "Turn on Reduce motion above to keep Maple still. Your choice is remembered between sessions."),
        new("The same calm corner",
            "Your cozy colors and rounded cards stay still, so the update notes remain easy to read."),
    ];
}

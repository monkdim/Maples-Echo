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
        new("Everyone gets a voice",
            "Choose Transcriptions + typed player messages in Connection settings to include typed replies from the same Discord channel."),
        new("Names you know",
            "Typed replies use server display names, falling back to Discord display names or usernames."),
        new("A little less noise",
            "Ordinary bot replies stay out. Scriptly transcriptions and your optional webhook filter still work as before."),
    ];
}

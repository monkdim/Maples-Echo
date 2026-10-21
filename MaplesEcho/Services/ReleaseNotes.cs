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
        new("A peek into the past",
            "Preview recent Discord messages in settings, with their original date and time."),
        new("Only fresh callouts",
            "Historical messages stay separate from your live relay and leave its connection status unchanged."),
        new("Quiet little previews",
            "Checking history won't trigger arrival sounds or chat notifications, or reset your last live message time."),
    ];
}

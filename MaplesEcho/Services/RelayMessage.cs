using System;

namespace MaplesEcho.Services;

/// <summary>
/// One relayed line ready for display. This is the unit the pipeline produces
/// (receive → filter → extract speaker → transform → store → render) and the
/// window consumes. Kept immutable so the draw thread never sees a half-built
/// message. (Plan §8, §8b)
/// </summary>
public sealed class RelayMessage
{
    /// <summary>Speaker name, already stripped of the configurable prefix.</summary>
    public required string Speaker { get; init; }

    /// <summary>Display text, already markdown-stripped and glossary-transformed.</summary>
    public required string Text { get; init; }

    /// <summary>Local time the message was received by the plugin.</summary>
    public required DateTime ReceivedAt { get; init; }

    /// <summary>Discord message id, for de-duplication / debugging.</summary>
    public ulong SourceMessageId { get; init; }
}

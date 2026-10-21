using System;
using System.Collections.Generic;

namespace MaplesEcho.Services;

// A separate type prevents diagnostic history from being mistaken for a live
// RelayMessage. CreatedAt is the original Discord timestamp, including offset.
public sealed record HistoricalMessage(string Speaker, string Text, DateTimeOffset CreatedAt, ulong SourceMessageId);

public sealed record HistoricalPreview(string Summary, IReadOnlyList<HistoricalMessage> Messages);

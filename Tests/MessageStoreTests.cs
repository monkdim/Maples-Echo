using System;
using MaplesEcho.Services;
using Xunit;

namespace MaplesEcho.Tests;

public class MessageStoreTests
{
    private static RelayMessage Msg(ulong id, string text, string speaker = "maple")
        => new()
        {
            Speaker = speaker,
            Text = text,
            ReceivedAt = new DateTime(2026, 8, 12, 20, 0, 0).AddSeconds(id),
            SourceMessageId = id,
        };

    [Fact]
    public void Add_DropsOldestPastCapacity()
    {
        var store = new MessageStore(2);
        store.Add(Msg(1, "one"));
        store.Add(Msg(2, "two"));
        store.Add(Msg(3, "three"));

        var snapshot = store.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("two", snapshot[0].Text);
        Assert.Equal("three", snapshot[1].Text);
    }

    [Fact]
    public void SetCapacity_TrimsImmediately()
    {
        var store = new MessageStore(5);
        for (ulong i = 1; i <= 5; i++)
            store.Add(Msg(i, $"m{i}"));

        store.SetCapacity(2);

        var snapshot = store.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("m4", snapshot[0].Text);
        Assert.Equal("m5", snapshot[1].Text);
    }

    [Fact]
    public void UpdateText_ReplacesTextInPlace()
    {
        // The transcription-edit case: partial line first, corrected line later.
        var store = new MessageStore(10);
        store.Add(Msg(1, "earlier line"));
        store.Add(Msg(2, "stack on the tea"));
        store.Add(Msg(3, "later line"));

        Assert.True(store.UpdateText(2, "stack on the T"));

        var snapshot = store.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal("stack on the T", snapshot[1].Text);
        // Order unchanged — the edit must not move the line.
        Assert.Equal("earlier line", snapshot[0].Text);
        Assert.Equal("later line", snapshot[2].Text);
    }

    [Fact]
    public void UpdateText_PreservesSpeakerAndTimestamp()
    {
        // Merge grouping keys off speaker + ReceivedAt; an edit must not
        // change either, or the line would regroup or jump.
        var store = new MessageStore(10);
        var original = Msg(7, "partial", speaker: "lasseth");
        store.Add(original);

        Assert.True(store.UpdateText(7, "final"));

        var updated = store.Snapshot()[0];
        Assert.Equal("lasseth", updated.Speaker);
        Assert.Equal(original.ReceivedAt, updated.ReceivedAt);
        Assert.Equal(7UL, updated.SourceMessageId);
    }

    [Fact]
    public void UpdateText_UnknownIdIsIgnored()
    {
        // Edits to messages that already left the buffer (or predate the
        // session) must not append out of order — just report false.
        var store = new MessageStore(10);
        store.Add(Msg(1, "one"));

        Assert.False(store.UpdateText(99, "ghost"));
        Assert.Single(store.Snapshot());
    }
}

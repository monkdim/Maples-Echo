using System.Collections.Generic;

namespace MaplesEcho.Services;

/// <summary>
/// Thread-safe ring buffer of relayed messages. Written from the Discord thread
/// (marshalled to the framework thread), read from the draw thread. All access
/// is locked; the buffer is never mutated mid-draw. (Plan §4, §8)
/// </summary>
public sealed class MessageStore
{
    private readonly object gate = new();
    private readonly LinkedList<RelayMessage> messages = new();
    private int capacity;

    public MessageStore(int capacity)
    {
        this.capacity = capacity < 1 ? 1 : capacity;
    }

    /// <summary>Append a message, dropping the oldest past capacity.</summary>
    public void Add(RelayMessage message)
    {
        lock (gate)
        {
            messages.AddLast(message);
            while (messages.Count > capacity)
                messages.RemoveFirst();
        }
    }

    /// <summary>Adjust the cap live (MaxMessages slider). Trims immediately.</summary>
    public void SetCapacity(int newCapacity)
    {
        lock (gate)
        {
            capacity = newCapacity < 1 ? 1 : newCapacity;
            while (messages.Count > capacity)
                messages.RemoveFirst();
        }
    }

    public void Clear()
    {
        lock (gate)
            messages.Clear();
    }

    /// <summary>
    /// Replace the text of a message by its Discord id — transcription bots post
    /// a partial line and edit in the final text, and the correction must reach
    /// the screen. Speaker and ReceivedAt are preserved so ordering and merge
    /// grouping stay stable. Searches from the newest end (edits target recent
    /// messages). Returns false if the id is no longer in the buffer.
    /// </summary>
    public bool UpdateText(ulong sourceMessageId, string newText)
    {
        lock (gate)
        {
            for (var node = messages.Last; node != null; node = node.Previous)
            {
                var m = node.Value;
                if (m.SourceMessageId != sourceMessageId)
                    continue;

                node.Value = new RelayMessage
                {
                    Speaker = m.Speaker,
                    Text = newText,
                    ReceivedAt = m.ReceivedAt,
                    SourceMessageId = m.SourceMessageId,
                };
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Snapshot for rendering. Copies under lock so the draw loop iterates a
    /// stable list even as new messages arrive. Cheap at MaxMessages ≈ 200.
    /// </summary>
    public IReadOnlyList<RelayMessage> Snapshot()
    {
        lock (gate)
            return new List<RelayMessage>(messages);
    }

    /// <summary>Most recent message, or null if empty (for the "last received" age).</summary>
    public RelayMessage? Newest()
    {
        lock (gate)
            return messages.Last?.Value;
    }
}

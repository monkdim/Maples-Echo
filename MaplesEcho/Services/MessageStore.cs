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

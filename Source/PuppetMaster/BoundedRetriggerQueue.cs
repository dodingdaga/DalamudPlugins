using System.Collections.Generic;

namespace PuppetMaster;

internal sealed class BoundedRetriggerQueue<T>(int capacity)
{
    private readonly Queue<T> items = new();

    public int Count => items.Count;

    public int Enqueue(ReactionExecutionPolicy policy, T item)
    {
        if (policy == ReactionExecutionPolicy.IgnoreWhileRunning)
            return 0;

        var dropped = 0;
        if (policy == ReactionExecutionPolicy.QueueLatestTrigger)
        {
            dropped = items.Count;
            items.Clear();
        }
        else if (items.Count >= capacity)
        {
            items.Dequeue();
            dropped = 1;
        }

        items.Enqueue(item);
        return dropped;
    }

    public bool TryDequeue(out T item)
    {
        if (items.Count == 0)
        {
            item = default!;
            return false;
        }

        item = items.Dequeue();
        return true;
    }

    public bool TryPeek(out T item)
    {
        if (items.Count == 0)
        {
            item = default!;
            return false;
        }

        item = items.Peek();
        return true;
    }

    public void Clear()
    {
        items.Clear();
    }
}

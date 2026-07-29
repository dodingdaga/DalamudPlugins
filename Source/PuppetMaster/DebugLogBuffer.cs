using System.Collections.Concurrent;

namespace PuppetMaster;

internal readonly record struct DebugLogEntry(int ChatTypeId, string Text);

internal static class DebugLogBuffer
{
    private const int MaximumEntries = 500;
    private static readonly ConcurrentQueue<DebugLogEntry> Entries = new();

    public static void Add(int chatTypeId, string text)
    {
        Entries.Enqueue(new DebugLogEntry(chatTypeId, text));

        while (Entries.Count > MaximumEntries)
            Entries.TryDequeue(out _);
    }

    public static DebugLogEntry[] Snapshot()
    {
        return Entries.ToArray();
    }

    public static void Clear()
    {
        while (Entries.TryDequeue(out _))
        {
        }
    }
}

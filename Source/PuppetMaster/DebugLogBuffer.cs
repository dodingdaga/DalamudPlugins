using System.Collections.Concurrent;

namespace PuppetMaster;

internal static class DebugLogBuffer
{
    private const int MaximumEntries = 500;
    private static readonly ConcurrentQueue<string> Entries = new();

    public static void Add(string entry)
    {
        Entries.Enqueue(entry);

        while (Entries.Count > MaximumEntries)
            Entries.TryDequeue(out _);
    }

    public static string[] Snapshot()
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

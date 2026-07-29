using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PuppetMaster;

internal readonly record struct DebugLogEntry(int ChatTypeId, string Text, string TriggerText);

internal static class DebugLogBuffer
{
    private const int MaximumEntries = 500;
    private static readonly ConcurrentQueue<DebugLogEntry> Entries = new();

    public static void Add(int chatTypeId, string text, string triggerText)
    {
        Entries.Enqueue(new DebugLogEntry(chatTypeId, text, triggerText));

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

    public static string SaveSnapshot(string directory, DebugLogEntry[] entries)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"PuppetMaster-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log.txt");
        var lines = new List<string>(entries.Length + 3)
        {
            "# Puppet Master message log",
            $"# Exported: {DateTimeOffset.Now:O}",
            $"# Entries: {entries.Length}",
        };
        lines.AddRange(entries.Select(static entry => entry.Text));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return path;
    }
}

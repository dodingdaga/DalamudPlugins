using System;
using System.Collections.Generic;
using System.Linq;

namespace PuppetMaster;

internal enum VisualizerRunStatus { Running, Completed, Cancelled, Disabled }

internal sealed record VisualizerRunSnapshot(long Id, long ReactionId, string ReactionName, string Command,
    int Lane, VisualizerRunStatus Status, DateTime StartedAt, DateTime? FinishedAt);
internal sealed record VisualizerQueueSnapshot(long Id, long ReactionId, string ReactionName, string Command, DateTime QueuedAt);
internal sealed record ReactionVisualizerSnapshot(VisualizerRunSnapshot[] Active, VisualizerQueueSnapshot[] Queued,
    VisualizerRunSnapshot[] Recent);

/// <summary>A one-way runtime projection. It owns no Reaction references and exposes immutable snapshots only.</summary>
internal static class ReactionVisualizerState
{
    private const int LaneCount = 4;
    private const int RecentCapacity = 24;
    private static readonly object Sync = new();
    private static readonly List<VisualizerRunSnapshot> Active = [];
    private static readonly List<VisualizerQueueSnapshot> Queued = [];
    private static readonly List<VisualizerRunSnapshot> Recent = [];
    private static long nextId;

    public static ReactionVisualizerSnapshot Snapshot()
    {
        lock (Sync) return new([.. Active], [.. Queued], [.. Recent]);
    }

    public static long Started(long reactionId, string reactionName, string command)
    {
        lock (Sync)
        {
            var id = ++nextId;
            var occupied = Active.Where(item => item.Lane >= 0).Select(item => item.Lane).ToHashSet();
            var lane = Enumerable.Range(0, LaneCount).FirstOrDefault(index => !occupied.Contains(index), -1);
            Active.Add(new(id, reactionId, DisplayName(reactionName), command, lane,
                VisualizerRunStatus.Running, DateTime.Now, null));
            return id;
        }
    }

    public static void Finished(long runId, bool cancelled, bool reactionEnabled)
    {
        lock (Sync)
        {
            var index = Active.FindIndex(item => item.Id == runId);
            if (index < 0) return;
            var completed = Active[index] with
            {
                Status = ResolveFinishedStatus(cancelled, reactionEnabled),
                FinishedAt = DateTime.Now,
            };
            Active.RemoveAt(index);
            Recent.Insert(0, completed);
            if (Recent.Count > RecentCapacity) Recent.RemoveRange(RecentCapacity, Recent.Count - RecentCapacity);
            RebalanceLanes();
        }
    }

    public static void QueuedRun(long reactionId, string reactionName, string command, ReactionExecutionPolicy policy)
    {
        lock (Sync)
        {
            if (policy is ReactionExecutionPolicy.QueueLatestTrigger or ReactionExecutionPolicy.RestartImmediately)
                Queued.RemoveAll(item => item.ReactionId == reactionId);
            Queued.Add(new(++nextId, reactionId, DisplayName(reactionName), command, DateTime.Now));
            while (Queued.Count(item => item.ReactionId == reactionId) > 16)
            {
                var oldest = Queued.FindIndex(item => item.ReactionId == reactionId);
                if (oldest < 0) break;
                Queued.RemoveAt(oldest);
            }
        }
    }

    public static void DequeuedRun(long reactionId)
    {
        lock (Sync)
        {
            var index = Queued.FindIndex(item => item.ReactionId == reactionId);
            if (index >= 0) Queued.RemoveAt(index);
        }
    }

    public static void ClearQueued(long reactionId) { lock (Sync) Queued.RemoveAll(item => item.ReactionId == reactionId); }

    public static void Reset()
    {
        lock (Sync) { Active.Clear(); Queued.Clear(); Recent.Clear(); }
    }

    public static VisualizerRunStatus ResolveFinishedStatus(bool cancelled, bool reactionEnabled)
    {
        if (!cancelled)
            return VisualizerRunStatus.Completed;
        return reactionEnabled ? VisualizerRunStatus.Cancelled : VisualizerRunStatus.Disabled;
    }

    private static void RebalanceLanes()
    {
        var occupied = Active.Where(item => item.Lane >= 0).Select(item => item.Lane).ToHashSet();
        for (var lane = 0; lane < LaneCount; lane++)
        {
            if (occupied.Contains(lane)) continue;
            var overflow = Active.FindIndex(item => item.Lane < 0);
            if (overflow < 0) return;
            Active[overflow] = Active[overflow] with { Lane = lane };
        }
    }

    private static string DisplayName(string name) => string.IsNullOrWhiteSpace(name) ? "Unnamed reaction" : name;
}

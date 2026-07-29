using System;
using System.Threading;
using System.Threading.Tasks;

namespace PuppetMaster;

internal sealed class BoundedRetriggerScheduler<T>(
    int capacity,
    Func<T, CancellationToken, Task<IDisposable>> acquire,
    Func<T, IDisposable, Task> execute,
    Action<int>? reportDropped = null,
    Action<Exception, int>? reportFailure = null)
{
    private readonly object sync = new();
    private readonly BoundedRetriggerQueue<T> queue = new(capacity);
    private bool isDraining;
    private long generation;
    private CancellationTokenSource? drainerCancellation;

    public int PendingCount
    {
        get
        {
            lock (sync)
                return queue.Count;
        }
    }

    public Task? Enqueue(ReactionExecutionPolicy policy, T item, CancellationToken lifetimeToken)
    {
        lock (sync)
        {
            var dropped = queue.Enqueue(policy, item);
            if (policy == ReactionExecutionPolicy.QueueEveryTrigger && dropped > 0)
                reportDropped?.Invoke(dropped);
            if (policy == ReactionExecutionPolicy.IgnoreWhileRunning || isDraining)
                return null;

            isDraining = true;
            generation++;
            drainerCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
            return DrainAsync(generation, drainerCancellation.Token);
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (sync)
        {
            queue.Clear();
            isDraining = false;
            generation++;
            cancellation = drainerCancellation;
            drainerCancellation = null;
        }

        if (cancellation == null)
            return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        cancellation.Dispose();
    }

    private async Task DrainAsync(long drainerGeneration, CancellationToken drainerToken)
    {
        try
        {
            while (true)
            {
                T pending;
                lock (sync)
                {
                    if (generation != drainerGeneration || !queue.TryPeek(out pending))
                        return;
                }

                var lease = await acquire(pending, drainerToken);
                lock (sync)
                {
                    if (generation != drainerGeneration || !queue.TryDequeue(out pending))
                    {
                        lease.Dispose();
                        return;
                    }
                }

                await execute(pending, lease);
            }
        }
        catch (OperationCanceledException) when (drainerToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            int discarded;
            lock (sync)
                discarded = queue.Count;
            reportFailure?.Invoke(exception, discarded);
        }
        finally
        {
            lock (sync)
            {
                if (generation == drainerGeneration)
                {
                    queue.Clear();
                    isDraining = false;
                    drainerCancellation?.Dispose();
                    drainerCancellation = null;
                }
            }
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PuppetMaster;

internal enum ReactionRejectionReason
{
    None,
    Busy,
    Cooldown,
}

internal sealed class ReactionExecutionGate
{
    private ConditionalWeakTable<Reaction, State> states = new();

    public bool TryEnter(
        Reaction reaction,
        TimeSpan cooldown,
        long nowTimestamp,
        out IDisposable? lease,
        out ReactionRejectionReason rejectionReason)
    {
        var state = states.GetValue(reaction, static _ => new State());
        lock (state)
        {
            if (state.Running || state.WaitingEntrants > 0)
            {
                lease = null;
                rejectionReason = ReactionRejectionReason.Busy;
                return false;
            }

            if (nowTimestamp < state.NextAllowedTimestamp)
            {
                lease = null;
                rejectionReason = ReactionRejectionReason.Cooldown;
                return false;
            }

            StartRun(state, cooldown, nowTimestamp);
            lease = new Lease(state);
            rejectionReason = ReactionRejectionReason.None;
            return true;
        }
    }

    public void Reset()
    {
        states = new ConditionalWeakTable<Reaction, State>();
    }

    public async Task<IDisposable> EnterWhenAvailableAsync(
        Reaction reaction,
        TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        var state = states.GetValue(reaction, static _ => new State());
        lock (state)
            state.WaitingEntrants++;

        try
        {
            while (true)
            {
                Task? idleTask = null;
                TimeSpan cooldownDelay = TimeSpan.Zero;
                lock (state)
                {
                    var nowTimestamp = Stopwatch.GetTimestamp();
                    if (!state.Running && nowTimestamp >= state.NextAllowedTimestamp)
                    {
                        state.WaitingEntrants--;
                        StartRun(state, cooldown, nowTimestamp);
                        return new Lease(state);
                    }

                    if (state.Running)
                        idleTask = state.Idle.Task;
                    else
                        cooldownDelay = TimeSpan.FromSeconds(
                            (state.NextAllowedTimestamp - nowTimestamp) / (double)Stopwatch.Frequency);
                }

                if (idleTask != null)
                    await idleTask.WaitAsync(cancellationToken);
                else
                    await Task.Delay(cooldownDelay, cancellationToken);
            }
        }
        catch
        {
            lock (state)
                state.WaitingEntrants--;
            throw;
        }
    }

    private static void StartRun(State state, TimeSpan cooldown, long nowTimestamp)
    {
        state.Running = true;
        state.Idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cooldownSeconds = Math.Max(0, cooldown.TotalSeconds);
        var cooldownTicks = cooldownSeconds >= long.MaxValue / (double)Stopwatch.Frequency
            ? long.MaxValue
            : (long)(cooldownSeconds * Stopwatch.Frequency);
        state.NextAllowedTimestamp = cooldownTicks > long.MaxValue - nowTimestamp
            ? long.MaxValue
            : nowTimestamp + cooldownTicks;
    }

    private sealed class State
    {
        public bool Running;
        public int WaitingEntrants;
        public long NextAllowedTimestamp;
        public TaskCompletionSource Idle = CreateIdleSignal();

        private static TaskCompletionSource CreateIdleSignal()
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            signal.SetResult();
            return signal;
        }
    }

    private sealed class Lease(State state) : IDisposable
    {
        private State? state = state;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref state, null);
            if (current == null)
                return;
            TaskCompletionSource idle;
            lock (current)
            {
                current.Running = false;
                idle = current.Idle;
            }
            idle.TrySetResult();
        }
    }
}

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using ECommons.Automation;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Diagnostics;
using Dalamud.Game.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

namespace PuppetMaster
{
    public partial class ChatHandler
    {
        private static readonly ReactionExecutionGate ExecutionGate = new();
        private static readonly ConcurrentDictionary<long, Task> ActiveTasks = new();
        private static readonly ConcurrentDictionary<Reaction, CancellationTokenSource> ActiveReactionCancellations =
            new(ReferenceEqualityComparer.Instance);
        private static readonly ConcurrentDictionary<IActiveNotification, byte> ActiveNotifications = new();
        private static ConditionalWeakTable<Reaction, SuppressionNotificationState> suppressionNotifications = new();
        private static ConditionalWeakTable<Reaction, ErrorNotificationState> errorNotifications = new();
        private static ConditionalWeakTable<Reaction, ReactionControlState> reactionControls = new();
        private static ConditionalWeakTable<Reaction, BoundedRetriggerScheduler<PendingRetrigger>> retriggerQueues = new();
        private static CancellationTokenSource pluginLifetime = new();
        private static long nextTaskId;
        private static long droppedMessageCount;
        private static long droppedRetriggerCount;
        private static int shuttingDown;
        private static Channel<ChatEnvelope> dispatcher = CreateDispatcher();

        private sealed record ReactionSnapshot(
            Reaction Source,
            ReactionControlState Control,
            long Generation,
            string Name,
            bool MotionOnly,
            bool AllowAllCommands,
            ReactionExecutionPolicy ExecutionPolicy,
            bool ShowNotifications,
            bool ShowSuppressionNotifications,
            int CooldownSeconds,
            HashSet<int> EnabledChannels,
            HashSet<string> CommandWhitelist,
            HashSet<string> CommandBlacklist,
            Regex Pattern,
            string Replacement);

        private sealed record ChatEnvelope(XivChatType Type, string Message, List<ReactionSnapshot> Reactions);
        private sealed record PendingRetrigger(
            ReactionSnapshot Reaction,
            string Command,
            CancellationToken PluginToken);

        private sealed class SuppressionNotificationState
        {
            public int PendingCount;
            public long NextNotificationTimestamp;
        }

        private sealed class ReactionControlState
        {
            public long Generation;
        }

        private sealed class ErrorNotificationState
        {
            public long NextNotificationTimestamp;
        }

        public ChatHandler()
        {
        }

        public static void Initialize()
        {
            Volatile.Write(ref shuttingDown, 0);
            var previous = Interlocked.Exchange(ref pluginLifetime, new CancellationTokenSource());
            previous.Cancel();
            dispatcher.Writer.TryComplete();
            dispatcher = CreateDispatcher();
            Interlocked.Exchange(ref droppedMessageCount, 0);
            Interlocked.Exchange(ref droppedRetriggerCount, 0);
            ExecutionGate.Reset();
            suppressionNotifications = new ConditionalWeakTable<Reaction, SuppressionNotificationState>();
            errorNotifications = new ConditionalWeakTable<Reaction, ErrorNotificationState>();
            reactionControls = new ConditionalWeakTable<Reaction, ReactionControlState>();
            retriggerQueues = new ConditionalWeakTable<Reaction, BoundedRetriggerScheduler<PendingRetrigger>>();
            var currentDispatcher = dispatcher;
            var token = pluginLifetime.Token;
            for (var index = 0; index < 4; index++)
                Track(Task.Run(() => DispatchLoopAsync(currentDispatcher.Reader, token), token));
        }

        public static void Shutdown()
        {
            Volatile.Write(ref shuttingDown, 1);
            pluginLifetime.Cancel();
            dispatcher.Writer.TryComplete();
            foreach (var cancellation in ActiveReactionCancellations.Values)
            {
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            foreach (var notification in ActiveNotifications.Keys)
            {
                if (notification.DismissReason == null)
                    notification.DismissNow();
            }
            ActiveNotifications.Clear();
            ExecutionGate.Reset();
            suppressionNotifications = new ConditionalWeakTable<Reaction, SuppressionNotificationState>();
            errorNotifications = new ConditionalWeakTable<Reaction, ErrorNotificationState>();
            reactionControls = new ConditionalWeakTable<Reaction, ReactionControlState>();
            retriggerQueues = new ConditionalWeakTable<Reaction, BoundedRetriggerScheduler<PendingRetrigger>>();
        }

        public static void CancelReaction(Reaction reaction)
        {
            InvalidateReaction(reaction, true);
        }

        public static void InvalidateReaction(Reaction reaction, bool cancelActive)
        {
            var control = reactionControls.GetValue(reaction, static _ => new ReactionControlState());
            Interlocked.Increment(ref control.Generation);
            if (cancelActive && ActiveReactionCancellations.TryGetValue(reaction, out var cancellation))
            {
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            CancelQueuedRetriggers(reaction);
        }

        private static void CancelQueuedRetriggers(Reaction reaction)
        {
            if (!retriggerQueues.TryGetValue(reaction, out var state))
                return;
            state.Cancel();
        }

        public static long DroppedMessageCount => Interlocked.Read(ref droppedMessageCount);
        public static long DroppedRetriggerCount => Interlocked.Read(ref droppedRetriggerCount);

        public static void ResetDroppedMessageCount()
        {
            Interlocked.Exchange(ref droppedMessageCount, 0);
            Interlocked.Exchange(ref droppedRetriggerCount, 0);
        }

        private static Channel<ChatEnvelope> CreateDispatcher()
        {
            return Channel.CreateBounded<ChatEnvelope>(new BoundedChannelOptions(128)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            }, _ => Interlocked.Increment(ref droppedMessageCount));
        }

        private static async Task DispatchLoopAsync(ChannelReader<ChatEnvelope> reader, CancellationToken token)
        {
            try
            {
                await foreach (var envelope in reader.ReadAllAsync(token))
                {
                    foreach (var reaction in envelope.Reactions)
                    {
                        var task = DoCommandAsync(reaction, envelope.Type, envelope.Message, token);
                        if (!task.IsCompletedSuccessfully)
                            Track(task);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        private static ReactionSnapshot? CreateSnapshot(
            Reaction reaction,
            bool showNotifications,
            bool showSuppressionNotifications)
        {
            if (!reaction.Enabled)
                return null;
            var pattern = reaction.UseRegex ? reaction.CustomRx : reaction.Rx;
            if (pattern == null)
                return null;
            var control = reactionControls.GetValue(reaction, static _ => new ReactionControlState());

            return new ReactionSnapshot(
                reaction,
                control,
                Volatile.Read(ref control.Generation),
                reaction.Name,
                reaction.MotionOnly,
                reaction.AllowAllCommands,
                reaction.ExecutionPolicy,
                showNotifications,
                showSuppressionNotifications,
                Math.Max(0, reaction.CooldownSeconds),
                new HashSet<int>(reaction.EnabledChannels),
                new HashSet<string>(reaction.CommandWhitelist, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(reaction.CommandBlacklist, StringComparer.OrdinalIgnoreCase),
                pattern,
                reaction.UseRegex ? reaction.ReplaceMatch : Service.GetDefaultReplaceMatch());
        }

        private static void Track(Task task)
        {
            var id = Interlocked.Increment(ref nextTaskId);
            ActiveTasks[id] = task;
            _ = task.ContinueWith(
                completed =>
                {
                    ActiveTasks.TryRemove(id, out _);
                    if (completed.Exception != null)
                        Service.PluginLog.Error(completed.Exception, "PuppetMaster background task failed.");
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static async Task RunMacroAsync(
            string[] lines,
            ReactionSnapshot reaction,
            CancellationTokenSource cancellation,
            CancellationToken pluginToken)
        {
            IActiveNotification? notification = null;

            if (reaction.ShowNotifications && !cancellation.IsCancellationRequested)
            {
                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    if (cancellation.IsCancellationRequested)
                        return;
                    notification = Service.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Puppet Master",
                        Content = $"Starting reaction: {reaction.Name}",
                        Type = NotificationType.Info,
                        Progress = 0,
                        InitialDuration = TimeSpan.MaxValue,
                        UserDismissable = false,
                    });
                    ActiveNotifications[notification] = 0;

                    notification.DrawActions += _ =>
                    {
                        if (ImGui.Button($"Cancel##PuppetMasterReaction{notification.Id}"))
                        {
                            try { cancellation.Cancel(); }
                            catch (ObjectDisposedException) { }
                        }
                    };
                });
            }

            try
            {
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var textCommand = Service.FormatCommand(lines[lineIndex]);
                    if (string.IsNullOrEmpty(textCommand.Main))
                        continue;

                    if (notification != null)
                    {
                        await UpdateReactionNotificationAsync(
                            notification,
                            $"{reaction.Name}\nStep {lineIndex + 1} of {lines.Length}: {textCommand}",
                            (float)lineIndex / lines.Length,
                            cancellation.Token);
                    }

                    // Process emote
                    var isEmote = Service.Emotes.Contains(textCommand.Main);
                    if (isEmote)
                    {
                        if (reaction.MotionOnly)
                            textCommand.Args = "motion";
                    }

                    if (IsCommandAllowed(reaction, textCommand.Main, out var permissionReason))
                    {
                        if (textCommand.Main == "/wait" && float.TryParse(textCommand.Args, out var seconds))
                            await Task.Delay((int)(Math.Clamp(seconds, 0.0, 60.0) * 1000.0), cancellation.Token);
                        else
                        {
                            // Lifted from AmberPlume's pull request. (to review)
                            try
                            {
                                // Critical fix: execute Chat.SendMessage on main thread
                                await Service.Framework.RunOnFrameworkThread(() =>
                                {
                                    if (cancellation.IsCancellationRequested)
                                        return;
                                    try
                                    {
                                        Chat.SendMessage($"{textCommand}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Service.ChatGui.PrintError($"[PuppetMaster] Failed to send command {textCommand}: {ex.Message}");
                                    }
                                });
                                cancellation.Token.ThrowIfCancellationRequested();
                            }
                            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Service.ChatGui.PrintError($"[PuppetMaster] Framework thread execution failed: {ex.Message}");
                            }
                        }
                    }
#if DEBUG
                    else
                    {
                        Service.ChatGui.Print($"{textCommand.Main} blocked: {permissionReason}");
                    }
#endif
                    if (notification != null)
                    {
                        await UpdateReactionNotificationAsync(
                            notification,
                            $"{reaction.Name}\nCompleted step {lineIndex + 1} of {lines.Length}",
                            (float)(lineIndex + 1) / lines.Length,
                            cancellation.Token);
                    }
                }

                if (notification != null && !pluginToken.IsCancellationRequested)
                    await FinishReactionNotificationAsync(notification, reaction.Name, false, pluginToken: pluginToken);
            }
            catch (OperationCanceledException)
            {
                if (notification != null && !pluginToken.IsCancellationRequested)
                    await FinishReactionNotificationAsync(notification, reaction.Name, true, pluginToken: pluginToken);
            }
            catch (Exception ex)
            {
                if (notification != null && !pluginToken.IsCancellationRequested)
                    await FinishReactionNotificationAsync(notification, reaction.Name, false, ex.Message, pluginToken);
                else
                    Service.ChatGui.PrintError($"[PuppetMaster] Reaction {reaction.Name} failed: {ex.Message}");
            }
        }

        private static bool IsCommandAllowed(ReactionSnapshot reaction, string command, out string reason)
        {
            if (reaction.CommandBlacklist.Contains(command))
            {
                reason = "command is blacklisted";
                return false;
            }
            if (Service.Emotes.Contains(command))
            {
                reason = "emote allowed by default";
                return true;
            }
            if (reaction.AllowAllCommands)
            {
                reason = "Allow all text commands is enabled";
                return true;
            }
            if (reaction.CommandWhitelist.Contains(command))
            {
                reason = "command is whitelisted";
                return true;
            }
            reason = "command is not whitelisted";
            return false;
        }

        private static async Task NotifySuppressionAsync(
            ReactionSnapshot reaction,
            ReactionRejectionReason reason,
            CancellationToken pluginToken)
        {
            if (!reaction.ShowSuppressionNotifications || pluginToken.IsCancellationRequested)
                return;

            var state = suppressionNotifications.GetValue(reaction.Source, static _ => new SuppressionNotificationState());
            var nowTimestamp = Stopwatch.GetTimestamp();
            int suppressedCount;
            lock (state)
            {
                state.PendingCount++;
                if (nowTimestamp < state.NextNotificationTimestamp)
                    return;
                suppressedCount = state.PendingCount;
                state.PendingCount = 0;
                state.NextNotificationTimestamp = nowTimestamp + 5 * Stopwatch.Frequency;
            }

            var reasonText = reason == ReactionRejectionReason.Busy
                ? "busy running or processing queued triggers"
                : "cooldown active";
            var countText = suppressedCount > 1 ? $"\n{suppressedCount} repeated triggers suppressed." : string.Empty;
            try
            {
                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    if (pluginToken.IsCancellationRequested)
                        return;
                    Service.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Puppet Master",
                        Content = $"Reaction suppressed: {reaction.Name}\n{reasonText}.{countText}",
                        Type = NotificationType.Warning,
                        InitialDuration = TimeSpan.FromSeconds(4),
                    });
                });
            }
            catch (Exception) when (pluginToken.IsCancellationRequested)
            {
            }
        }

        private static async Task NotifyReactionErrorAsync(
            ReactionSnapshot reaction,
            string error,
            CancellationToken pluginToken)
        {
            if (!reaction.ShowNotifications || pluginToken.IsCancellationRequested)
                return;
            var state = errorNotifications.GetValue(reaction.Source, static _ => new ErrorNotificationState());
            var nowTimestamp = Stopwatch.GetTimestamp();
            lock (state)
            {
                if (nowTimestamp < state.NextNotificationTimestamp)
                    return;
                state.NextNotificationTimestamp = nowTimestamp + 5 * Stopwatch.Frequency;
            }

            await Service.Framework.RunOnFrameworkThread(() =>
            {
                if (pluginToken.IsCancellationRequested)
                    return;
                Service.NotificationManager.AddNotification(new Notification
                {
                    Title = "Puppet Master",
                    Content = $"Reaction failed: {reaction.Name}\n{error}",
                    Type = NotificationType.Error,
                    InitialDuration = TimeSpan.FromSeconds(5),
                });
            });
        }

        private static async Task NotifySchedulerFailureAsync(
            Reaction reaction,
            string error,
            int discarded,
            CancellationToken pluginToken)
        {
            if (Volatile.Read(ref shuttingDown) != 0 ||
                pluginToken.IsCancellationRequested ||
                Service.configuration?.ShowReactionNotifications != true)
                return;

            var state = errorNotifications.GetValue(reaction, static _ => new ErrorNotificationState());
            var nowTimestamp = Stopwatch.GetTimestamp();
            lock (state)
            {
                if (nowTimestamp < state.NextNotificationTimestamp)
                    return;
                state.NextNotificationTimestamp = nowTimestamp + 5 * Stopwatch.Frequency;
            }

            try
            {
                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    if (Volatile.Read(ref shuttingDown) != 0 || pluginToken.IsCancellationRequested)
                        return;
                    Service.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Puppet Master",
                        Content = $"Reaction scheduler failed: {reaction.Name}\n{error}" +
                                  (discarded > 0 ? $"\n{discarded} pending trigger(s) discarded." : string.Empty),
                        Type = NotificationType.Error,
                        InitialDuration = TimeSpan.FromSeconds(6),
                    });
                });
            }
            catch (Exception) when (pluginToken.IsCancellationRequested || Volatile.Read(ref shuttingDown) != 0)
            {
            }
        }

        private static Task UpdateReactionNotificationAsync(
            IActiveNotification notification,
            string content,
            float progress,
            CancellationToken token)
        {
            return Service.Framework.RunOnFrameworkThread(() =>
            {
                if (!token.IsCancellationRequested && notification.DismissReason == null)
                {
                    notification.Content = content;
                    notification.Progress = Math.Clamp(progress, 0, 1);
                }
            });
        }

        private static Task FinishReactionNotificationAsync(
            IActiveNotification notification,
            string reactionName,
            bool cancelled,
            string? error = null,
            CancellationToken pluginToken = default)
        {
            return Service.Framework.RunOnFrameworkThread(() =>
            {
                if (pluginToken.IsCancellationRequested)
                    return;
                notification.DismissNow();
                ActiveNotifications.TryRemove(notification, out _);
                Service.NotificationManager.AddNotification(new Notification
                {
                    Title = "Puppet Master",
                    Content = error != null
                        ? $"Reaction failed: {reactionName}\n{error}"
                        : cancelled
                            ? $"Cancelled reaction: {reactionName}"
                            : $"Completed reaction: {reactionName}",
                    Type = error != null
                        ? NotificationType.Error
                        : cancelled ? NotificationType.Warning : NotificationType.Success,
                    InitialDuration = TimeSpan.FromSeconds(4),
                });
            });
        }

        private static async Task DoCommandAsync(ReactionSnapshot reaction, XivChatType type, string message, CancellationToken pluginToken)
        {
            if (!reaction.EnabledChannels.Contains((int)type) || pluginToken.IsCancellationRequested ||
                Volatile.Read(ref reaction.Control.Generation) != reaction.Generation)
                return;

            var matchStatus = ReactionCommandMatcher.TryGenerateCommand(
                reaction.Pattern,
                message,
                reaction.Replacement,
                out var command,
                out var matchError);
            if (matchStatus == ReactionMatchStatus.NoMatch || matchStatus == ReactionMatchStatus.TimedOut)
                return;
            if (matchStatus == ReactionMatchStatus.InvalidReplacement)
            {
                await NotifyReactionErrorAsync(reaction, matchError ?? "Invalid replacement pattern.", pluginToken);
                return;
            }

            if (!ExecutionGate.TryEnter(
                    reaction.Source,
                    TimeSpan.FromSeconds(reaction.CooldownSeconds),
                    Stopwatch.GetTimestamp(),
                    out var lease,
                    out var rejectionReason))
            {
                if (rejectionReason == ReactionRejectionReason.Busy &&
                    reaction.ExecutionPolicy != ReactionExecutionPolicy.IgnoreWhileRunning)
                {
                    QueueRetrigger(reaction, command, pluginToken);
                    return;
                }
                await NotifySuppressionAsync(reaction, rejectionReason, pluginToken);
                return;
            }

            await RunAcceptedCommandAsync(reaction, command, lease!, pluginToken);
        }

        private static async Task RunAcceptedCommandAsync(
            ReactionSnapshot reaction,
            string command,
            IDisposable lease,
            CancellationToken pluginToken)
        {
            using (lease)
            {
                pluginToken.ThrowIfCancellationRequested();
                var lines = MyRegex().Split(command);
                using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(pluginToken);
                ActiveReactionCancellations[reaction.Source] = runCancellation;
                try
                {
                    if (Volatile.Read(ref reaction.Control.Generation) != reaction.Generation)
                    {
                        runCancellation.Cancel();
                        return;
                    }
                    await RunMacroAsync(lines, reaction, runCancellation, pluginToken);
                }
                finally
                {
                    ActiveReactionCancellations.TryRemove(reaction.Source, out _);
                }
            }
        }

        private static void QueueRetrigger(
            ReactionSnapshot reaction,
            string command,
            CancellationToken pluginToken)
        {
            var scheduler = retriggerQueues.GetValue(reaction.Source, source =>
                new BoundedRetriggerScheduler<PendingRetrigger>(
                    16,
                    (pending, cancellationToken) => ExecutionGate.EnterWhenAvailableAsync(
                        source,
                        TimeSpan.FromSeconds(pending.Reaction.CooldownSeconds),
                        cancellationToken),
                    (pending, lease) => RunAcceptedCommandAsync(
                        pending.Reaction,
                        pending.Command,
                        lease,
                        pending.PluginToken),
                    dropped => Interlocked.Add(ref droppedRetriggerCount, dropped),
                    (exception, discarded) =>
                    {
                        if (discarded > 0)
                            Interlocked.Add(ref droppedRetriggerCount, discarded);
                        Service.PluginLog.Error(
                            exception,
                            "Retrigger scheduler failed for {ReactionName}; discarded {DiscardedCount} pending trigger(s).",
                            source.Name,
                            discarded);
                        Track(NotifySchedulerFailureAsync(source, exception.Message, discarded, pluginToken));
                    }));
            var drainer = scheduler.Enqueue(
                reaction.ExecutionPolicy,
                new PendingRetrigger(reaction, command, pluginToken),
                pluginToken);
            if (drainer != null)
                Track(drainer);
        }
        public static void OnChatMessage(IHandleableChatMessage message)
        {
            OnChatMessage(message.LogKind, message.Timestamp, message.Sender, message.Message, message.IsHandled);
        }
        public static void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
        {
            if (Volatile.Read(ref shuttingDown) != 0)
                return;
            if (Service.configuration!.DebugLogTypes)
            {
                var prefix = int.TryParse(type.ToString(), out var number)?"[" + number + "]":"[" + ((int)type) + "][" + type + "]";
                prefix += (sender.ToString().IsNullOrEmpty() ? "" : "<" + sender + "> ");
                DebugLogBuffer.Add((int)type, $"[{DateTime.Now:HH:mm:ss}] {prefix} {message}", message.ToString());
            }

            if (isHandled) return;

            string messageStr = message.ToString();
            var token = pluginLifetime.Token;
            Track(Service.Framework.RunOnFrameworkThread(() =>
            {
                if (!token.IsCancellationRequested)
                    EnqueueMessage(type, messageStr);
            }));
        }

        private static void EnqueueMessage(XivChatType type, string message)
        {
            var snapshots = new List<ReactionSnapshot>();
            var configuration = Service.configuration!;
            foreach (var reaction in configuration.Reactions)
            {
                if (!reaction.Enabled || !reaction.EnabledChannels.Contains((int)type))
                    continue;
                var snapshot = CreateSnapshot(
                    reaction,
                    configuration.ShowReactionNotifications,
                    configuration.ShowSuppressedReactionNotifications);
                if (snapshot != null)
                    snapshots.Add(snapshot);
            }
            if (snapshots.Count == 0)
                return;

            dispatcher.Writer.TryWrite(new ChatEnvelope(type, message, snapshots));
        }

        [GeneratedRegex("\r\n|\r|\n")]
        private static partial Regex MyRegex();
    }
}

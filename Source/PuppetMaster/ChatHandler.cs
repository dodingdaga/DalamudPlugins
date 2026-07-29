using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using ECommons.Automation;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

namespace PuppetMaster
{
    public partial class ChatHandler
    {
        public ChatHandler()
        {
        }

        public static async Task RunMacroAsync(string[] lines, int index)
        {
            Service.semaphore.WaitOne();
            var reaction = Service.configuration!.Reactions[index];
            Service.semaphore.Release();

            var cancellation = new CancellationTokenSource();
            IActiveNotification? notification = null;

            if (Service.configuration.ShowReactionNotifications)
            {
                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    notification = Service.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Puppet Master",
                        Content = $"Starting reaction: {reaction.Name}",
                        Type = NotificationType.Info,
                        Progress = 0,
                        InitialDuration = TimeSpan.MaxValue,
                        UserDismissable = false,
                    });

                    notification.DrawActions += _ =>
                    {
                        if (ImGui.Button($"Cancel##PuppetMasterReaction{notification.Id}"))
                            cancellation.Cancel();
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
                            (float)lineIndex / lines.Length);
                    }

                    // Process emote
                    var isEmote = Service.Emotes.Contains(textCommand.Main);
                    if (isEmote)
                    {
                        if (reaction.MotionOnly)
                            textCommand.Args = "motion";
                    }

                    if (Service.IsCommandAllowed(reaction, textCommand.Main, out var permissionReason))
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
                            (float)(lineIndex + 1) / lines.Length);
                    }
                }

                if (notification != null)
                    await FinishReactionNotificationAsync(notification, reaction.Name, false);
            }
            catch (OperationCanceledException)
            {
                if (notification != null)
                    await FinishReactionNotificationAsync(notification, reaction.Name, true);
            }
            catch (Exception ex)
            {
                if (notification != null)
                    await FinishReactionNotificationAsync(notification, reaction.Name, false, ex.Message);
                else
                    Service.ChatGui.PrintError($"[PuppetMaster] Reaction {reaction.Name} failed: {ex.Message}");
            }
        }

        private static Task UpdateReactionNotificationAsync(IActiveNotification notification, string content, float progress)
        {
            return Service.Framework.RunOnFrameworkThread(() =>
            {
                if (notification.DismissReason == null)
                {
                    notification.Content = content;
                    notification.Progress = Math.Clamp(progress, 0, 1);
                }
            });
        }

        private static Task FinishReactionNotificationAsync(IActiveNotification notification, string reactionName, bool cancelled, string? error = null)
        {
            return Service.Framework.RunOnFrameworkThread(() =>
            {
                notification.DismissNow();
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

        public static async Task DoCommandAsync(int index, XivChatType type, String message)
        {
            // Check if part of enabled channels
            if (!Service.configuration!.Reactions[index].EnabledChannels.Contains((int)type)) return;

            var usingRegex = (Service.configuration.Reactions[index].UseRegex && Service.configuration.Reactions[index].CustomRx != null);

            // Guard against whitespace regex
            if ((usingRegex && Service.configuration.Reactions[index].CustomRx!.ToString().IsNullOrWhitespace()) ||
                (!usingRegex && Service.configuration.Reactions[index].Rx!.ToString().IsNullOrWhitespace()))
            {
#if DEBUG
                Service.ChatGui.PrintError($"[PuppetMasster][ERR] Empty RegEx [{message}]");
#endif
                return;
            }

            // Find command in message
            var matches = usingRegex ? Service.configuration.Reactions[index].CustomRx!.Matches(message) : Service.configuration.Reactions[index].Rx!.Matches(message);
            if (matches.Count == 0) return;
            var command = string.Empty;
            try
            {
                command = usingRegex ?
                    Service.configuration.Reactions[index].CustomRx!.Replace(matches[0].Value, Service.configuration.Reactions[index].ReplaceMatch) :
                    Service.configuration.Reactions[index].Rx!.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());
            } catch (Exception) { }


            var lines = MyRegex().Split(command.ToString());
            await RunMacroAsync(lines, index);
        }
        public static void OnChatMessage(IHandleableChatMessage message)
        {
            OnChatMessage(message.LogKind, message.Timestamp, message.Sender, message.Message, message.IsHandled);
        }
        public static void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
        {
            if (Service.configuration!.DebugLogTypes)
            {
                var prefix = int.TryParse(type.ToString(), out var number)?"[" + number + "]":"[" + ((int)type) + "][" + type + "]";
                prefix += (sender.ToString().IsNullOrEmpty() ? "" : "<" + sender + "> ");
                DebugLogBuffer.Add((int)type, $"[{DateTime.Now:HH:mm:ss}] {prefix} {message}", message.ToString());
            }

            if (isHandled) return;

            string messageStr = message.ToString();

            _ = Task.Run(async () =>
            {
                var tasks = new List<Task>();
                for (var index = 0; index < Service.configuration.Reactions.Count; index++)
                {
                    if (Service.configuration.Reactions[index].Enabled)
                        tasks.Add(DoCommandAsync(index, type, messageStr));
                }
                await Task.WhenAll(tasks);
            });
        }

        [GeneratedRegex("\r\n|\r|\n")]
        private static partial Regex MyRegex();
    }
}

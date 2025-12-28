using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using ECommons.Automation;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PuppetMaster
{
    public partial class ChatHandler
    {
        // Used to track cooldown for each reaction
        private static Dictionary<int, DateTime> ReactionCooldowns = new();
        // Anti-loop flag: record the last executed emote command
        private static DateTime LastEmoteExecutionTime = DateTime.MinValue;
        // Command execution lock
        private static readonly SemaphoreSlim CommandSemaphore = new SemaphoreSlim(1, 1);
        // Cancellation token source
        private static CancellationTokenSource? CurrentCancellationTokenSource;
        // Debug output control
        private static DateTime LastDebugPrintTime = DateTime.MinValue;
        private const int DEBUG_MIN_INTERVAL_MS = 500; // Minimum interval for debug output 500ms

        public ChatHandler()
        {
        }

        // Safe debug output method - fixed memory leak
        private static void SafeDebugPrint(string message)
        {
            try
            {
                if (Service.configuration?.EnableVerboseDebug != true)
                    return;

                // Limit debug output frequency to prevent spamming
                var now = DateTime.Now;
                if ((now - LastDebugPrintTime).TotalMilliseconds < DEBUG_MIN_INTERVAL_MS)
                    return;

                LastDebugPrintTime = now;
                Service.ChatGui.Print(message);
            }
            catch
            {
                // Ignore debug output errors
            }
        }

        // Game state check
        private static bool IsGameStateValid(Reaction reaction)
        {
            try
            {
                // Check if local player exists and is valid
                var localPlayer = Service.ObjectTable?.LocalPlayer;
                if (localPlayer == null) return false;

                // Use convenient methods
                if (reaction.ShouldDisableInCombat() && Service.Condition[ConditionFlag.InCombat])
                {
                    return false;
                }

                // Check cutscene
                if (reaction.ShouldDisableInCutscene() &&
                    (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
                     Service.Condition[ConditionFlag.WatchingCutscene]))
                {
                    return false;
                }

                // Check loading state
                if (reaction.ShouldDisableWhileLoading() &&
                    (Service.Condition[ConditionFlag.BetweenAreas] ||
                     Service.Condition[ConditionFlag.BetweenAreas51]))
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Check cooldown
        private static bool CheckCooldown(int index, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0) return true;

            if (!ReactionCooldowns.ContainsKey(index))
            {
                ReactionCooldowns[index] = DateTime.MinValue;
                return true;
            }

            var timeSinceLastTrigger = (DateTime.Now - ReactionCooldowns[index]).TotalSeconds;
            if (timeSinceLastTrigger >= cooldownSeconds)
            {
                ReactionCooldowns[index] = DateTime.Now;
                return true;
            }

            return false;
        }

        // Simple and safe command execution method
        private static async Task ExecuteSimpleCommands(string[] lines, Reaction reaction, CancellationToken cancellationToken)
        {
            try
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ProcessingCommand", 1, lines.Length, "start")}");

                for (int i = 0; i < lines.Length; i++)
                {
                    // Check cancellation token
                    if (cancellationToken.IsCancellationRequested)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.CommandCancelled")}");
                        return;
                    }

                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var textCommand = Service.FormatCommand(line);
                    if (string.IsNullOrEmpty(textCommand.Main))
                        continue;

                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ProcessingCommand", i + 1, lines.Length, textCommand.Main)}");

                    // Process emote
                    var isEmote = Service.Emotes.Contains(textCommand.Main);
                    if (isEmote)
                    {
                        if ((textCommand.Main == "/sit" || textCommand.Main == "/groundsit" || textCommand.Main == "/lounge") && !reaction.AllowSit)
                            textCommand.Main = "/no";
                        if (reaction.MotionOnly)
                            textCommand.Args = "motion";
                    }

                    // Special handling for /em command
                    if (textCommand.Main == "/em" || textCommand.Main == "/emote")
                    {
                        if (string.IsNullOrWhiteSpace(textCommand.Args))
                        {
                            textCommand.Args = " ";
                        }
                        else if (!textCommand.Args.StartsWith("\"") && !textCommand.Args.EndsWith("\""))
                        {
                            textCommand.Args = $"\"{textCommand.Args}\"";
                        }
                    }

                    // Check command blacklist
                    if (reaction.GetCommandBlacklist().Contains(textCommand.Main))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] Command in blacklist, skip: {textCommand.Main}");
                        continue;
                    }

                    // Check if command is allowed to execute
                    if (!reaction.AllowAllCommands && !isEmote && !reaction.GetCommandWhitelist().Contains(textCommand.Main))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] Command not in whitelist, skip: {textCommand.Main}");
                        continue;
                    }

                    // Execute command
                    if (textCommand.Main == "/wait")
                    {
                        if (float.TryParse(textCommand.Args, out var seconds))
                        {
                            var waitTime = Math.Clamp(seconds, 0.0f, 60.0f);
                            SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.Waiting", waitTime)}");

                            // Wait, but can be cancelled
                            try
                            {
                                await Task.Delay((int)(waitTime * 1000), cancellationToken);
                            }
                            catch (TaskCanceledException)
                            {
                                SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.WaitCancelled")}");
                                return;
                            }
                        }
                    }
                    else
                    {
                        var fullCommand = textCommand.ToString();
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.SendingCommand", textCommand.Main)}");

                        try
                        {
                            // Critical fix: execute Chat.SendMessage on main thread
                            await Service.Framework.RunOnFrameworkThread(() =>
                            {
                                try
                                {
                                    Chat.SendMessage(fullCommand);
                                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.CommandSent", textCommand.Main)}");
                                }
                                catch (Exception ex)
                                {
                                    Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.SendFailed", fullCommand, ex.Message)}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.FrameworkFailed", ex.Message)}");
                        }

                        // Delay between commands (except last line)
                        if (i < lines.Length - 1)
                        {
                            await Task.Delay(300, cancellationToken);
                        }
                    }
                }

                SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.AllCommandsComplete")}");
            }
            catch (Exception ex) when (!(ex is TaskCanceledException))
            {
                Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.ExecuteFailed", ex.Message)}");
            }
        }

        public static async Task RunMacroAsync(string[] lines, int index)
        {
            // Get semaphore to ensure only one command sequence executes at a time
            if (!await CommandSemaphore.WaitAsync(0))
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.AlreadyExecuting")}");
                return;
            }

            try
            {
                Service.semaphore.WaitOne();
                var reaction = Service.configuration!.Reactions[index];
                Service.semaphore.Release();

                // Check game state
                if (!IsGameStateValid(reaction))
                {
                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.GameStateFailed")}");
                    return;
                }

                // ========== Anti-loop check ==========
                bool hasEmoteCommand = false;
                foreach (var line in lines)
                {
                    var textCommand = Service.FormatCommand(line);
                    if (textCommand.Main == "/em" || textCommand.Main == "/emote" ||
                        Service.Emotes.Contains(textCommand.Main))
                    {
                        hasEmoteCommand = true;
                        break;
                    }
                }

                if (hasEmoteCommand)
                {
                    var timeSinceLastEmote = DateTime.Now - LastEmoteExecutionTime;
                    if (timeSinceLastEmote.TotalMilliseconds < 1000)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.AntiLoop")}");
                        return; // Skip if emote was executed within 1 second
                    }
                    LastEmoteExecutionTime = DateTime.Now;
                    await Task.Delay(100); // Anti-loop delay
                }
                // ========== Anti-loop check end ==========

                // Apply configured delay
                float effectiveDelay = reaction.GetDelaySeconds();
                if (effectiveDelay > 0)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ApplyDelay", effectiveDelay)}");
                    await Task.Delay((int)(effectiveDelay * 1000));
                }

                // Cancel previous command execution (if any)
                CurrentCancellationTokenSource?.Cancel();
                CurrentCancellationTokenSource = new CancellationTokenSource();

                // Execute commands
                await ExecuteSimpleCommands(lines, reaction, CurrentCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.RunMacroFailed", ex.Message)}");
            }
            finally
            {
                CommandSemaphore.Release();
            }
        }

        public static void DoCommand(int index, XivChatType type, String message, String sender)
        {
            if (Service.configuration == null) return;

            var reaction = Service.configuration.Reactions[index];

            SafeDebugPrint($"[PuppetMaster Debug] DoCommand - Index:{index} Channel:{type}");

            // Check channel
            var enabledChannels = reaction.GetEffectiveChannels();
            if (!enabledChannels.Contains((int)type))
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ChannelNotEnabled", type)}");
                return;
            }

            // Check cooldown
            float effectiveCooldown = reaction.GetCooldownSeconds();
            if (!CheckCooldown(index, effectiveCooldown))
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.Cooldown")}");
                return;
            }

            // Anti-loop check
            var timeSinceLastEmote = DateTime.Now - LastEmoteExecutionTime;
            if (timeSinceLastEmote.TotalMilliseconds < 500)
            {
                return;
            }

            string command = string.Empty;

            // ========== Process according to trigger mode ==========
            switch (reaction.TriggerMode)
            {
                case TriggerMode.SpecificPlayer:
                    // Specific player mode: check if sender is in specific player list
                    if (!IsSpecificPlayer(sender, reaction.SpecificPlayers))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.SenderNotInList", sender)}");
                        return;
                    }

                    // Extract emote command from message
                    command = ExtractEmoteFromMessage(message);
                    if (string.IsNullOrEmpty(command))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.NoEmoteFound", message)}");
                        return;
                    }
                    break;

                case TriggerMode.Keyword:
                case TriggerMode.Regex:
                default:
                    // Keyword or regex mode
                    var usingRegex = reaction.UseRegex && reaction.CustomRx != null;

                    // Guard against whitespace regex
                    if ((usingRegex && reaction.CustomRx!.ToString().IsNullOrWhitespace()) ||
                        (!usingRegex && (reaction.Rx == null || reaction.Rx.ToString().IsNullOrWhitespace())))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.RegexEmpty")}");
                        return;
                    }

                    // Find command in message
                    var matches = usingRegex ? reaction.CustomRx!.Matches(message) : reaction.Rx!.Matches(message);
                    if (matches.Count == 0)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.NoMatch", usingRegex ? reaction.CustomRx!.ToString() : reaction.Rx!.ToString())}");
                        return;
                    }

                    try
                    {
                        command = usingRegex ?
                            reaction.CustomRx!.Replace(matches[0].Value, reaction.ReplaceMatch) :
                            reaction.Rx!.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());

                        SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.MatchSuccess", command)}");
                    }
                    catch (Exception ex)
                    {
                        Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.RegexFailed", ex.Message)}");
                        return;
                    }
                    break;
            }

            if (string.IsNullOrEmpty(command))
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.Get("Debug.GeneratedCommandEmpty")}");
                return;
            }

            var linesArray = MyRegex().Split(command.ToString());

            // Start command execution (don't wait)
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunMacroAsync(linesArray, index);
                }
                catch (Exception ex)
                {
                    Service.ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Debug.CommandTaskFailed", ex.Message)}");
                }
            });
        }

        // Check if it's a specific player
        private static bool IsSpecificPlayer(string sender, List<string> specificPlayers)
        {
            if (specificPlayers == null || specificPlayers.Count == 0) return false;

            var senderName = ExtractPlayerName(sender);
            foreach (var player in specificPlayers)
            {
                // Support matching with or without server name
                var playerName = player.Split('@')[0].Trim();
                var senderNameTrimmed = senderName.Trim();

                if (senderNameTrimmed.Equals(player.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    senderNameTrimmed.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // Extract emote command from message
        private static string ExtractEmoteFromMessage(string message)
        {
            // Protection: ensure Service.Emotes is not null
            if (Service.Emotes == null || Service.Emotes.Count == 0)
            {
                return string.Empty;
            }

            // Convert message to lowercase (case-insensitive)
            var messageLower = message.ToLowerInvariant();

            // Sort by length descending, prioritize matching longer emote names
            var emotesByLength = new List<string>(Service.Emotes);
            emotesByLength.Sort((a, b) => b.Length.CompareTo(a.Length));

            // Check if message contains emote name
            foreach (var emote in emotesByLength)
            {
                try
                {
                    // Extract emote command name (remove slash)
                    var emoteName = emote.TrimStart('/').ToLowerInvariant();

                    // Ignore too short emote names
                    if (emoteName.Length < 2) continue;

                    // Use regex to ensure word boundary matching
                    var pattern = $@"\b{Regex.Escape(emoteName)}\b";
                    if (Regex.IsMatch(messageLower, pattern, RegexOptions.IgnoreCase))
                    {
                        return emote; // Return complete emote command
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return string.Empty;
        }

        // Extract pure player name (remove server name)
        private static string ExtractPlayerName(string sender)
        {
            return sender.Split('@')[0];
        }

        public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (Service.configuration == null) return;

            // Debug log
            SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ReceivedMessage", type, sender, message)}");

            if (Service.configuration.DebugLogTypes && type != XivChatType.Debug)
            {
                var prefix = int.TryParse(type.ToString(), out var number) ? "[" + number + "]" : "[" + ((int)type) + "][" + type + "]";
                prefix += (sender.ToString().IsNullOrEmpty() ? "" : "<" + sender + "> ");
                Service.ChatGui.Print(prefix + " " + message);
            }

            if (isHandled) return;

            // Critical fix: ignore messages generated by emote actions
            if (type == XivChatType.CustomEmote || (int)type == 57)
            {
                SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.IgnoreEmote", message)}");
                return;
            }

            var messageText = message.ToString();
            var senderText = sender.ToString();

            SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.StartProcessing", messageText)}");

            // Iterate through all reactions
            for (var index = 0; index < Service.configuration.Reactions.Count; index++)
            {
                var reaction = Service.configuration.Reactions[index];
                if (!reaction.Enabled)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.ReactionDisabled", index)}");
                    continue;
                }

                bool shouldProcess = false;

                // ========== Apply different filtering logic based on trigger mode ==========
                switch (reaction.TriggerMode)
                {
                    case TriggerMode.SpecificPlayer:
                        // Specific player mode: directly check if it's a specific player
                        shouldProcess = IsSpecificPlayer(senderText, reaction.SpecificPlayers);
                        break;

                    case TriggerMode.Keyword:
                    case TriggerMode.Regex:
                    default:
                        // Keyword and regex mode: apply speaker filter
                        shouldProcess = ApplySpeakerFilter(senderText, reaction.GetSpeakerFilter());

                        if (shouldProcess)
                        {
                            // Use convenient method to get player whitelist
                            var playerWhitelist = reaction.GetPlayerWhitelist();
                            if (playerWhitelist != null && playerWhitelist.Count > 0)
                            {
                                var playerName = ExtractPlayerName(senderText);
                                shouldProcess = playerWhitelist.Exists(p =>
                                    playerName.Equals(p.Split('@')[0].Trim(), StringComparison.OrdinalIgnoreCase) ||
                                    playerName.Equals(p.Trim(), StringComparison.OrdinalIgnoreCase));
                            }

                            // Use convenient method to get player blacklist
                            if (shouldProcess)
                            {
                                var playerBlacklist = reaction.GetPlayerBlacklist();
                                if (playerBlacklist != null && playerBlacklist.Count > 0)
                                {
                                    var playerName = ExtractPlayerName(senderText);
                                    shouldProcess = !playerBlacklist.Exists(p =>
                                        playerName.Equals(p.Split('@')[0].Trim(), StringComparison.OrdinalIgnoreCase) ||
                                        playerName.Equals(p.Trim(), StringComparison.OrdinalIgnoreCase));
                                }
                            }
                        }
                        break;
                }

                if (!shouldProcess)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.FilterFailed", index)}");
                    continue;
                }

                SafeDebugPrint($"[PuppetMaster Debug] {Localization.GetFormatted("Debug.FilterPassed", index)}");

                // If passed filtering, execute command processing logic
                DoCommand(index, type, messageText, senderText);
            }
        }

        // Apply speaker filter
        private static bool ApplySpeakerFilter(string sender, SpeakerFilterMode filterMode)
        {
            string? localPlayerName = Service.ObjectTable?.LocalPlayer?.Name.ToString();

            if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(localPlayerName))
                return filterMode == SpeakerFilterMode.All;

            var senderName = ExtractPlayerName(sender);
            var localName = ExtractPlayerName(localPlayerName);

            return filterMode switch
            {
                SpeakerFilterMode.All => true,
                SpeakerFilterMode.IgnoreSelf => !senderName.Equals(localName, StringComparison.OrdinalIgnoreCase),
                SpeakerFilterMode.SelfOnly => senderName.Equals(localName, StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        [GeneratedRegex("\r\n|\r|\n")]
        private static partial Regex MyRegex();
    }
}

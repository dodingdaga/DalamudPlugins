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

        // Safe debug output method
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
                // Check 1: Local player
                var localPlayer = Service.ObjectTable?.LocalPlayer;
                if (localPlayer == null)
                {
                    return false;
                }

                // Check 2: Disable in combat
                if (reaction.ShouldDisableInCombat() && Service.Condition[ConditionFlag.InCombat])
                {
                    return false;
                }

                // Check 3: Disable in cutscene
                if (reaction.ShouldDisableInCutscene() &&
                    (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
                     Service.Condition[ConditionFlag.WatchingCutscene]))
                {
                    return false;
                }

                // Check 4: Disable while loading
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
            if (cooldownSeconds <= 0)
            {
                Service.ChatGui.Print($"[PuppetMaster Debug] No cooldown, passing directly");
                return true;
            }

            // Simplified logic...
            return true; // Temporarily always return true
        }

        // Simple and safe command execution method
        private static async Task ExecuteSimpleCommands(string[] lines, Reaction reaction, CancellationToken cancellationToken)
        {
            try
            {
                SafeDebugPrint($"[PuppetMaster Debug] Starting command processing, total lines: {lines.Length}");

                for (int i = 0; i < lines.Length; i++)
                {
                    // Check cancellation token
                    if (cancellationToken.IsCancellationRequested)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] Command cancelled");
                        return;
                    }

                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var textCommand = Service.FormatCommand(line);
                    if (string.IsNullOrEmpty(textCommand.Main))
                        continue;

                    SafeDebugPrint($"[PuppetMaster Debug] Processing line {i + 1}/{lines.Length}: {textCommand.Main}");

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
                            SafeDebugPrint($"[PuppetMaster Debug] Waiting for {waitTime} seconds");

                            // Wait, but can be cancelled
                            try
                            {
                                await Task.Delay((int)(waitTime * 1000), cancellationToken);
                            }
                            catch (TaskCanceledException)
                            {
                                SafeDebugPrint($"[PuppetMaster Debug] Wait cancelled");
                                return;
                            }
                        }
                    }
                    else
                    {
                        var fullCommand = textCommand.ToString();
                        SafeDebugPrint($"[PuppetMaster Debug] Sending command: {textCommand.Main}");

                        try
                        {
                            // Critical fix: execute Chat.SendMessage on main thread
                            await Service.Framework.RunOnFrameworkThread(() =>
                            {
                                try
                                {
                                    Chat.SendMessage(fullCommand);
                                    SafeDebugPrint($"[PuppetMaster Debug] Command sent: {textCommand.Main}");
                                }
                                catch (Exception ex)
                                {
                                    Service.ChatGui.PrintError($"[PuppetMaster] Failed to send command {fullCommand}: {ex.Message}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Service.ChatGui.PrintError($"[PuppetMaster] Framework thread execution failed: {ex.Message}");
                        }

                        // Delay between commands (except last line)
                        if (i < lines.Length - 1)
                        {
                            await Task.Delay(300, cancellationToken);
                        }
                    }
                }

                SafeDebugPrint($"[PuppetMaster Debug] All commands completed");
            }
            catch (Exception ex) when (!(ex is TaskCanceledException))
            {
                Service.ChatGui.PrintError($"[PuppetMaster] Command execution failed: {ex.Message}");
            }
        }

        public static async Task RunMacroAsync(string[] lines, int index)
        {
            // Get semaphore to ensure only one command sequence executes at a time
            if (!await CommandSemaphore.WaitAsync(0))
            {
                Service.ChatGui.Print($"[PuppetMaster Debug] Another command is already executing, skip");
                return;
            }

            try
            {
                Service.semaphore.WaitOne();
                var reaction = Service.configuration!.Reactions[index];
                Service.semaphore.Release();

                // Fix: Only check game state once
                bool isGameStateValid = false;
                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    isGameStateValid = IsGameStateValid(reaction);
                });

                if (!isGameStateValid)  // Use the check result
                {
                    Service.ChatGui.Print($"[PuppetMaster Debug] Game state check failed, skip execution");
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
                        SafeDebugPrint($"[PuppetMaster Debug] Anti-loop triggered, skipping execution");
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
                    SafeDebugPrint($"[PuppetMaster Debug] Applying configured delay: {effectiveDelay} seconds");
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
                Service.ChatGui.PrintError($"[PuppetMaster] Macro execution failed: {ex.Message}");
            }
            finally
            {
                CommandSemaphore.Release();
            }
        }

        public static void DoCommand(int index, XivChatType type, String message, String sender)
        {
            if (Service.configuration == null) return;

            // Debug output
            SafeDebugPrint($"[PuppetMaster Debug] DoCommand started: Reaction {index}, Name: {Service.configuration.Reactions[index].Name}");

            var reaction = Service.configuration.Reactions[index];

            // Check 1: Channel
            var enabledChannels = reaction.GetEffectiveChannels();
            SafeDebugPrint($"[PuppetMaster Debug] Enabled channels: {string.Join(",", enabledChannels)}");
            SafeDebugPrint($"[PuppetMaster Debug] Current channel ID: {(int)type}");

            if (!enabledChannels.Contains((int)type))
            {
                SafeDebugPrint($"[PuppetMaster Debug] Channel check failed");
                return;
            }
            SafeDebugPrint($"[PuppetMaster Debug] Channel check passed");

            // Check 2: Cooldown
            float effectiveCooldown = reaction.GetCooldownSeconds();
            SafeDebugPrint($"[PuppetMaster Debug] Cooldown time: {effectiveCooldown}");
            if (!CheckCooldown(index, effectiveCooldown))
            {
                SafeDebugPrint($"[PuppetMaster Debug] Cooldown check failed");
                return;
            }
            SafeDebugPrint($"[PuppetMaster Debug] Cooldown check passed");

            // Check 3: Process according to trigger mode
            string command = string.Empty;

            switch (reaction.TriggerMode)
            {
                case TriggerMode.SpecificPlayer:
                    SafeDebugPrint($"[PuppetMaster Debug] Using Specific Player mode");

                    // Check if sender is in specific player list
                    if (!IsSpecificPlayer(sender, reaction.SpecificPlayers))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] Sender not in specific player list");
                        return;
                    }
                    SafeDebugPrint($"[PuppetMaster Debug] Sender is in specific player list");

                    // Extract emote command from message
                    command = ExtractEmoteFromMessage(message);
                    SafeDebugPrint($"[PuppetMaster Debug] Extracted emote: {command}");

                    if (string.IsNullOrEmpty(command))
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] No emote found in message");
                        return;
                    }
                    break;

                case TriggerMode.Regex:
                    SafeDebugPrint($"[PuppetMaster Debug] Using Regex mode");
                    SafeDebugPrint($"[PuppetMaster Debug] Regex pattern: {reaction.CustomPhrase}");
                    SafeDebugPrint($"[PuppetMaster Debug] Replacement text: {reaction.ReplaceMatch}");
                    SafeDebugPrint($"[PuppetMaster Debug] CustomRx: {(reaction.CustomRx == null ? "null" : "initialized")}");

                    if (reaction.CustomRx == null)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] CustomRx not initialized, attempting initialization");
                        Service.InitializeRegex(index, true);

                        if (reaction.CustomRx == null)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] CustomRx initialization failed");
                            return;
                        }
                    }

                    var regexMatches = reaction.CustomRx!.Matches(message);
                    SafeDebugPrint($"[PuppetMaster Debug] Regex match count: {regexMatches.Count}");

                    if (regexMatches.Count == 0)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] No regex match");
                        return;
                    }

                    try
                    {
                        command = reaction.CustomRx!.Replace(regexMatches[0].Value, reaction.ReplaceMatch);
                        SafeDebugPrint($"[PuppetMaster Debug] Regex generated command: {command}");
                    }
                    catch (Exception ex)
                    {
                        Service.ChatGui.PrintError($"[PuppetMaster] Regex replacement failed: {ex.Message}");
                        return;
                    }
                    break;

                case TriggerMode.Keyword:
                default:
                    SafeDebugPrint($"[PuppetMaster Debug] Using Keyword mode");
                    SafeDebugPrint($"[PuppetMaster Debug] Keyword: {reaction.TriggerPhrase}");
                    SafeDebugPrint($"[PuppetMaster Debug] Rx: {(reaction.Rx == null ? "null" : "initialized")}");

                    if (reaction.Rx == null)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] Rx not initialized, attempting initialization");
                        Service.InitializeRegex(index, true);

                        if (reaction.Rx == null)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] Rx initialization failed");
                            return;
                        }
                    }

                    var keywordMatches = reaction.Rx!.Matches(message);
                    SafeDebugPrint($"[PuppetMaster Debug] Keyword match count: {keywordMatches.Count}");

                    if (keywordMatches.Count == 0)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] No keyword match");
                        return;
                    }

                    try
                    {
                        command = reaction.Rx!.Replace(keywordMatches[0].Value, Service.GetDefaultReplaceMatch());
                        SafeDebugPrint($"[PuppetMaster Debug] Keyword generated command: {command}");
                    }
                    catch (Exception ex)
                    {
                        Service.ChatGui.PrintError($"[PuppetMaster] Keyword replacement failed: {ex.Message}");
                        return;
                    }
                    break;
            }

            if (string.IsNullOrEmpty(command))
            {
                SafeDebugPrint($"[PuppetMaster Debug] Generated command is empty");
                return;
            }

            SafeDebugPrint($"[PuppetMaster Debug] Final command: {command}");

            // Execute command
            var linesArray = MyRegex().Split(command.ToString());
            SafeDebugPrint($"[PuppetMaster Debug] Split into {linesArray.Length} command lines");

            // Async execution
            _ = Task.Run(async () =>
            {
                try
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Starting macro execution");
                    await RunMacroAsync(linesArray, index);
                }
                catch (Exception ex)
                {
                    Service.ChatGui.PrintError($"[PuppetMaster] Execution failed: {ex.Message}");
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
            if (Service.configuration == null || isHandled)
            {
                return;
            }

            var messageText = message.ToString();
            var senderText = sender.ToString();

            // Minimal debug: only output for CrossLinkShell5 messages
            if (type == XivChatType.CrossLinkShell5 && Service.configuration.EnableVerboseDebug)
            {
                SafeDebugPrint($"[PuppetMaster Debug] Received CWLS5: {senderText}: {messageText}");
                SafeDebugPrint($"[PuppetMaster Debug] Total reactions: {Service.configuration.Reactions.Count}");
            }

            // Track which modes have been matched
            bool specificPlayerMatched = false;

            // First pass: Check Specific Player mode (highest priority)
            for (var index = 0; index < Service.configuration.Reactions.Count; index++)
            {
                var reaction = Service.configuration.Reactions[index];
                if (!reaction.Enabled || reaction.TriggerMode != TriggerMode.SpecificPlayer)
                {
                    continue;
                }

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Checking Specific Player mode: {reaction.Name}");
                }

                // Check if sender is in specific player list
                if (!IsSpecificPlayer(senderText, reaction.SpecificPlayers))
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Not a specific player: {reaction.Name}");
                    }
                    continue;
                }

                // IMPORTANT FIX: Also check speaker filter for specific player mode
                if (!ApplySpeakerFilter(senderText, reaction.GetSpeakerFilter()))
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Speaker filter failed for specific player: {reaction.Name}");
                    }
                    continue;
                }

                // Check if message contains a valid emote
                string emoteCommand = ExtractEmoteFromMessage(messageText);
                if (string.IsNullOrEmpty(emoteCommand))
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Specific player but no emote found: {reaction.Name}");
                    }
                    continue; // Specific player but no emote, not a valid match
                }

                // Execute Specific Player mode
                DoCommand(index, type, messageText, senderText);
                specificPlayerMatched = true;

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] ✓ Specific Player mode matched: {reaction.Name}");
                }
            }

            // If Specific Player mode matched, return immediately (highest priority)
            if (specificPlayerMatched)
            {
                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Specific Player mode matched, ending processing");
                }
                return;
            }

            // Second pass: Check Keyword mode (medium priority)
            bool anyKeywordMatched = false;
            for (var index = 0; index < Service.configuration.Reactions.Count; index++)
            {
                var reaction = Service.configuration.Reactions[index];
                if (!reaction.Enabled || reaction.TriggerMode != TriggerMode.Keyword)
                {
                    continue;
                }

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Checking Keyword mode: {reaction.Name}");
                }

                // Check speaker filter
                if (!ApplySpeakerFilter(senderText, reaction.GetSpeakerFilter()))
                {
                    continue;
                }

                // Check if regex matches (Keyword mode uses Rx)
                if (reaction.Rx == null)
                {
                    Service.InitializeRegex(index, true);
                    if (reaction.Rx == null) continue;
                }

                var matches = reaction.Rx.Matches(messageText);
                if (matches.Count == 0)
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Keyword mode not matched: {reaction.Name}");
                    }
                    continue;
                }

                // IMPORTANT: Verify extracted text is a valid emote
                try
                {
                    string extractedCommand = reaction.Rx.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());
                    if (string.IsNullOrEmpty(extractedCommand) || !Service.Emotes.Contains(extractedCommand.Split(' ')[0]))
                    {
                        if (Service.configuration.EnableVerboseDebug)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ✗ Keyword mode but not an emote: {reaction.Name}");
                        }
                        continue; // Keyword mode but extracted text is not an emote
                    }
                }
                catch
                {
                    continue; // Extraction failed
                }

                // Execute Keyword mode
                DoCommand(index, type, messageText, senderText);
                anyKeywordMatched = true;

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] ✓ Keyword mode matched: {reaction.Name}");
                }
            }

            // If any Keyword mode matched, skip Regex mode
            if (anyKeywordMatched)
            {
                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Keyword mode matched, skipping Regex mode");
                }
                return;
            }

            // Third pass: Check Regex mode (lowest priority)
            for (var index = 0; index < Service.configuration.Reactions.Count; index++)
            {
                var reaction = Service.configuration.Reactions[index];
                if (!reaction.Enabled || reaction.TriggerMode != TriggerMode.Regex)
                {
                    continue;
                }

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Checking Regex mode: {reaction.Name}");
                }

                // Check speaker filter
                if (!ApplySpeakerFilter(senderText, reaction.GetSpeakerFilter()))
                {
                    continue;
                }

                // Check if regex matches
                if (reaction.CustomRx == null)
                {
                    Service.InitializeRegex(index, true);
                    if (reaction.CustomRx == null) continue;
                }

                var matches = reaction.CustomRx.Matches(messageText);
                if (matches.Count > 0)
                {
                    // Execute Regex mode
                    DoCommand(index, type, messageText, senderText);

                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✓ Regex mode matched: {reaction.Name}");
                    }
                }
                else
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Regex mode not matched: {reaction.Name}");
                    }
                }
            }
        }


        // Apply speaker filter
        private static bool ApplySpeakerFilter(string sender, SpeakerFilterMode filterMode)
        {
            if (filterMode == SpeakerFilterMode.All) return true;

            try
            {
                string? localPlayerName = Service.ObjectTable?.LocalPlayer?.Name.ToString();
                if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(localPlayerName))
                    return filterMode == SpeakerFilterMode.All;

                var senderName = ExtractPlayerName(sender);
                var localName = ExtractPlayerName(localPlayerName);

                return filterMode switch
                {
                    SpeakerFilterMode.IgnoreSelf => !senderName.Equals(localName, StringComparison.OrdinalIgnoreCase),
                    SpeakerFilterMode.SelfOnly => senderName.Equals(localName, StringComparison.OrdinalIgnoreCase),
                    _ => true
                };
            }
            catch
            {
                return true; // Default pass when error occurs
            }
        }

        [GeneratedRegex("\r\n|\r|\n")]
        private static partial Regex MyRegex();
    }
}

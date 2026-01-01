using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using ECommons.Automation;
using System;
using System.Collections.Generic;
using System.Text;
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

        // Check cooldown for a specific reaction
        private static bool CheckCooldown(int index, float cooldownSeconds)
        {
            // If cooldown is 0 or negative, always pass
            if (cooldownSeconds <= 0)
            {
                if (Service.configuration?.EnableVerboseDebug == true)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] No cooldown, passing directly");
                }
                return true;
            }

            // Initialize cooldown tracking if not exists
            if (!ReactionCooldowns.ContainsKey(index))
            {
                ReactionCooldowns[index] = DateTime.MinValue;

                if (Service.configuration?.EnableVerboseDebug == true)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Initialized cooldown for reaction {index}");
                }
            }
            // Calculate time since last trigger
            var timeSinceLastTrigger = (DateTime.Now - ReactionCooldowns[index]).TotalSeconds;

            if (Service.configuration?.EnableVerboseDebug == true)
            {
                SafeDebugPrint($"[PuppetMaster Debug] Cooldown check - Reaction {index}: " +
                              $"Last trigger: {ReactionCooldowns[index]}, " +
                              $"Time since: {timeSinceLastTrigger:F1}s, " +
                              $"Required: {cooldownSeconds:F1}s");
            }

            // Check if cooldown has expired
            if (timeSinceLastTrigger >= cooldownSeconds)
            {
                // Update cooldown time
                ReactionCooldowns[index] = DateTime.Now;

                if (Service.configuration?.EnableVerboseDebug == true)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Cooldown passed for reaction {index}");
                }
                return true;
            }
            else
            {
                if (Service.configuration?.EnableVerboseDebug == true)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Cooldown active for reaction {index}, " +
                                  $"waiting {cooldownSeconds - timeSinceLastTrigger:F1}s more");
                }
                return false;
            }
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

            command = ExtractCommandFromMessage(reaction, message, sender, index);

            if (string.IsNullOrEmpty(command))
            {
                SafeDebugPrint($"[PuppetMaster Debug] No command extracted, skip execution");
                return;
            }
            SafeDebugPrint($"[PuppetMaster Debug] Final command to execute: {command}");

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

        private static string ExtractCommandFromMessage(Reaction reaction, string message, string sender, int reactionIndex = -1)
        {
            SafeDebugPrint($"[PuppetMaster Debug] === ExtractCommandFromMessage开始 ===, index={reactionIndex}");

            switch (reaction.TriggerMode)
            {
                case TriggerMode.SpecificPlayer:
                    // Specific Player Mode
                    SafeDebugPrint($"[PuppetMaster Debug] ExtractCommand: SpecificPlayer mode");

                    if (reaction.AllowAllCommands)
                    {
                        // AllowAllCommands mode: try to extract raw command
                        return ExtractRawCommandForSpecificPlayer(message, reaction);
                    }

                    if (reaction.ScanFullMessageForEmote)
                    {
                        // Mode: Scan full message for emote (original behavior)
                        var extractedEmote = ExtractEmoteFromMessage(message);
                        SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage enabled, extracted emote: {extractedEmote}");
                        return extractedEmote;
                    }
                    else
                    {
                        // Mode: Require exact match (Scheme B)
                        var trimmedMessage = message.Trim();
                        var exactEmote = FindExactEmoteMatch(trimmedMessage);

                        if (!string.IsNullOrEmpty(exactEmote))
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage disabled, exact emote match: {exactEmote}");
                            return exactEmote;
                        }
                        else
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage disabled, no exact emote match for: '{trimmedMessage}'");
                            return string.Empty;
                        }
                    }

                case TriggerMode.Regex:
                    // Regex Mode - Always highest priority
                    SafeDebugPrint($"[PuppetMaster Debug] ExtractCommand: Regex mode");

                    if (reaction.CustomRx == null) return string.Empty;
                    var matchRegex = reaction.CustomRx.Matches(message);
                    if (matchRegex.Count == 0) return string.Empty;

                    if (reaction.ScanFullMessageForEmote)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage enabled for Regex, using regex replacement");
                        try
                        {
                            return reaction.CustomRx.Replace(matchRegex[0].Value, reaction.ReplaceMatch);
                        }
                        catch { return string.Empty; }
                    }
                    else
                    {
                        var matchedValue = matchRegex[0].Value.Trim();
                        var fullMessageTrimmed = message.Trim();

                        if (matchedValue == fullMessageTrimmed)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage disabled, exact regex match");
                            try
                            {
                                return reaction.CustomRx.Replace(matchRegex[0].Value, reaction.ReplaceMatch);
                            }
                            catch { return string.Empty; }
                        }
                        else
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage disabled, not exact match");
                            return string.Empty;
                        }
                    }

                case TriggerMode.Keyword:
                default:
                    // Keyword Mode (default)
                    SafeDebugPrint($"[PuppetMaster Debug] ExtractCommand: Keyword mode");

                    if (reaction.AllowAllCommands)
                    {
                        // AllowAllCommands mode: try to extract raw command from keyword match
                        SafeDebugPrint($"[PuppetMaster Debug] AllowAllCommands enabled, reaction index={reactionIndex}");
                        return ExtractRawCommandForKeyword(message, reaction, reactionIndex);
                    }

                    if (reaction.ScanFullMessageForEmote)
                    {
                        // New mode: Extract emote from full message
                        var emoteFromFullMessage = ExtractEmoteFromMessage(message);
                        SafeDebugPrint($"[PuppetMaster Debug] ScanFullMessage enabled, extracted: {emoteFromFullMessage}");
                        return emoteFromFullMessage;
                    }
                    else
                    {
                        // Original mode: Use regex replacement
                        if (reaction.Rx == null)
                        {
                            int index = reactionIndex >= 0 ? reactionIndex : FindReactionIndex(reaction);
                            if (index >= 0)
                            {
                                Service.InitializeRegex(index, true);
                            }
                            if (reaction.Rx == null) return string.Empty;
                        }

                        var matchKeyword = reaction.Rx.Matches(message);
                        if (matchKeyword.Count == 0) return string.Empty;

                        try
                        {
                            return reaction.Rx.Replace(matchKeyword[0].Value, Service.GetDefaultReplaceMatch());
                        }
                        catch { return string.Empty; }
                    }
            }
        }

        // Helper method for exact emote matching (Scheme B for Specific Player mode)
        private static string FindExactEmoteMatch(string message)
        {
            if (Service.Emotes == null || Service.Emotes.Count == 0) return string.Empty;

            // Try to find an emote that exactly matches the message (case-insensitive)
            foreach (var emote in Service.Emotes)
            {
                // Remove the leading slash for comparison
                var emoteName = emote.TrimStart('/');

                // Case-insensitive comparison
                if (string.Equals(emoteName, message, StringComparison.OrdinalIgnoreCase))
                {
                    return emote; // Return the full emote command with slash
                }
            }

            return string.Empty;
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
                    var pattern = $@"(?:^|[\s\p{{P}}\W_]|(?<=[\p{{L}}\p{{N}}])){Regex.Escape(emoteName)}(?:$|[\s\p{{P}}\W_]|(?=[\p{{L}}\p{{N}}]))";
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

        // Extract raw command for Specific Player mode when AllowAllCommands is enabled
        private static string ExtractRawCommandForSpecificPlayer(string message, Reaction reaction)
        {
            SafeDebugPrint($"[PuppetMaster Debug] AllowAllCommands enabled for SpecificPlayer, extracting raw command");

            // For Specific Player mode, use the entire message as command
            var trimmedMessage = message.Trim();
            if (string.IsNullOrEmpty(trimmedMessage))
                return string.Empty;

            // Add / prefix if not already present
            if (!trimmedMessage.StartsWith("/"))
                trimmedMessage = "/" + trimmedMessage;

            SafeDebugPrint($"[PuppetMaster Debug] Raw command extracted: {trimmedMessage}");
            return trimmedMessage;
        }

        public static string ExtractRawCommandForKeywordPublic(string message, Reaction reaction, int reactionIndex = -1)
        {
            return ExtractRawCommandForKeyword(message, reaction, reactionIndex);
        }

        // Extract raw command for Keyword mode when AllowAllCommands is enabled
        private static string ExtractRawCommandForKeyword(string message, Reaction reaction, int reactionIndex = -1)
        {
            SafeDebugPrint($"[PuppetMaster Debug] ExtractRawCommandForKeyword: reaction={reaction.Name}, index={reactionIndex}, message='{message}'");

            // Ensure Rx is initialized
            if (reaction.Rx == null)
            {
                if (reactionIndex < 0)
                {
                    reactionIndex = FindReactionIndex(reaction);
                }

                if (reactionIndex >= 0)
                {
                    Service.InitializeRegex(reactionIndex, true);
                }

                if (reaction.Rx == null)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Rx initialization failed");
                    return string.Empty;
                }
            }

            // Perform keyword match
            var match = reaction.Rx.Match(message);
            if (!match.Success)
            {
                SafeDebugPrint($"[PuppetMaster Debug] Keyword not matched in message");
                return string.Empty;
            }

            SafeDebugPrint($"[PuppetMaster Debug] Keyword matched at position {match.Index}, length {match.Length}");

            // Start from after the keyword match
            int startPos = match.Index + match.Length;

            // Check if there's an explicit # terminator
            int hashIndex = message.IndexOf('#', startPos);
            bool hasExplicitTerminator = hashIndex >= startPos;

            string rawText;

            if (hasExplicitTerminator)
            {
                // Case A: Has explicit # terminator
                // Extract everything between keyword and #
                rawText = message.Substring(startPos, hashIndex - startPos).Trim();
                SafeDebugPrint($"[PuppetMaster Debug] Using # explicit terminator, extracted: '{rawText}'");
            }
            else
            {
                // Case B: No explicit terminator
                // Skip whitespace and basic punctuation
                startPos = SkipSeparators(message, startPos);

                if (startPos >= message.Length)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] No text after keyword");
                    return string.Empty;
                }

                // Extract FF14 command (handles quotes, angle brackets, etc.)
                rawText = ExtractFF14Command(message, startPos);
                SafeDebugPrint($"[PuppetMaster Debug] No # terminator, extracted: '{rawText}'");
            }

            // Format and return command
            return FormatCommand(rawText);
        }

        // Skip whitespace and basic punctuation
        private static int SkipSeparators(string text, int start)
        {
            // Basic separators to skip
            string separators = " 　\t\r\n，。？！,.\"“”'‘’!?:;、；：";

            while (start < text.Length && separators.Contains(text[start]))
            {
                start++;
            }

            return start;
        }

        // Extract FF14 command with proper syntax handling (for non-# case)
        private static string ExtractFF14Command(string message, int startPos)
        {
            StringBuilder result = new StringBuilder();
            bool inQuotes = false;
            bool inAngleBrackets = false;
            char quoteChar = '\0';

            for (int i = startPos; i < message.Length; i++)
            {
                char c = message[i];

                // Check for # (should not happen here since we already checked)
                if (c == '#')
                {
                    break;
                }

                // Handle quotes
                if (c == '"' || c == '\'' || c == '＂' || c == '＇' || c == '“' || c == '”' || c == '‘' || c == '’')
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (c == quoteChar || IsMatchingQuote(quoteChar, c))
                    {
                        inQuotes = false;
                    }
                    result.Append(c);
                    continue;
                }

                // Handle angle brackets
                if (c == '<')
                {
                    inAngleBrackets = true;
                    result.Append(c);
                    continue;
                }

                if (c == '>' && inAngleBrackets)
                {
                    inAngleBrackets = false;
                    result.Append(c);
                    continue;
                }

                // Inside quotes/brackets, keep everything
                if (inQuotes || inAngleBrackets)
                {
                    result.Append(c);
                    continue;
                }

                // Outside quotes/brackets, check for termination
                if (IsCommandTerminator(c))
                {
                    break;
                }

                result.Append(c);
            }

            return result.ToString().Trim();
        }

        // Check if two quotes match
        private static bool IsMatchingQuote(char open, char close)
        {
            return (open == '“' && close == '”') ||
                   (open == '‘' && close == '’') ||
                   (open == '"' && (close == '"' || close == '＂')) ||
                   (open == '\'' && (close == '\'' || close == '＇'));
        }

        // Check if character should terminate command extraction (for non-# case)
        private static bool IsCommandTerminator(char c)
        {
            // Terminators: punctuation that ends a command
            string terminators = "，。？！,.\t\r\n!?:;、；：";
            return terminators.Contains(c);
        }

        // Format command: add / prefix if not already present
        private static string FormatCommand(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
            {
                return string.Empty;
            }

            // If already starts with /, keep as is
            if (rawText.StartsWith("/"))
            {
                return rawText;
            }

            // Otherwise add / prefix
            return "/" + rawText;
        }

        // Helper method to find reaction index by reaction object
        private static int FindReactionIndex(Reaction targetReaction)
        {
            if (Service.configuration == null || targetReaction == null)
                return -1;

            for (int i = 0; i < Service.configuration.Reactions.Count; i++)
            {
                var reaction = Service.configuration.Reactions[i];

                // Compare by reference (most reliable)
                if (ReferenceEquals(reaction, targetReaction))
                    return i;

                // Fallback: compare by name if reference doesn't match
                if (reaction.Name == targetReaction.Name)
                    return i;
            }

            return -1;
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


            // ========== Step 2: Check Regex mode (highest priority) ==========
            bool anyRegexMatched = false;
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
                if (matches.Count == 0)
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Regex mode not matched: {reaction.Name}");
                    }
                    continue;
                }

                if (!reaction.ScanFullMessageForEmote)
                {
                    var matchedValue = matches[0].Value.Trim();
                    var fullMessageTrimmed = messageText.Trim();

                    if (matchedValue != fullMessageTrimmed)
                    {
                        if (Service.configuration.EnableVerboseDebug)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ✗ Regex mode not exact match: {reaction.Name}");
                        }
                        continue;
                    }
                }

                // Execute Regex mode
                DoCommand(index, type, messageText, senderText);
                anyRegexMatched = true;

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] ✓ Regex mode matched: {reaction.Name}");
                }
            }

            // If Regex mode matched, skip other modes
            if (anyRegexMatched)
            {
                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Regex mode matched, skipping other modes");
                }
                return;
            }

            // ========== Step 3: Check Specific Player mode ==========
            bool specificPlayerMatched = false;
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

                // Check speaker filter
                if (!ApplySpeakerFilter(senderText, reaction.GetSpeakerFilter()))
                {
                    if (Service.configuration.EnableVerboseDebug)
                    {
                        SafeDebugPrint($"[PuppetMaster Debug] ✗ Speaker filter failed for specific player: {reaction.Name}");
                    }
                    continue;
                }

                if (reaction.AllowAllCommands)
                {
                    // AllowAllCommands enabled: skip emote checks, will be handled in ExtractCommandFromMessage
                }
                else if (reaction.ScanFullMessageForEmote)
                {
                    string emoteCommand = ExtractEmoteFromMessage(messageText);
                    if (string.IsNullOrEmpty(emoteCommand))
                    {
                        if (Service.configuration.EnableVerboseDebug)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ✗ Specific player but no emote found in full message: {reaction.Name}");
                        }
                        continue;
                    }
                }
                else
                {
                    var trimmedMessage = messageText.Trim();
                    var exactEmote = FindExactEmoteMatch(trimmedMessage);
                    if (string.IsNullOrEmpty(exactEmote))
                    {
                        if (Service.configuration.EnableVerboseDebug)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ✗ Specific player but message not exact emote: {reaction.Name}");
                        }
                        continue;
                    }
                }

                // Execute Specific Player mode
                DoCommand(index, type, messageText, senderText);
                specificPlayerMatched = true;

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] ✓ Specific Player mode matched: {reaction.Name}");
                }
            }

            // If Specific Player mode matched, skip Keyword mode
            if (specificPlayerMatched)
            {
                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] Specific Player mode matched, skipping Keyword mode");
                }
                return;
            }

            // ========== Step 4: Check Keyword mode (lowest priority) ==========
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

                if (reaction.AllowAllCommands)
                {
                    // AllowAllCommands enabled: skip emote checks, will be handled in ExtractCommandFromMessage
                }
                else if (reaction.ScanFullMessageForEmote)
                {
                    string extractedEmote = ExtractEmoteFromMessage(messageText);
                    if (string.IsNullOrEmpty(extractedEmote))
                    {
                        if (Service.configuration.EnableVerboseDebug)
                        {
                            SafeDebugPrint($"[PuppetMaster Debug] ✗ Keyword matched but no emote found in full message: {reaction.Name}");
                        }
                        continue;
                    }
                }
                else
                {
                    try
                    {
                        string extractedCommand = reaction.Rx.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());
                        if (string.IsNullOrEmpty(extractedCommand) || !Service.Emotes.Contains(extractedCommand.Split(' ')[0]))
                        {
                            if (Service.configuration.EnableVerboseDebug)
                            {
                                SafeDebugPrint($"[PuppetMaster Debug] ✗ Keyword mode but not an emote: {reaction.Name}");
                            }
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Execute Keyword mode
                DoCommand(index, type, messageText, senderText);

                if (Service.configuration.EnableVerboseDebug)
                {
                    SafeDebugPrint($"[PuppetMaster Debug] ✓ Keyword mode matched: {reaction.Name}");
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

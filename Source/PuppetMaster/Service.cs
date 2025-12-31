using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.Automation;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace PuppetMaster
{
    internal class Service
    {
        public static Plugin? plugin;
        public static Configuration? configuration;
        public static Lumina.Excel.ExcelSheet<Emote>? emoteCommands;
        public static HashSet<String> Emotes = [];
        public static Semaphore semaphore = new(initialCount: 1, maximumCount: 1);

        private const uint CHANNEL_COUNT = 23;

        // ========== Added: Localization helper methods ==========
        public static string Localize(string key)
        {
            return Localization.Get(key);
        }

        public static string LocalizeFormatted(string key, params object[] args)
        {
            try
            {
                var format = Localization.Get(key);
                return string.Format(format, args);
            }
            catch
            {
                return key;
            }
        }
        // ========== Localization helper methods end ==========

        // Safe send message method
        public static void SafeSendMessage(string command)
        {
            try
            {
                // Use static method of Chat class
                Chat.SendMessage(command);
            }
            catch (Exception ex)
            {
                ChatGui.PrintError($"[PuppetMaster] {Localization.GetFormatted("Error.SendCommand", command, ex.Message)}");
            }
        }

        // Initialize new fields
        private static void InitializeNewFields()
        {
            if (configuration == null) return;

            // Ensure GlobalSettings is not null
            configuration.GlobalSettings ??= new GlobalSettings();

            // ========== NEW: Command prefix validation and initialization ==========
            // Ensure command prefix is valid
            if (string.IsNullOrWhiteSpace(configuration.CommandPrefix))
            {
                configuration.CommandPrefix = "/puppetmaster";
            }

            // Ensure command prefix starts with /
            if (!configuration.CommandPrefix.StartsWith("/"))
            {
                configuration.CommandPrefix = "/" + configuration.CommandPrefix;
            }

            // Ensure command prefix doesn't contain spaces
            if (configuration.CommandPrefix.Contains(" "))
            {
                configuration.CommandPrefix = configuration.CommandPrefix.Split(' ')[0];
            }

            // Limit command prefix length
            if (configuration.CommandPrefix.Length > 30)
            {
                configuration.CommandPrefix = configuration.CommandPrefix.Substring(0, 30);
            }

            // Validate command prefix characters (only letters, numbers, and underscores after slash)
            if (configuration.CommandPrefix.Length > 1)
            {
                var prefixAfterSlash = configuration.CommandPrefix.Substring(1);
                if (!Regex.IsMatch(prefixAfterSlash, @"^[a-zA-Z0-9_]+$"))
                {
                    // Reset to default if invalid characters
                    configuration.CommandPrefix = "/puppetmaster";
                }
            }
            // ========== End of command prefix validation ==========
            // Initialize Pokemon mode settings
            if (string.IsNullOrEmpty(configuration.PokemonActivationPassword))
            {
                // Default to English password
                configuration.PokemonActivationPassword = "pokemon activate";
            }

            if (configuration.PokemonTimeoutMinutes < 0)
            {
                configuration.PokemonTimeoutMinutes = 30;
            }

            // Clear invalid activation data
            if (configuration.PokemonActivationTime.HasValue &&
                configuration.PokemonActivationTime.Value < DateTime.Now.AddDays(-1))
            {
                configuration.PokemonActivePlayer = null;
                configuration.PokemonActivationTime = null;
            }
            foreach (var reaction in configuration.Reactions)
            {
                // Ensure new list fields are not null
                reaction.PlayerWhitelist ??= [];
                reaction.PlayerBlacklist ??= [];
                reaction.SpecificPlayers ??= [];

                // Ensure override lists are not null
                reaction.OverridePlayerWhitelist ??= [];
                reaction.OverridePlayerBlacklist ??= [];
                reaction.OverrideCommandWhitelist ??= [];
                reaction.OverrideCommandBlacklist ??= [];

                // Initialize DelaySeconds
                if (reaction.DelaySeconds < 0)
                    reaction.DelaySeconds = 0;

                // Initialize OverrideDelaySeconds
                if (reaction.OverrideDelaySeconds < 0)
                    reaction.OverrideDelaySeconds = 0;

                // Initialize CooldownSeconds
                if (reaction.CooldownSeconds < 0)
                    reaction.CooldownSeconds = 0;

                // Initialize OverrideCooldownSeconds
                if (reaction.OverrideCooldownSeconds < 0)
                    reaction.OverrideCooldownSeconds = 0;
                // Initialize ScanFullMessageForEmote (default to true for new behavior)
                if (reaction.ScanFullMessageForEmote != true && reaction.ScanFullMessageForEmote != false)
                {
                    reaction.ScanFullMessageForEmote = true; // Default to scanning full message
                }
                // Game state restriction default values
                if (reaction.DisableInCombat != true && reaction.DisableInCombat != false)
                    reaction.DisableInCombat = true;

                if (reaction.OverrideDisableInCombat != true && reaction.OverrideDisableInCombat != false)
                    reaction.OverrideDisableInCombat = true;

                if (reaction.DisableInCutscene != true && reaction.DisableInCutscene != false)
                    reaction.DisableInCutscene = true;

                if (reaction.OverrideDisableInCutscene != true && reaction.OverrideDisableInCutscene != false)
                    reaction.OverrideDisableInCutscene = true;

                if (reaction.DisableWhileLoading != true && reaction.DisableWhileLoading != false)
                    reaction.DisableWhileLoading = true;

                if (reaction.OverrideDisableWhileLoading != true && reaction.OverrideDisableWhileLoading != false)
                    reaction.OverrideDisableWhileLoading = true;

                // Speaker filter default values
                if (reaction.FilterMode < SpeakerFilterMode.All || reaction.FilterMode > SpeakerFilterMode.SelfOnly)
                    reaction.FilterMode = SpeakerFilterMode.All;

                if (reaction.OverrideSpeakerFilter < SpeakerFilterMode.All || reaction.OverrideSpeakerFilter > SpeakerFilterMode.SelfOnly)
                    reaction.OverrideSpeakerFilter = SpeakerFilterMode.All;

                // Channel settings default values
                if (reaction.UseGlobalChannels != true && reaction.UseGlobalChannels != false)
                    reaction.UseGlobalChannels = true;
            }
        }




        public static void InitializeEmotes()
        {
            emoteCommands = DataManager.GetExcelSheet<Emote>();
            if (emoteCommands == null)
                ChatGui.PrintError($"[PuppetMaster][Error] {Localization.Get("Error.LoadEmotes")}");
            else
            {
                foreach (var emoteCommand in emoteCommands)
                {
                    var cmd = emoteCommand.TextCommand.ValueNullable?.Command.ExtractText();
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.ValueNullable?.ShortCommand.ExtractText(); ;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.ValueNullable?.Alias.ExtractText(); ;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.ValueNullable?.ShortAlias.ExtractText(); ;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                }
                if (Emotes.Count == 0)
                    ChatGui.PrintError($"[PuppetMaster][Error] {Localization.Get("Error.BuildEmotes")}");
                else if (configuration?.EnableVerboseDebug == true)
                {
                    ChatGui.Print($"[PuppetMaster Debug] Loaded {Emotes.Count} emotes");
                }
            }
        }

        public static void SetEnabledAll(bool enabled = true)
        {
            for (var i = 0; i < configuration?.Reactions.Count; i++)
                configuration.Reactions[i].Enabled = enabled;
            configuration?.Save();

            if (configuration?.EnableVerboseDebug == true && configuration.Reactions.Count > 0)
            {
                ChatGui.Print($"[PuppetMaster] " + (enabled ?
                    Localization.Get("Command.Enabled") :
                    Localization.Get("Command.Disabled")) + $" {configuration.Reactions.Count} reactions");
            }
        }

        public static void SetEnabled(string name, bool enabled = true, StringComparison sc = StringComparison.Ordinal)
        {
            var found = 0;
            for (var i = 0; i < configuration?.Reactions.Count; i++)
            {
                if (configuration.Reactions[i].Name.Equals(name, sc))
                {
                    configuration.Reactions[i].Enabled = enabled;
                    found++;
                }
            }

            if (configuration?.EnableVerboseDebug == true && found > 0)
            {
                ChatGui.Print($"[PuppetMaster] " + (enabled ?
                    Localization.Get("Command.Enabled") :
                    Localization.Get("Command.Disabled")) + $" {found} reactions, name={name}");
            }
            configuration?.Save();
        }

        public static bool IsValidReactionIndex(int index)
        {
            return (0 <= index && index < configuration?.Reactions.Count);
        }

        public static String GetDefaultRegex(int index)
        {
            if (!IsValidReactionIndex(index) || configuration!.Reactions[index].TriggerPhrase.IsNullOrWhitespace())
                return @"";

            var triggerPhrase = configuration.Reactions[index].TriggerPhrase;
            var keywords = triggerPhrase.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (keywords.Length == 0) return @"";

            var escapedKeywords = new List<string>();
            foreach (var keyword in keywords)
            {
                escapedKeywords.Add(Regex.Escape(keyword.Trim()));
            }

            // Fixed regex: supports three cases
            // 1. Keyword only
            // 2. Keyword + space + word
            // 3. Keyword + space + parentheses content
            return @"(?i)(?:" + string.Join("|", escapedKeywords) + @")(?:(?:\s+\((.*?)\))|\s+(\w+))?";
        }

        public static String GetDefaultReplaceMatch()
        {
            return @"/$1$2";
        }

        private static void InitializeRegex()
        {
            for (var i = 0; i < configuration?.Reactions.Count; i++)
                InitializeRegex(i);
        }

        public static void InitializeRegex(int index, bool reload = false)
        {
            var reaction = configuration!.Reactions[index];

            if (reaction.TriggerMode == TriggerMode.Regex || reaction.UseRegex)
            {
                // Regex mode or UseRegex is true: use CustomPhrase
                if (reload || reaction.CustomRx == null)
                {
                    try
                    {
                        reaction.CustomRx = new Regex(reaction.CustomPhrase);

                        if (configuration.EnableVerboseDebug)
                        {
                            ChatGui.Print($"[PuppetMaster Debug] Initialized custom regex: {reaction.CustomPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ChatGui.PrintError($"[PuppetMaster] {Localization.Get("Error.RegexInit")} {ex.Message}");
                        reaction.CustomRx = null;
                    }
                }
            }
            else if (reaction.TriggerMode == TriggerMode.Keyword)
            {
                // Keyword mode: use default regex
                if (reload || reaction.Rx == null)
                {
                    try
                    {
                        var regexPattern = GetDefaultRegex(index);
                        if (!string.IsNullOrEmpty(regexPattern))
                        {
                            reaction.Rx = new Regex(regexPattern);

                            if (configuration.EnableVerboseDebug)
                            {
                                ChatGui.Print($"[PuppetMaster Debug] Initialized keyword regex: {regexPattern}");
                            }
                        }
                        else
                        {
                            reaction.Rx = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        ChatGui.PrintError($"[PuppetMaster] {Localization.Get("Error.RegexInitDefault")} {ex.Message}");
                        reaction.Rx = null;
                    }
                }
            }
            // Specific player mode doesn't need regex
        }

        public struct ParsedTextCommand
        {
            public ParsedTextCommand() { }
            public string Main = string.Empty;
            public string Args = string.Empty;

            public override readonly string ToString()
            {
                if (string.IsNullOrEmpty(Args))
                    return Main;
                return Main + " " + Args;
            }
        }

        public static ParsedTextCommand FormatCommand(string command)
        {
            ParsedTextCommand textCommand = new();
            if (!string.IsNullOrEmpty(command))
            {
                command = command.Trim();
                if (command.StartsWith('/'))
                {
                    command = command.Replace('[', '<').Replace(']', '>');

                    var spaceIndex = command.IndexOf(' ');
                    if (spaceIndex >= 0)
                    {
                        textCommand.Main = command.Substring(0, spaceIndex).ToLower();
                        textCommand.Args = command.Substring(spaceIndex + 1);
                    }
                    else
                    {
                        textCommand.Main = command.ToLower();
                        textCommand.Args = string.Empty;
                    }
                }
                else
                {
                    textCommand.Main = command;
                }
            }
            return textCommand;
        }

        public static ParsedTextCommand GetTestInputCommand(int index)
        {
            ParsedTextCommand result = new();

            if (!IsValidReactionIndex(index) ||
                configuration!.Reactions[index].TestInput.IsNullOrWhitespace()) return result;

            var reaction = configuration.Reactions[index];

            // Debug log
            if (configuration.EnableVerboseDebug)
            {
                ChatGui.Print($"[PuppetMaster Debug] Test input - Mode: {reaction.TriggerMode}, Input: {reaction.TestInput}");
            }

            if (reaction.TriggerMode == TriggerMode.SpecificPlayer)
            {
                // Specific player mode: test emote extraction
                result.Main = ExtractEmoteFromMessage(reaction.TestInput, index);
                return result;
            }
            else if (reaction.TriggerMode == TriggerMode.Regex || reaction.UseRegex)
            {
                if (reaction.CustomRx == null) return result;
                var matches = reaction.CustomRx.Matches(reaction.TestInput);
                if (matches.Count != 0)
                {
                    result.Args = matches[0].ToString();
                    try
                    {
                        result.Main = reaction.CustomRx.Replace(matches[0].Value, reaction.ReplaceMatch);
                        if (result.Main == "/") result.Main = string.Empty;
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                if (reaction.Rx == null) return result;
                var matches = reaction.Rx.Matches(reaction.TestInput);
                if (matches.Count != 0)
                {
                    result.Args = matches[0].ToString();
                    try
                    {
                        result.Main = reaction.Rx.Replace(matches[0].Value, GetDefaultReplaceMatch());
                        if (result.Main == "/") result.Main = string.Empty;
                    }
                    catch (Exception) { }
                }
            }

            result.Main = FormatCommand(result.Main).ToString();

            if (configuration.EnableVerboseDebug)
            {
                ChatGui.Print($"[PuppetMaster Debug] Test result - Matched: {result.Args}, Command: {result.Main}");
            }

            return result;
        }

    
        // Test method for extracting emote
        private static string ExtractEmoteFromMessage(string message, int index)
        {
            if (Service.Emotes == null || Service.Emotes.Count == 0) return string.Empty;

            var messageLower = message.ToLowerInvariant();
            var emotesByLength = new List<string>(Service.Emotes);
            emotesByLength.Sort((a, b) => b.Length.CompareTo(a.Length));

            foreach (var emote in emotesByLength)
            {
                try
                {
                    var emoteName = emote.TrimStart('/').ToLowerInvariant();
                    if (emoteName.Length < 2) continue;

                    var pattern = $@"\b{Regex.Escape(emoteName)}\b";
                    if (Regex.IsMatch(messageLower, pattern, RegexOptions.IgnoreCase))
                    {
                        return emote;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return string.Empty;
        }

        // Configuration migration
        private static void migrateConfiguration(ref Configuration configuration)
        {
            // Version 0 to 1 migration
            if (configuration.Version == 0)
            {
                var enabledChannels = new List<int>();
                foreach (var channel in configuration.EnabledChannels)
                {
                    if (channel.Enabled)
                        enabledChannels.Add(channel.ChatType);
                }
                configuration.Reactions =
                    [
                        new() {
                            Enabled = true,
                            Name = "Reaction",
                            TriggerPhrase = configuration.TriggerPhrase,
                            AllowSit = configuration.AllowSit,
                            MotionOnly = configuration.MotionOnly,
                            AllowAllCommands = configuration.AllowAllCommands,
                            UseRegex = configuration.UseRegex,
                            CustomPhrase = configuration.CustomPhrase,
                            ReplaceMatch = configuration.ReplaceMatch,
                            TestInput = configuration.TestInput,
                            EnabledChannels = enabledChannels,
                        }
                    ];
                configuration.Version = 1;
            }

            // Version 1 to 2: Add global settings system
            if (configuration.Version == 1)
            {
                configuration.GlobalSettings = new GlobalSettings();

                foreach (var reaction in configuration.Reactions)
                {
                    reaction.UseGlobalPlayerLists = true;
                    reaction.UseGlobalCommandLists = true;
                    reaction.UseGlobalDelay = true;
                    reaction.UseGlobalCooldown = true;
                    reaction.UseGlobalSpeakerFilter = true;
                    reaction.UseGlobalGameStateRestrictions = true;

                    reaction.OverridePlayerWhitelist = new List<string>(reaction.PlayerWhitelist);
                    reaction.OverridePlayerBlacklist = new List<string>(reaction.PlayerBlacklist);
                    reaction.OverrideCommandWhitelist = new List<string>(reaction.CommandWhitelist);
                    reaction.OverrideCommandBlacklist = new List<string>(reaction.CommandBlacklist);
                }

                configuration.Version = 2;
            }

            // Version 2 to 3: Add global channel settings
            if (configuration.Version == 2)
            {
                configuration.GlobalSettings ??= new GlobalSettings();

                foreach (var reaction in configuration.Reactions)
                {
                    reaction.UseGlobalChannels = true;
                }

                configuration.Version = 3;
            }

            // Version 3 to 4: Add debug switch
            if (configuration.Version == 3)
            {
                configuration.EnableVerboseDebug = false;
                configuration.Version = 4;
            }

            // ========== NEW: Version 4 to 5: Add language and command settings ==========
            if (configuration.Version == 4)
            {
                // Initialize language setting
                configuration.Language = PluginLanguage.English;

                // Initialize command settings
                configuration.CommandPrefix = "/puppetmaster";
                configuration.EnableShortCommand = true;

                configuration.Version = 5;
            }

            // ========== NEW: Version 5 to 6: Add any future settings ==========
            // This is a placeholder for future migrations
            // if (configuration.Version == 5) { ... }
        }

        public static void InitializeConfig()
        {
            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(PluginInterface);

            if (configuration.Version < ConfigVersion.CURRENT)
            {
                migrateConfiguration(ref configuration);
            }

            if (configuration.EnabledChannels.Count != CHANNEL_COUNT)
            {
                configuration.EnabledChannels =
                [
                    new() {ChatType = (int)XivChatType.CrossLinkShell1, Name = "CWLS1"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell2, Name = "CWLS2"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell3, Name = "CWLS3"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell4, Name = "CWLS4"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell5, Name = "CWLS5"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell6, Name = "CWLS6"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell7, Name = "CWLS7"},
                    new() {ChatType = (int)XivChatType.CrossLinkShell8, Name = "CWLS8"},
                    new() {ChatType = (int)XivChatType.Ls1, Name = "LS1"},
                    new() {ChatType = (int)XivChatType.Ls2, Name = "LS2"},
                    new() {ChatType = (int)XivChatType.Ls3, Name = "LS3"},
                    new() {ChatType = (int)XivChatType.Ls4, Name = "LS4"},
                    new() {ChatType = (int)XivChatType.Ls5, Name = "LS5"},
                    new() {ChatType = (int)XivChatType.Ls6, Name = "LS6"},
                    new() {ChatType = (int)XivChatType.Ls7, Name = "LS7"},
                    new() {ChatType = (int)XivChatType.Ls8, Name = "LS8"},
                    new() {ChatType = (int)XivChatType.TellIncoming, Name = "Tell"},
                    new() {ChatType = (int)XivChatType.Say, Name = "Say"},
                    new() {ChatType = (int)XivChatType.Party, Name = "Party"},
                    new() {ChatType = (int)XivChatType.Yell, Name = "Yell"},
                    new() {ChatType = (int)XivChatType.Shout, Name = "Shout"},
                    new() {ChatType = (int)XivChatType.FreeCompany, Name = "Free Company"},
                    new() {ChatType = (int)XivChatType.Alliance, Name = "Alliance"}
                ];
            }

            // Initialize new fields
            InitializeNewFields();
            InitializeRegex();

            if (configuration.Reactions.Count == 0)
            {
                configuration.Reactions.Add(new Reaction()
                {
                    Name = "Reaction",
                    AllowAllCommands = true
                });
            }

            if (configuration.CustomChannels.Count == 0)
            {
                configuration.CustomChannels.Add(new ChannelSetting() { Name = "SystemMessage", ChatType = 57 });
            }

            // Always set to false on load
            configuration.DebugLogTypes = false;

            configuration.Save();

            if (configuration.EnableVerboseDebug)
            {
                ChatGui.Print("[PuppetMaster] " + Localization.Get("Error.InitializeConfig"));
            }
        }

        [PluginService]
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        [PluginService]
        public static ICommandManager CommandManager { get; private set; } = null!;

        [PluginService]
        public static IPlayerState PlayerState { get; private set; } = null!;

        [PluginService]
        public static IObjectTable ObjectTable { get; private set; } = null!;

        [PluginService]
        public static IChatGui ChatGui { get; private set; } = null!;

        [PluginService]
        public static ISigScanner SigScanner { get; private set; } = null!;

        [PluginService]
        public static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        public static IFramework Framework { get; private set; } = null!;

        [PluginService]
        public static ICondition Condition { get; private set; } = null!;
    }
}

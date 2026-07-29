using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

using System;
using System.Collections.Generic;
using System.IO;
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
        public static string LastDebugLogExportPath { get; private set; } = string.Empty;

        public static Semaphore semaphore = new(initialCount:1, maximumCount:1);

        private const uint CHANNEL_COUNT = 23;

        public static (string Path, int EntryCount) SaveDebugLogs()
        {
            var entries = DebugLogBuffer.Snapshot();
            if (entries.Length == 0)
                throw new InvalidOperationException("There are no captured log entries to save.");
            var configDirectory = PluginInterface.ConfigFile.DirectoryName ?? AppContext.BaseDirectory;
            LastDebugLogExportPath = DebugLogBuffer.SaveSnapshot(
                Path.Combine(configDirectory, "PuppetMasterLogs"),
                entries);
            return (LastDebugLogExportPath, entries.Length);
        }

        public static void InitializeEmotes()
        {
            emoteCommands = DataManager.GetExcelSheet<Emote>();
            if (emoteCommands == null)
                ChatGui.PrintError($"[PuppetMaster][Error] Failed to read Emotes list");
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
                    ChatGui.PrintError($"[PuppetMaster][Error] Failed to build Emotes list");
            }
        }

        public static void SetEnabledAll(bool enabled = true)
        {
            for (var i = 0; i < configuration?.Reactions.Count; i++)
            {
                configuration.Reactions[i].Enabled = enabled;
                if (!enabled)
                    ChatHandler.CancelReaction(configuration.Reactions[i]);
            }
            configuration?.Save();
#if DEBUG
            if (configuration != null && configuration.Reactions.Count > 0)
                ChatGui.Print("[PuppetMaster] "+(enabled ? "Enabled" : "Disabled") + $" {configuration.Reactions.Count} reaction" + (configuration.Reactions.Count > 1 ? "s" : ""));
#endif
        }

        public static void SetEnabled(string name, bool enabled = true, StringComparison sc = StringComparison.Ordinal)
        {
#if DEBUG
            var found = 0;
#endif
            for (var i = 0; i < configuration?.Reactions.Count; i++)
            {
                if (configuration.Reactions[i].Name.Equals(name, sc))
                {
                    configuration.Reactions[i].Enabled = enabled;
                    if (!enabled)
                        ChatHandler.CancelReaction(configuration.Reactions[i]);
#if DEBUG
                    found++;
#endif
                }
            }
#if DEBUG
            if (found > 0)
            {
                ChatGui.Print("[PuppetMaster] " + (enabled ? "Enabled" : "Disabled") + $" {found} reaction" + (found > 1 ? "s" : "") + $" with name={name}");
            }
#endif
            configuration?.Save();
        }

        public static bool IsValidReactionIndex(int index)
        {
            return (0 <= index && index < configuration?.Reactions.Count);
        }

        public static String GetDefaultRegex(int index)
        {
            return IsValidReactionIndex(index) && !configuration!.Reactions[index].TriggerPhrase.IsNullOrWhitespace() ?
                @"(?i)\b(?:" + configuration.Reactions[index].TriggerPhrase + @")\s+(?:\((.*?)\)|(\w+))" : @"";
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
            if (!reload && (reaction.UseRegex ? reaction.CustomRx != null : reaction.Rx != null))
                return;

            reaction.Rx = null;
            reaction.CustomRx = null;
            try
            {
                if (reaction.UseRegex)
                {
                    if (!reaction.CustomPhrase.IsNullOrWhitespace())
                        reaction.CustomRx = new Regex(reaction.CustomPhrase, RegexOptions.None, TimeSpan.FromMilliseconds(250));
                }
                else
                {
                    var pattern = GetDefaultRegex(index);
                    if (!pattern.IsNullOrWhitespace())
                        reaction.Rx = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
                }
            }
            catch (ArgumentException)
            {
                // Invalid patterns remain null so they cannot silently reuse stale compiled regexes.
            }
        }

        public struct ParsedTextCommand
        {
            public ParsedTextCommand() {}
            public string Main = string.Empty;
            public string Args = string.Empty;

            public override readonly string ToString()
            {
                return (Main + " " + Args).Trim();
            }
        }

        public static ParsedTextCommand FormatCommand(string command)
        {
            ParsedTextCommand textCommand = new();
            if (command != string.Empty)
            {
                command = command.Trim();
                if (command.StartsWith('/'))
                {
                    command = command.Replace('[', '<').Replace(']', '>');
                    var space = command.IndexOf(' ');
                    textCommand.Main = (space == -1 ? command : command[..space]).ToLower();
                    textCommand.Args = (space == -1 ? string.Empty : command[(space + 1)..]);
                }
                else
                    textCommand.Main = command;
            }
            return textCommand;
        }

        public static bool IsCommandAllowed(Reaction reaction, string command, out string reason)
        {
            if (reaction.CommandBlacklist.Exists(item => item.Equals(command, StringComparison.OrdinalIgnoreCase)))
            {
                reason = "command is blacklisted";
                return false;
            }

            if (Emotes.Contains(command))
            {
                reason = "emote allowed by default";
                return true;
            }

            if (reaction.AllowAllCommands)
            {
                reason = "Allow all text commands is enabled";
                return true;
            }

            if (reaction.CommandWhitelist.Exists(item => item.Equals(command, StringComparison.OrdinalIgnoreCase)))
            {
                reason = "command is whitelisted";
                return true;
            }

            reason = "command is not whitelisted";
            return false;
        }

        public static ParsedTextCommand GetTestInputCommand(int index)
        {
            ParsedTextCommand result = new();

            if (!IsValidReactionIndex(index) ||
                configuration!.Reactions[index].TestInput.IsNullOrWhitespace()) return result;

            var usingRegex = (configuration.Reactions[index].UseRegex && configuration.Reactions[index].CustomRx != null);

            if ((usingRegex && Service.configuration.Reactions[index].CustomRx!.ToString().IsNullOrWhitespace()) ||
                (!usingRegex && Service.configuration.Reactions[index].Rx!.ToString().IsNullOrWhitespace()))
            {
                return result;
            }

#if DEBUG
            /*
            if (usingRegex)
                ChatGui.Print($"[TESTING] Pattern:{configuration.Reactions[index].CustomRx} Replace:{configuration.Reactions[index].ReplaceMatch} Test:{configuration.Reactions[index].TestInput}");
            else
                ChatGui.Print($"[TESTING] Pattern:{configuration.Reactions[index].Rx} Test:{configuration.Reactions[index].TestInput}");
            */
#endif

            try
            {
                var matches = usingRegex
                    ? configuration.Reactions[index].CustomRx!.Matches(configuration.Reactions[index].TestInput)
                    : configuration.Reactions[index].Rx!.Matches(configuration.Reactions[index].TestInput);
                if (matches.Count != 0)
                {
                    result.Args = matches[0].ToString();
                    result.Main = usingRegex ?
                        configuration.Reactions[index].CustomRx!.Replace(matches[0].Value, configuration.Reactions[index].ReplaceMatch) :
                        configuration.Reactions[index].Rx!.Replace(matches[0].Value, GetDefaultReplaceMatch());
                }
            }
            catch (RegexMatchTimeoutException) { }
            catch (ArgumentException) { }
            result.Main = FormatCommand(result.Main).ToString();
            return result;
        }

        public static void InitializeConfig()
        {
            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(PluginInterface);

            var requiresMigration = configuration.Version < ConfigVersion.CURRENT;
            if (requiresMigration)
                BackupConfiguration(configuration.Version);

            ConfigurationMigrator.MigrateAndNormalize(configuration);

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

            if (configuration.Reactions.Count == 0)
            {
                configuration.Reactions.Add(Reaction.CreateDefault(
                    commandWhitelist: configuration.DefaultCommandWhitelist,
                    commandBlacklist: configuration.DefaultCommandBlacklist,
                    allowAllCommands: configuration.DefaultAllowAllCommands,
                    motionOnly: configuration.DefaultMotionOnly,
                    enabledChannels: configuration.DefaultEnabledChannels));
            }

            InitializeRegex();

            if (configuration.CustomChannels.Count == 0)
            {
                configuration.CustomChannels.Add(new ChannelSetting() { Name = "SystemMessage", ChatType = 57 });
            }

            // Always set to false on load
            configuration.DebugLogTypes = false;

            configuration.Save();
        }

        private static void BackupConfiguration(int sourceVersion)
        {
            var configFile = PluginInterface.ConfigFile;
            if (!configFile.Exists)
                return;

            var backupName = $"{Path.GetFileNameWithoutExtension(configFile.Name)}.v{sourceVersion}.{DateTime.UtcNow:yyyyMMddHHmmss}.backup.json";
            var backupPath = Path.Combine(configFile.DirectoryName!, backupName);
            File.Copy(configFile.FullName, backupPath, overwrite: false);
        }

        [PluginService]
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        [PluginService]
        public static ICommandManager CommandManager { get; private set; } = null!;

        //[PluginService]
        //public static IClientState ClientState { get; private set; } = null!;

        [PluginService]
        public static IChatGui ChatGui { get; private set; } = null!;

        [PluginService]
        public static ISigScanner SigScanner { get; private set; } = null!;

        //[PluginService]
        //public static IObjectTable ObjectTable { get; private set; } = null!;

        //[PluginService]
        //public static ITargetManager TargetManager { get; private set; } = null!;

        [PluginService]
        public static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        public static IFramework Framework { get; private set; } = null!;

        [PluginService]
        public static INotificationManager NotificationManager { get; private set; } = null!;

        [PluginService]
        public static IPluginLog PluginLog { get; private set; } = null!;
    }
}

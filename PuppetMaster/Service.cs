using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Lumina.Excel.GeneratedSheets;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    internal class Service
    {
        public static Plugin? plugin;
        public static Configuration? configuration;
        public static Lumina.Excel.ExcelSheet<Emote>? emoteCommands;
        public static HashSet<String> Emotes = [];

        private const uint CHANNEL_COUNT = 23;

        public static void InitializeEmotes()
        {
            emoteCommands = DataManager.GetExcelSheet<Emote>();
            if (emoteCommands == null)
                ChatGui.Print($"[PuppetMaster][Error] Failed to read Emotes list");
            else
            {
                foreach (var emoteCommand in emoteCommands)
                {
                    var cmd = emoteCommand.TextCommand.Value?.Command;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.Value?.ShortCommand;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.Value?.Alias;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                    cmd = emoteCommand.TextCommand.Value?.ShortAlias;
                    if (cmd != null && cmd != "") Emotes.Add(cmd);
                }
                if (Emotes.Count == 0)
                    ChatGui.Print($"[PuppetMaster][Error] Failed to build Emotes list");
            }
        }

        public static void SetEnabledAll(bool enabled=true)
        {
            for(int i = 0; i < configuration?.Reactions.Count; i++)
                configuration.Reactions[i].Enabled = enabled;
            configuration?.Save();
        }

        public static void SetEnabled(string name, bool enabled = true, StringComparison sc = StringComparison.Ordinal)
        {
            for (int i = 0;i < configuration?.Reactions.Count;i++)     
            {
                if (configuration.Reactions[i].Name.Equals(name,sc))
                {
                    configuration.Reactions[i].Enabled = enabled;
                }
            }
            configuration?.Save();
        }

        public static bool IsValidReactionIndex(int index)
        {
            return (0 <= index && index < configuration?.Reactions.Count);
        }

        private static String GetDefaultRegex(int index)
        {
            return configuration != null && IsValidReactionIndex(index)? @"(?i)\b(?:" + configuration.Reactions[index].TriggerPhrase + @")\s+(?:\((.*?)\)|(\w+))" : @"";
        }
        public static String GetDefaultReplaceMatch()
        {
            return @"/$1$2";
        }

        public static void InitializeRegex()
        {
            for (var i = 0; i < configuration?.Reactions.Count; i++)
                InitializeRegex(i);
        }

        public static void InitializeRegex(int index, bool reload=false)
        {
            if (configuration == null) return;

            if (configuration.Reactions[index].UseRegex)
            {
                if (string.IsNullOrEmpty(configuration.Reactions[index].CustomPhrase))
                {
                    configuration.Reactions[index].CustomPhrase = GetDefaultRegex(index);
                    configuration.Reactions[index].ReplaceMatch = GetDefaultReplaceMatch();
                    configuration.Save();
                    reload = true;
                }
                if (configuration.Reactions[index].CustomRx == null || reload)
                    try { configuration.Reactions[index].CustomRx = new Regex(configuration.Reactions[index].CustomPhrase); } catch (Exception) { }
            }
            else if (configuration.Reactions[index].Rx == null || reload)
                try { configuration.Reactions[index].Rx = new Regex(GetDefaultRegex(index)); } catch (Exception) { }
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

        public static ParsedTextCommand GetTestInputCommand(int index)
        {
            ParsedTextCommand result = new();

            if (configuration == null || !IsValidReactionIndex(index)) return result;

            InitializeRegex(index);

            var usingRegex = (configuration.Reactions[index].UseRegex && configuration.Reactions[index].CustomRx != null);
            var matches = usingRegex ? configuration.Reactions[index].CustomRx!.Matches(configuration.Reactions[index].TestInput) : configuration.Reactions[index].Rx!.Matches(configuration.Reactions[index].TestInput);
            if (matches.Count != 0)
            {
                result.Args = matches[0].ToString();
                try
                {
                    result.Main = usingRegex ?
                    configuration.Reactions[index].CustomRx!.Replace(matches[0].Value, configuration.Reactions[index].ReplaceMatch) :
                    configuration.Reactions[index].Rx!.Replace(matches[0].Value, GetDefaultReplaceMatch()); ;
                }
                catch (Exception) { }
            }
            result.Main = FormatCommand(result.Main).ToString();
            return result;
        }

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

            InitializeRegex();

            if (configuration.Reactions.Count == 0)
            {
                var reaction = new Reaction();
                reaction.Enabled = true;
                reaction.Name = "Reaction";
                reaction.TriggerPhrase = "please do";
                reaction.EnabledChannels.Add((int)XivChatType.Say);
                configuration.Reactions.Add(reaction);
            }

            if (configuration.CustomChannels.Count == 0)
            {
                var channelSetting = new ChannelSetting();
                channelSetting.Name = "SystemMessage";
                channelSetting.ChatType = 57;
                configuration.CustomChannels.Add(channelSetting);
            }

            // Always set to false on load
            configuration.DebugLogTypes = false;

            configuration.Save();
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
    }
}

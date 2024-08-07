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
        public static Regex? Rx;
        public static Regex? CustomRx;
        public static List<XivChatType> enabledChannels = [];
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

        private static String GetDefaultRegex()
        {
            return @"(?i)\b(?:" + configuration!.TriggerPhrase + @")\s+(?:\((.*?)\)|(\w+))";
        }
        public static String GetDefaultReplaceMatch()
        {
            return @"/$1$2";
        }

        public static void InitializeRegex(bool reload=false)
        {
            if (configuration!.UseRegex)
            {
                if (String.IsNullOrEmpty(configuration.CustomPhrase))
                {
                    configuration.CustomPhrase = GetDefaultRegex();
                    configuration.ReplaceMatch = GetDefaultReplaceMatch();
                    configuration.Save();
                    reload = true;
                }
                if (CustomRx == null || reload)
                    try { CustomRx = new Regex(configuration.CustomPhrase); } catch (Exception) { }
            }
            else if (Rx == null || reload)
                try { Rx = new Regex(GetDefaultRegex()); } catch (Exception) { }
        }

        public struct ParsedTextCommand
        {
            public ParsedTextCommand() {}
            public String Main = String.Empty;
            public String Args = String.Empty;

            public override readonly String ToString()
            {
                return (Main + " " + Args).Trim();
            }
        }

        public static ParsedTextCommand FormatCommand(String command)
        {
            ParsedTextCommand textCommand = new();
            if (command != String.Empty)
            {
                command = command.Trim();
                if (command.StartsWith('/'))
                {
                    command = command.Replace('[', '<').Replace(']', '>');
                    int space = command.IndexOf(' ');
                    textCommand.Main = (space == -1 ? command : command[..space]).ToLower();
                    textCommand.Args = (space == -1 ? String.Empty : command[(space + 1)..]);
                }
                else
                    textCommand.Main = command;
            }
            return textCommand;
        }

        public static ParsedTextCommand GetTestInputCommand()
        {
            ParsedTextCommand result = new();
            InitializeRegex();

            bool usingRegex = (configuration!.UseRegex && CustomRx != null);
            MatchCollection matches = usingRegex ? CustomRx!.Matches(configuration!.TestInput) : Rx!.Matches(configuration!.TestInput);
            if (matches.Count != 0)
            {
                result.Args = matches[0].ToString();
                try
                {
                    result.Main = usingRegex ?
                    CustomRx!.Replace(matches[0].Value, configuration.ReplaceMatch) :
                    Rx!.Replace(matches[0].Value, GetDefaultReplaceMatch()); ;
                }
                catch (Exception) { }
            }
            result.Main = FormatCommand(result.Main).ToString();
            return result;
        }

        public static void InitializeConfig()
        {
            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(PluginInterface);

            if (configuration.EnabledChannels.Count != CHANNEL_COUNT)
            {
                configuration.EnabledChannels =
                [
                    new() {ChatType = XivChatType.CrossLinkShell1, Name = "CWLS1"},
                    new() {ChatType = XivChatType.CrossLinkShell2, Name = "CWLS2"},
                    new() {ChatType = XivChatType.CrossLinkShell3, Name = "CWLS3"},
                    new() {ChatType = XivChatType.CrossLinkShell4, Name = "CWLS4"},
                    new() {ChatType = XivChatType.CrossLinkShell5, Name = "CWLS5"},
                    new() {ChatType = XivChatType.CrossLinkShell6, Name = "CWLS6"},
                    new() {ChatType = XivChatType.CrossLinkShell7, Name = "CWLS7"},
                    new() {ChatType = XivChatType.CrossLinkShell8, Name = "CWLS8"},
                    new() {ChatType = XivChatType.Ls1, Name = "LS1"},
                    new() {ChatType = XivChatType.Ls2, Name = "LS2"},
                    new() {ChatType = XivChatType.Ls3, Name = "LS3"},
                    new() {ChatType = XivChatType.Ls4, Name = "LS4"},
                    new() {ChatType = XivChatType.Ls5, Name = "LS5"},
                    new() {ChatType = XivChatType.Ls6, Name = "LS6"},
                    new() {ChatType = XivChatType.Ls7, Name = "LS7"},
                    new() {ChatType = XivChatType.Ls8, Name = "LS8"},
                    new() {ChatType = XivChatType.TellIncoming, Name = "Tell"},
                    new() {ChatType = XivChatType.Say, Name = "Say", Enabled = true},
                    new() {ChatType = XivChatType.Party, Name = "Party"},
                    new() {ChatType = XivChatType.Yell, Name = "Yell"},
                    new() {ChatType = XivChatType.Shout, Name = "Shout"},
                    new() {ChatType = XivChatType.FreeCompany, Name = "Free Company"},
                    new() {ChatType = XivChatType.Alliance, Name = "Alliance"}
                ];
            }

            for (int i = 0; i < CHANNEL_COUNT; ++i)
            {
                if (configuration.EnabledChannels[i].Enabled)
                    enabledChannels.Add(configuration.EnabledChannels[i].ChatType);
            }

            InitializeRegex();

            configuration.Save();
        }

        [PluginService]
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        [PluginService]
        public static ICommandManager CommandManager { get; private set; } = null!;

        [PluginService]
        public static IClientState ClientState { get; private set; } = null!;

        [PluginService]
        public static IChatGui ChatGui { get; private set; } = null!;

        [PluginService]
        public static ISigScanner SigScanner { get; private set; } = null!;

        [PluginService]
        public static IObjectTable ObjectTable { get; private set; } = null!;

        [PluginService]
        public static ITargetManager TargetManager { get; private set; } = null!;

        [PluginService]
        public static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        public static IFramework Framework { get; private set; } = null!;
    }
}

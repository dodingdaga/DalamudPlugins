using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#nullable enable
namespace PuppetMaster
{
    internal class Service
    {
        public static PuppetMaster.Plugin? plugin;
        public static Configuration? configuration;
        public static Regex? Rx;
        public static Regex? CustomRx;
        //public static List<XivChatType> enabledChannels = new List<XivChatType>();
        public static ExcelSheet<Emote>? emoteCommands;
        public static HashSet<string> Emotes = new HashSet<string>();
        private const uint CHANNEL_COUNT = 23;

        public static void InitializeEmotes()
        {
            Service.emoteCommands = Service.DataManager.GetExcelSheet<Emote>();

            if (Service.emoteCommands == null)
            {
                Service.ChatGui.Print("[PuppetMaster][Error] Failed to read Emotes list", null, new ushort?());
            } else {
                foreach (Emote emoteCommand in Service.emoteCommands)
                {
                    SeString command = emoteCommand.TextCommand.Value?.Command;

                    if (command != null && command != "")
                        Service.Emotes.Add(command);

                    SeString shortCommand = emoteCommand.TextCommand.Value?.ShortCommand;

                    if (shortCommand != null && shortCommand != "")
                        Service.Emotes.Add(shortCommand);

                    SeString alias = emoteCommand.TextCommand.Value?.Alias;

                    if (alias != null && alias != "")
                        Service.Emotes.Add(alias);

                    SeString shortAlias = emoteCommand.TextCommand.Value?.ShortAlias;

                    if (shortAlias != null && shortAlias != "")
                        Service.Emotes.Add(shortAlias);
                }

                if (Service.Emotes.Count != 0)
                    return;

                Service.ChatGui.Print("[PuppetMaster][Error] Failed to build Emotes list", null, new ushort?());
            }
        }

        private static string GetDefaultRegex()
        {
            return "(?i)\\b(?:" + Service.configuration.DefaultTriggerPhrase + ")\\s+(?:\\((.*?)\\)|(\\w+))";
        }

        public static string GetDefaultReplaceMatch() => "/$1$2";

        public static void InitializeRegex(bool reload = false)
        {
            if (Service.configuration.DefaultUseRegex)
            {
                if (string.IsNullOrEmpty(Service.configuration.DefaultCustomPhrase))
                {
                    Service.configuration.DefaultCustomPhrase = Service.GetDefaultRegex();
                    Service.configuration.DefaultReplaceMatch = Service.GetDefaultReplaceMatch();
                    Service.configuration.Save();
                    reload = true;
                }
                if (!(Service.CustomRx == null | reload))
                    return;
                try
                {
                    Service.CustomRx = new Regex(Service.configuration.DefaultCustomPhrase);
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                if (!(Service.Rx == null | reload))
                    return;
                try
                {
                    Service.Rx = new Regex(Service.GetDefaultRegex());
                }
                catch (Exception ex)
                {
                }
            }
        }

        public static Service.ParsedTextCommand FormatCommand(string command)
        {
            Service.ParsedTextCommand parsedTextCommand = new Service.ParsedTextCommand();
            if (command != string.Empty)
            {
                command = command.Trim();
                if (command.StartsWith('/'))
                {
                    command = command.Replace('[', '<').Replace(']', '>');
                    int length = command.IndexOf(' ');
                    parsedTextCommand.Main = (length == -1 ? command : command.Substring(0, length)).ToLower();
                    ref Service.ParsedTextCommand local = ref parsedTextCommand;
                    string str1;
                    if (length != -1)
                    {
                        string str2 = command;
                        int startIndex = length + 1;
                        str1 = str2.Substring(startIndex, str2.Length - startIndex);
                    }
                    else
                        str1 = string.Empty;
                    local.Args = str1;
                }
                else
                    parsedTextCommand.Main = command;
            }
            return parsedTextCommand;
        }

        public static void InitializeConfig()
        {
            if (!(Service.PluginInterface.GetPluginConfig() is Configuration configuration))
                configuration = new Configuration();
            Service.configuration = configuration;
            Service.configuration.Initialize(Service.PluginInterface);
            if (Service.configuration.DefaultEnabledChannels.Count != 23)
                Service.configuration.DefaultEnabledChannels = new List<ChannelSetting>()
        {
          new ChannelSetting()
          {
            ChatType = (XivChatType) 37,
            Name = "CWLS1"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 101,
            Name = "CWLS2"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 102,
            Name = "CWLS3"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 103,
            Name = "CWLS4"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 104,
            Name = "CWLS5"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 105,
            Name = "CWLS6"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 106,
            Name = "CWLS7"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 107,
            Name = "CWLS8"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 16,
            Name = "LS1"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 17,
            Name = "LS2"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 18,
            Name = "LS3"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 19,
            Name = "LS4"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 20,
            Name = "LS5"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 21,
            Name = "LS6"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 22,
            Name = "LS7"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 23,
            Name = "LS8"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 13,
            Name = "Tell"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 10,
            Name = "Say"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 14,
            Name = "Party"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 30,
            Name = "Yell"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 11,
            Name = "Shout"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 24,
            Name = "Free Company"
          },
          new ChannelSetting()
          {
            ChatType = (XivChatType) 15,
            Name = "Alliance"
          }
        };
            Service.InitializeRegex();
            Service.configuration.Save();
        }

        [PluginService]
        public static DalamudPluginInterface PluginInterface { get; private set; } = (DalamudPluginInterface)null;

        [PluginService]
        public static ICommandManager CommandManager { get; private set; } = (ICommandManager)null;

        [PluginService]
        public static IClientState ClientState { get; private set; } = (IClientState)null;

        [PluginService]
        public static IChatGui ChatGui { get; private set; } = (IChatGui)null;

        [PluginService]
        public static ISigScanner SigScanner { get; private set; } = (ISigScanner)null;

        [PluginService]
        public static IObjectTable ObjectTable { get; private set; } = (IObjectTable)null;

        [PluginService]
        public static ITargetManager TargetManager { get; private set; } = (ITargetManager)null;

        [PluginService]
        public static IDataManager DataManager { get; private set; } = (IDataManager)null;

        [PluginService]
        public static IFramework Framework { get; private set; } = (IFramework)null;

        public struct ParsedTextCommand
        {
            public string Main;
            public string Args;

            public ParsedTextCommand()
            {
                this.Main = string.Empty;
                this.Args = string.Empty;
            }

            public override string ToString() => (this.Main + " " + this.Args).Trim();
        }
    }
}

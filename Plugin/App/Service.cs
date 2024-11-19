using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#nullable enable
namespace PuppetMaster
{
    public class Service
    {
        public static Configuration? configuration;
        public static Regex? Rx;
        public static Regex? CustomRx;
        public static ExcelSheet<Emote>? emoteCommands;
        public static ExcelSheet<World>? worlds;
        public static HashSet<string> Emotes = new HashSet<string>();
        public static List<World> Worlds = new List<World>();

        public static void InitializeEmotes()
        {
            Service.emoteCommands = Service.DataManager.GetExcelSheet<Emote>();

            if (Service.emoteCommands == null)
            {
                Service.Logger.Error("[PuppetMaster] [Error] Failed to read Emotes list");
            }
            else
            {
                foreach (var emoteCommand in Service.emoteCommands)
                {
                    try
                    {
                        // Tenter d'accéder à TextCommand.Value
                        var textCommand = emoteCommand.TextCommand.Value;

                        if (!string.IsNullOrEmpty(textCommand.Command.ToString()))
                            Service.Emotes.Add(textCommand.Command.ToString());

                        if (!string.IsNullOrEmpty(textCommand.ShortCommand.ToString()))
                            Service.Emotes.Add(textCommand.ShortCommand.ToString());

                        if (!string.IsNullOrEmpty(textCommand.Alias.ToString()))
                            Service.Emotes.Add(textCommand.Alias.ToString());

                        if (!string.IsNullOrEmpty(textCommand.ShortAlias.ToString()))
                            Service.Emotes.Add(textCommand.ShortAlias.ToString());
                    }
                    catch (InvalidOperationException)
                    {
                        Service.Logger.Warning($"EmoteCommand.TextCommand.Value is null for emoteCommand: {emoteCommand}");
                    }
                }

                /*foreach (var emoteCommand in Service.emoteCommands)
                {
                    var command = emoteCommand.TextCommand.Value?.Command;

                    if (command != null && command != "")
                        Service.Emotes.Add(command);

                    var shortCommand = emoteCommand.TextCommand.Value?.ShortCommand;

                    if (shortCommand != null && shortCommand != "")
                        Service.Emotes.Add(shortCommand);

                    var alias = emoteCommand.TextCommand.Value?.Alias;

                    if (alias != null && alias != "")
                        Service.Emotes.Add(alias);

                    var shortAlias = emoteCommand.TextCommand.Value?.ShortAlias;

                    if (shortAlias != null && shortAlias != "")
                        Service.Emotes.Add(shortAlias);
                }*/

                if (Service.Emotes.Count != 0)
                    return;

                Service.Logger.Error("[PuppetMaster] [Error] Failed to build Emotes list");
            }
        }

        public static void InitializeWorlds()
        {
            Service.worlds = Service.DataManager.GetExcelSheet<World>();

            if (Service.worlds == null)
            {
                Service.Logger.Error("[PuppetMaster] [Error] Failed to read Worlds list");
            }
            else
            {
                foreach (var world in Service.worlds)
                {
                    if (world.IsPublic)
                        Service.Worlds.Add(world);
                }

                if (Service.Worlds.Count != 0)
                {
                    foreach (var world in Service.Worlds)
                    {
                        Service.Logger.Info("[PuppetMaster] World : Name - " + world.Name.ToString() + " /// Internal_name - " + world.InternalName.ToString() + " /// Region - " + world.Region);
                    }

                    return;
                }

                Service.Logger.Error("[PuppetMaster] [Error] Failed to build Worlds list");
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
                } catch (Exception ex) 
                {
                    Service.Logger.Error("[PuppetMaster] [Error] Could not initialize default Regex");
                }
            } else {
                if (!(Service.Rx == null | reload))
                    return;

                try
                {
                    Service.Rx = new Regex(Service.GetDefaultRegex());
                } catch (Exception ex)
                {
                    Service.Logger.Error("[PuppetMaster] [Error] Could not initialize default Regex");
                }
            }
        }

        public static Service.ParsedTextCommand GetTestInputCommand()
        {
            Service.ParsedTextCommand testInputCommand = new Service.ParsedTextCommand();
            Service.InitializeRegex();

            bool flag = Service.configuration.DefaultUseRegex && Service.CustomRx != null;
            MatchCollection matchCollection = flag ? Service.CustomRx.Matches(Service.configuration.DefaultTestInput) : Service.Rx.Matches(Service.configuration.DefaultTestInput);

            if (matchCollection.Count != 0)
            {
                testInputCommand.Args = matchCollection[0].ToString();

                try {
                    testInputCommand.Main = flag ? Service.CustomRx.Replace(matchCollection[0].Value, Service.configuration.DefaultReplaceMatch) : Service.Rx.Replace(matchCollection[0].Value, Service.GetDefaultReplaceMatch());
                } catch (Exception ex) {
                    Service.Logger.Error("[PuppetMaster] [Error] Error using Regex");
                }
            }

            testInputCommand.Main = Service.FormatCommand(testInputCommand.Main).ToString();

            return testInputCommand;
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
                    } else {
                        str1 = string.Empty;
                    }
                    local.Args = str1;
                } else {
                    parsedTextCommand.Main = command;
                }
            }

            return parsedTextCommand;
        }

        public static void InitializeConfig()
        {
            Service.configuration = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            if (Service.configuration.DefaultEnabledChannels.Count != 23)
            {
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
            }

            Service.InitializeRegex();
            Service.configuration.Save();
        }


        [PluginService]
        public static ICommandManager? CommandManager { get; private set; } = null;

        [PluginService]
        public static IClientState? ClientState { get; private set; } = null;

        [PluginService]
        public static IChatGui? ChatGui { get; private set; } = null;

        [PluginService]
        public static ISigScanner? SigScanner { get; private set; } = null;

        [PluginService]
        public static IObjectTable? ObjectTable { get; private set; } = null;

        [PluginService]
        public static ITargetManager? TargetManager { get; private set; } = null;

        [PluginService]
        public static IDataManager? DataManager { get; private set; } = null;

        [PluginService]
        public static IFramework? Framework { get; private set; } = null;
        
        [PluginService]
        public static IPluginLog? Logger { get; private set; } = null;

        public struct ParsedTextCommand
        {
            public string Main;
            public string Args;

            public ParsedTextCommand()
            {
                this.Main = string.Empty;
                this.Args = string.Empty;
            }

            public override readonly string ToString() => (this.Main + " " + this.Args).Trim();
        }
    }
}
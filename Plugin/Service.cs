using Dalamud.Game.Text;
using NoireLib;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PuppetMaster_Enhanced;

public class Service
{
    public static Regex? Rx;
    public static Regex? CustomRx;

    private static string GetDefaultRegex()
    {
        return $"(?i)\\b(?:{Configuration.Instance.DefaultTriggerPhrase})\\s+(?:\\((.*?)\\)|(\\w+))";
    }

    public static string GetDefaultReplaceMatch() => "/$1$2";

    public static void InitializeRegex(bool reload = false)
    {
        if (Configuration.Instance.DefaultUseRegex)
        {
            if (string.IsNullOrEmpty(Configuration.Instance.DefaultCustomPhrase))
            {
                Configuration.Instance.DefaultCustomPhrase = GetDefaultRegex();
                Configuration.Instance.DefaultReplaceMatch = GetDefaultReplaceMatch();
                reload = true;
            }

            if (!(CustomRx == null || reload))
                return;

            try
            {
                CustomRx = new Regex(Configuration.Instance.DefaultCustomPhrase);
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, "[PuppetMaster] [Error] Could not initialize default Regex");
            }
        }
        else
        {
            if (!(Rx == null || reload))
                return;

            try
            {
                Rx = new Regex(GetDefaultRegex());
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, "[PuppetMaster] [Error] Could not initialize default Regex");
            }
        }
    }

    public static ParsedTextCommand GetTestInputCommand()
    {
        ParsedTextCommand testInputCommand = new ParsedTextCommand();
        InitializeRegex();

        bool flag = Configuration.Instance.DefaultUseRegex && CustomRx != null;
        MatchCollection? matchCollection = flag ? CustomRx?.Matches(Configuration.Instance.DefaultTestInput) : Rx?.Matches(Configuration.Instance.DefaultTestInput);

        if (matchCollection != null && matchCollection.Count != 0)
        {
            testInputCommand.Args = matchCollection[0].ToString();

            try
            {
                testInputCommand.Main = flag ? CustomRx!.Replace(matchCollection[0].Value, Configuration.Instance.DefaultReplaceMatch) : Rx!.Replace(matchCollection[0].Value, GetDefaultReplaceMatch());
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, "[PuppetMaster] [Error] Error using Regex");
            }
        }

        testInputCommand.Main = Service.FormatCommand(testInputCommand.Main).ToString();

        return testInputCommand;
    }

    public static ParsedTextCommand FormatCommand(string command)
    {
        ParsedTextCommand parsedTextCommand = new ParsedTextCommand();

        if (command != string.Empty)
        {
            command = command.Trim();

            if (command.StartsWith('/'))
            {
                command = command.Replace('[', '<').Replace(']', '>');

                int length = command.IndexOf(' ');

                parsedTextCommand.Main = (length == -1 ? command : command.Substring(0, length)).ToLower();

                ref ParsedTextCommand local = ref parsedTextCommand;

                string str1;

                if (length != -1)
                {
                    string str2 = command;
                    int startIndex = length + 1;
                    str1 = str2.Substring(startIndex, str2.Length - startIndex);
                }
                else
                {
                    str1 = string.Empty;
                }
                local.Args = str1;
            }
            else
            {
                parsedTextCommand.Main = command;
            }
        }

        return parsedTextCommand;
    }

    public static void InitializeConfig()
    {
        if (Configuration.Instance.DefaultEnabledChannels.Count != 23)
        {
            Configuration.Instance.DefaultEnabledChannels = new List<ChannelSetting>()
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

        InitializeRegex();
        Configuration.Instance.Save();
    }

    public struct ParsedTextCommand
    {
        public string Main;
        public string Args;

        public ParsedTextCommand()
        {
            Main = string.Empty;
            Args = string.Empty;
        }

        public override readonly string ToString() => (Main + " " + Args).Trim();
    }
}
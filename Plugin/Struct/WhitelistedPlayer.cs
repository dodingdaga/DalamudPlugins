using Dalamud.Game.Text;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PuppetMaster_Enhanced;

[Serializable]
public class WhitelistedPlayer
{
    public string Id { get; init; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerWorld { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool StrictPlayerName { get; set; } = true;


    public bool UseAllDefaultSettings { get; set; } = true;
    public bool UseDefaultTrigger { get; set; } = false;
    public bool UseDefaultRequests { get; set; } = false;
    public bool UseDefaultEnabledChannels { get; set; } = false;


    public string TriggerPhrase { get; set; } = string.Empty;
    public string CustomPhrase { get; set; } = string.Empty;
    public string ReplaceMatch { get; set; } = string.Empty;
    public bool UseRegex { get; set; } = false;
    public string TestInput { get; set; } = string.Empty;


    public bool AllowSit { get; set; } = false;
    public bool MotionOnly { get; set; } = false;
    public bool AllowAllCommands { get; set; } = false;


    public Regex? Rx { get; set; }
    public Regex? CustomRx { get; set; }

    public Service.ParsedTextCommand TextCommand { get; set; } = new Service.ParsedTextCommand();

    public List<ChannelSetting> EnabledChannels { get; set; } = new List<ChannelSetting>()
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

    public WhitelistedPlayer()
    {
        Id = RandomGenerator.GenerateGuidString();
    }

    public WhitelistedPlayer(string playerName) : this()
    {
        PlayerName = playerName;
    }

    public void AddEnabledChannel(ChannelSetting channelSetting) => EnabledChannels.Add(channelSetting);

    public void RemoveEnabledChannel(ChannelSetting channelSetting) => EnabledChannels.Remove(channelSetting);

    public void InitializeRegex(bool reload = false)
    {
        if (UseRegex)
        {
            if (string.IsNullOrEmpty(CustomPhrase))
            {
                CustomPhrase = GetDefaultRegex();
                ReplaceMatch = GetDefaultReplaceMatch();
                Configuration.Instance.Save();
                reload = true;
            }

            if (!(CustomRx == null || reload))
                return;

            try
            {
                CustomRx = new Regex(CustomPhrase);
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(this, ex, "[PuppetMaster] [Error] Could not initialize Regex for Whitelist entry n°" + this.Id);
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
                NoireLogger.LogError(this, ex, "[PuppetMaster] [Error] Could not initialize Regex for Whitelist entry n°" + this.Id);
            }
        }
    }

    public Service.ParsedTextCommand GetTestInputCommand()
    {
        Service.ParsedTextCommand testInputCommand = new Service.ParsedTextCommand();
        this.InitializeRegex();

        bool flag = UseRegex && CustomRx != null;
        MatchCollection? matchCollection = flag ? CustomRx?.Matches(TestInput) : Rx?.Matches(TestInput);

        if (matchCollection != null && matchCollection.Count != 0)
        {
            testInputCommand.Args = matchCollection[0].ToString();

            try
            {
                testInputCommand.Main = flag ? CustomRx!.Replace(matchCollection[0].Value, ReplaceMatch) : Rx!.Replace(matchCollection[0].Value, GetDefaultReplaceMatch());
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(this, ex, "[PuppetMaster] [Error] Error using Regex");
            }
        }

        testInputCommand.Main = Service.FormatCommand(testInputCommand.Main).ToString();

        return testInputCommand;
    }

    private string GetDefaultRegex()
    {
        return $"(?i)\\b(?:{TriggerPhrase})\\s+(?:\\((.*?)\\)|(\\w+))";
    }

    public string GetDefaultReplaceMatch() => "/$1$2";
}

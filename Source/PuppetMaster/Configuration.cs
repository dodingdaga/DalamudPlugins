using Dalamud.Configuration;
using Dalamud.Plugin;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    public class ConfigVersion
    {
        public const int CURRENT = 1;
    }

    public class ChannelSetting
    {
        public int ChatType { get; set; }
        public string Name { get; set; } = string.Empty;
        //---- Deprecated, setting will be managed per Reaction
        public bool Enabled { get; set; }
    }

    public class Reaction
    {
        public bool Enabled { get; set; } = false;
        public string Name { get; set; } = string.Empty;
        public string TriggerPhrase { get; set; } = string.Empty;
        public bool AllowSit { get; set; } = false;
        public bool MotionOnly { get; set; } = true;
        public bool AllowAllCommands { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public string CustomPhrase { get; set; } = string.Empty;
        public string ReplaceMatch { get; set; } = string.Empty;
        public string TestInput { get; set; } = string.Empty;
        public List<int> EnabledChannels { get; set; } = [];
        public List<string> CommandWhitelist { get; set; } = [];
        public List<string> CommandBlacklist { get; set; } = [];
        public Regex? Rx;
        public Regex? CustomRx;
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = ConfigVersion.CURRENT;

        //---- Version 0 Config [DEPRECATED, WILL NOT BE USED]
        public string TriggerPhrase { get; set; } = "please do";
        public bool AllowSit { get; set; } = false;
        public bool MotionOnly { get; set; } = true;
        public bool AllowAllCommands { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public string CustomPhrase { get; set; } = string.Empty;
        public string ReplaceMatch { get; set; } = string.Empty;
        public string TestInput { get; set; } = string.Empty;

        //---- Version 1 Config
        public List<ChannelSetting> EnabledChannels { get; set; } = [];
        public List<ChannelSetting> CustomChannels { get; set; } = [];
        public List<Reaction> Reactions { get; set; } = [];
        public int CurrentReactionEdit = -1;
        public bool DebugLogTypes { get; set; } = false;
        public uint MaxRegexLength { get; set; } = 1000;

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}

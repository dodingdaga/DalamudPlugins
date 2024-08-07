using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;

namespace PuppetMaster
{
    public class ChannelSetting
    {
        public XivChatType ChatType { get; set; }
        public bool Enabled { get; set; }
        public String Name { get; set; } = String.Empty;
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public String TriggerPhrase { get; set; } = "please do";
        public bool AllowSit { get; set; } = false;
        public bool MotionOnly { get; set; } = true;
        public bool AllowAllCommands { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public String CustomPhrase { get; set; } = String.Empty;
        public String ReplaceMatch { get; set; } = String.Empty;
        public String TestInput { get; set; } = String.Empty;

        public List<ChannelSetting> EnabledChannels { get; set; } = [];

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

using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

#nullable enable
namespace PuppetMaster
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public int Version { get; set; }

        public string DefaultTriggerPhrase { get; set; } = string.Empty;

        public bool DefaultAllowSit { get; set; } = false;

        public bool EnablePlugin { get; set; } = true;

        public bool EnableWhitelist { get; set; } = false;

        public bool EnableBlacklist { get; set; } = false;

        public bool DefaultMotionOnly { get; set; } = false;

        public bool DefaultAllowAllCommands { get; set; } = false;

        public bool DefaultUseRegex { get; set; } = false;

        public string DefaultCustomPhrase { get; set; } = string.Empty;

        public string DefaultReplaceMatch { get; set; } = string.Empty;

        public List<ChannelSetting> DefaultEnabledChannels { get; set; } = new List<ChannelSetting>();

        public List<BlacklistedPlayer> BlacklistedPlayers { get; set; } = new List<BlacklistedPlayer>();

        public List<WhitelistedPlayer> WhitelistedPlayers { get; set; } = new List<WhitelistedPlayer>();

        public void AddBlacklistedPlayer(BlacklistedPlayer blacklistedPlayer) => this.BlacklistedPlayers.Add(blacklistedPlayer);
        public void RemoveBlacklistedPlayer(BlacklistedPlayer blacklistedPlayer) => this.BlacklistedPlayers.Remove(blacklistedPlayer);

        public void AddWhitelistedPlayer(WhitelistedPlayer whitelistedPlayer) => this.WhitelistedPlayers.Add(whitelistedPlayer);
        public void RemoveWhitelistedPlayer(WhitelistedPlayer whitelistedPlayer) => this.WhitelistedPlayers.Remove(whitelistedPlayer);

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }
    }
}

using NoireLib.Configuration;
using System;
using System.Collections.Generic;

namespace PuppetMaster_Enhanced;

[Serializable]
public class Configuration : NoireConfigBase<Configuration>
{
    public override string GetConfigFileName() => "PuppetMasterConfig";

    public virtual string DefaultTriggerPhrase { get; set; } = string.Empty;

    public virtual bool DefaultAllowSit { get; set; } = false;

    public virtual bool EnablePlugin { get; set; } = true;

    public virtual bool EnableWhitelist { get; set; } = true;

    public virtual bool EnableBlacklist { get; set; } = true;

    public virtual bool DefaultMotionOnly { get; set; } = false;

    public virtual bool DefaultAllowAllCommands { get; set; } = false;

    public virtual bool DefaultUseRegex { get; set; } = false;

    public virtual string DefaultCustomPhrase { get; set; } = string.Empty;

    public virtual string DefaultReplaceMatch { get; set; } = string.Empty;

    public virtual string DefaultTestInput { get; set; } = string.Empty;

    public virtual List<ChannelSetting> DefaultEnabledChannels { get; set; } = new List<ChannelSetting>();

    public virtual List<BlacklistedPlayer> BlacklistedPlayers { get; set; } = new List<BlacklistedPlayer>();

    public virtual List<WhitelistedPlayer> WhitelistedPlayers { get; set; } = new List<WhitelistedPlayer>();
}

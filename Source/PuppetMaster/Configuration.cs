using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    // Enum definitions for trigger modes
    public enum TriggerMode
    {
        Keyword,        // Keyword mode
        Regex,          // Regular expression mode
        SpecificPlayer  // Specific player mode
    }

    // Enum definitions for speaker filter modes
    public enum SpeakerFilterMode
    {
        All,        // Recognize all (default, original behavior)
        IgnoreSelf, // Ignore self
        SelfOnly    // Recognize self only
    }

    // Enum definitions for plugin languages
    public enum PluginLanguage
    {
        English,
        Chinese,
        //Japanese removed
    }

    public class ConfigVersion
    {
        public const int CURRENT = 5; // Version number updated to 5 (added command settings)
    }

    public class ChannelSetting
    {
        public int ChatType { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public class Reaction
    {
        // Whether to use global settings
        public bool UseGlobalPlayerLists { get; set; } = true;
        public bool UseGlobalCommandLists { get; set; } = true;
        public bool UseGlobalDelay { get; set; } = true;
        public bool UseGlobalCooldown { get; set; } = true;
        public bool UseGlobalSpeakerFilter { get; set; } = true;
        public bool UseGlobalGameStateRestrictions { get; set; } = true;
        public bool UseGlobalChannels { get; set; } = true;
        public bool ScanFullMessageForEmote { get; set; } = true;

        // Override settings (used when not using global settings)
        public List<string> OverridePlayerWhitelist { get; set; } = [];
        public List<string> OverridePlayerBlacklist { get; set; } = [];
        public List<string> OverrideCommandWhitelist { get; set; } = [];
        public List<string> OverrideCommandBlacklist { get; set; } = [];
        public float OverrideDelaySeconds { get; set; } = 0f;
        public float OverrideCooldownSeconds { get; set; } = 0f;
        public SpeakerFilterMode OverrideSpeakerFilter { get; set; } = SpeakerFilterMode.All;
        public bool OverrideDisableInCombat { get; set; } = true;
        public bool OverrideDisableInCutscene { get; set; } = true;
        public bool OverrideDisableWhileLoading { get; set; } = true;


        // Convenience methods - get actual values to use
        public List<string> GetEffectivePlayerWhitelist(GlobalSettings global)
        {
            if (!UseGlobalPlayerLists || !global.UseGlobalPlayerLists)
                return OverridePlayerWhitelist ?? new List<string>();
            return global.GlobalPlayerWhitelist ?? new List<string>();
        }

        public List<string> GetEffectivePlayerBlacklist(GlobalSettings global)
        {
            if (!UseGlobalPlayerLists || !global.UseGlobalPlayerLists)
                return OverridePlayerBlacklist ?? new List<string>();
            return global.GlobalPlayerBlacklist ?? new List<string>();
        }

        public List<string> GetEffectiveCommandWhitelist(GlobalSettings global)
        {
            if (!UseGlobalCommandLists || !global.UseGlobalCommandLists)
                return OverrideCommandWhitelist ?? new List<string>();
            return global.GlobalCommandWhitelist ?? new List<string>();
        }

        public List<string> GetEffectiveCommandBlacklist(GlobalSettings global)
        {
            if (!UseGlobalCommandLists || !global.UseGlobalCommandLists)
                return OverrideCommandBlacklist ?? new List<string>();
            return global.GlobalCommandBlacklist ?? new List<string>();
        }

        public float GetEffectiveDelaySeconds(GlobalSettings global)
        {
            float baseDelay = UseGlobalDelay ? global.GlobalDelaySeconds : 0;
            return baseDelay + OverrideDelaySeconds;
        }

        public float GetEffectiveCooldownSeconds(GlobalSettings global)
        {
            float baseCooldown = UseGlobalCooldown ? global.GlobalCooldownSeconds : 0;
            return baseCooldown + OverrideCooldownSeconds;
        }

        public SpeakerFilterMode GetEffectiveSpeakerFilter(GlobalSettings global)
        {
            if (!UseGlobalSpeakerFilter)
                return OverrideSpeakerFilter;
            return global.GlobalSpeakerFilter;
        }

        public bool GetEffectiveDisableInCombat(GlobalSettings global)
        {
            if (!UseGlobalGameStateRestrictions || !global.UseGlobalGameStateRestrictions)
                return OverrideDisableInCombat;
            return global.GlobalDisableInCombat;
        }

        public bool GetEffectiveDisableInCutscene(GlobalSettings global)
        {
            if (!UseGlobalGameStateRestrictions || !global.UseGlobalGameStateRestrictions)
                return OverrideDisableInCutscene;
            return global.GlobalDisableInCutscene;
        }

        public bool GetEffectiveDisableWhileLoading(GlobalSettings global)
        {
            if (!UseGlobalGameStateRestrictions || !global.UseGlobalGameStateRestrictions)
                return OverrideDisableWhileLoading;
            return global.GlobalDisableWhileLoading;
        }

        // Convenience method: get actual enabled channels
        public List<int> GetEffectiveChannels()
        {
            // 首先检查 Service.configuration 是否已初始化
            if (Service.configuration == null)
            {
                return EnabledChannels ?? new List<int>();
            }

            var global = Service.configuration.GlobalSettings;
            if (!UseGlobalChannels || !global.UseGlobalChannels)
                return EnabledChannels ?? new List<int>();

            return global.GlobalEnabledChannels ?? new List<int>();
        }

        // Backward compatibility wrapper methods
        public List<int> GetChannels() => GetEffectiveChannels();
        public List<string> GetPlayerWhitelist()
            => GetEffectivePlayerWhitelist(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public List<string> GetPlayerBlacklist()
            => GetEffectivePlayerBlacklist(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public List<string> GetCommandWhitelist()
            => GetEffectiveCommandWhitelist(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public List<string> GetCommandBlacklist()
            => GetEffectiveCommandBlacklist(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public float GetDelaySeconds()
            => GetEffectiveDelaySeconds(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public float GetCooldownSeconds()
            => GetEffectiveCooldownSeconds(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public bool ShouldDisableInCombat()
            => GetEffectiveDisableInCombat(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public bool ShouldDisableInCutscene()
            => GetEffectiveDisableInCutscene(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public bool ShouldDisableWhileLoading()
            => GetEffectiveDisableWhileLoading(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        public SpeakerFilterMode GetSpeakerFilter()
            => GetEffectiveSpeakerFilter(Service.configuration?.GlobalSettings ?? new GlobalSettings());

        // Original properties
        public bool Enabled { get; set; } = false;
        public string Name { get; set; } = string.Empty;
        public string TriggerPhrase { get; set; } = string.Empty;
        public bool AllowSit { get; set; } = false;
        public bool MotionOnly { get; set; } = true;
        public bool AllowAllCommands { get; set; } = true;
        public bool UseRegex { get; set; } = false;
        public TriggerMode TriggerMode { get; set; } = TriggerMode.Keyword;
        public bool DisableInCombat { get; set; } = true;
        public bool DisableInCutscene { get; set; } = true;
        public bool DisableWhileLoading { get; set; } = true;
        public float DelaySeconds { get; set; } = 0f;
        public float CooldownSeconds { get; set; } = 0f;
        public List<string> PlayerWhitelist { get; set; } = [];
        public List<string> PlayerBlacklist { get; set; } = [];
        public List<string> SpecificPlayers { get; set; } = [];
        public SpeakerFilterMode FilterMode { get; set; } = SpeakerFilterMode.All;
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
    public class GlobalSettings
    {
        public bool UseGlobalPlayerLists { get; set; } = true;
        public bool UseGlobalCommandLists { get; set; } = true;
        public List<string> GlobalPlayerWhitelist { get; set; } = [];
        public List<string> GlobalPlayerBlacklist { get; set; } = [];
        public List<string> GlobalCommandWhitelist { get; set; } = [];
        public List<string> GlobalCommandBlacklist { get; set; } = [];
        public float GlobalDelaySeconds { get; set; } = 0f;
        public float GlobalCooldownSeconds { get; set; } = 0f;
        public SpeakerFilterMode GlobalSpeakerFilter { get; set; } = SpeakerFilterMode.All;
        public bool UseGlobalChannels { get; set; } = true;
        public List<int> GlobalEnabledChannels { get; set; } = [];
        public bool UseGlobalGameStateRestrictions { get; set; } = true;
        public bool GlobalDisableInCombat { get; set; } = true;
        public bool GlobalDisableInCutscene { get; set; } = true;
        public bool GlobalDisableWhileLoading { get; set; } = true;
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = ConfigVersion.CURRENT;

        // Language settings
        public PluginLanguage Language { get; set; } = PluginLanguage.English;

        // Debug switch
        public bool EnableVerboseDebug { get; set; } = false;

        // Command settings (new in version 5)
        public string CommandPrefix { get; set; } = "/puppetmaster";
        public bool EnableShortCommand { get; set; } = true;

        //---- Version 0 Config [DEPRECATED]
        public string TriggerPhrase { get; set; } = "please do";
        public bool AllowSit { get; set; } = false;

        public bool MotionOnly { get; set; } = true;
        public bool AllowAllCommands { get; set; } = false;
        public bool UseRegex { get; set; } = false;
        public string CustomPhrase { get; set; } = string.Empty;
        public string ReplaceMatch { get; set; } = string.Empty;
        public string TestInput { get; set; } = string.Empty;

        //---- Version 1+ Config
        public List<ChannelSetting> EnabledChannels { get; set; } = [];
        public List<ChannelSetting> CustomChannels { get; set; } = [];
        public List<Reaction> Reactions { get; set; } = [];
        public int CurrentReactionEdit = -1;
        public bool DebugLogTypes { get; set; } = false;
        public int MaxRegexLength { get; set; } = 1000;

        // Global settings
        public GlobalSettings GlobalSettings { get; set; } = new();

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

        // Configuration migration
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

            // Version 1 to 2: Add global settings system
            if (configuration.Version == 1)
            {
                configuration.GlobalSettings = new GlobalSettings();

                foreach (var reaction in configuration.Reactions)
                {
                    reaction.UseGlobalPlayerLists = true;
                    reaction.UseGlobalCommandLists = true;
                    reaction.UseGlobalDelay = true;
                    reaction.UseGlobalCooldown = true;
                    reaction.UseGlobalSpeakerFilter = true;
                    reaction.UseGlobalGameStateRestrictions = true;

                    reaction.OverridePlayerWhitelist = new List<string>(reaction.PlayerWhitelist);
                    reaction.OverridePlayerBlacklist = new List<string>(reaction.PlayerBlacklist);
                    reaction.OverrideCommandWhitelist = new List<string>(reaction.CommandWhitelist);
                    reaction.OverrideCommandBlacklist = new List<string>(reaction.CommandBlacklist);
                }

                configuration.Version = 2;
            }

            // Version 2 to 3: Add global channel settings
            if (configuration.Version == 2)
            {
                configuration.GlobalSettings ??= new GlobalSettings();

                foreach (var reaction in configuration.Reactions)
                {
                    reaction.UseGlobalChannels = true;
                }

                configuration.Version = 3;
            }

            // Version 3 to 4: Add debug switch
            if (configuration.Version == 3)
            {
                // Add EnableVerboseDebug field
                configuration.EnableVerboseDebug = false;
                configuration.Version = 4;
            }

            // Version 4 to 5: Add language and command settings
            if (configuration.Version == 4)
            {
                configuration.Language = PluginLanguage.English;
                configuration.CommandPrefix = "/puppetmaster";
                configuration.EnableShortCommand = true;
                configuration.Version = 5;
            }
        }
    }
}

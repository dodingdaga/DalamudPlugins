using System;
using System.Collections.Generic;

namespace PuppetMaster;

public static class ConfigurationMigrator
{
    private static readonly string[] LegacySitCommands = ["/sit", "/groundsit", "/lounge"];

    public static bool MigrateAndNormalize(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Version < 0)
            throw new InvalidOperationException($"Invalid configuration version: {configuration.Version}.");
        if (configuration.Version > ConfigVersion.CURRENT)
            throw new InvalidOperationException(
                $"Configuration v{configuration.Version} is newer than supported v{ConfigVersion.CURRENT}.");

        var changed = false;
        while (configuration.Version < ConfigVersion.CURRENT)
        {
            changed |= configuration.Version switch
            {
                0 => MigrateV0ToV1(configuration),
                1 => MigrateV1ToV2(configuration),
                2 => MigrateV2ToV3(configuration),
                _ => throw new InvalidOperationException(
                    $"No migration path exists from configuration v{configuration.Version}."),
            };
        }

        changed |= NormalizeLegacyCommandRules(configuration);
        Validate(configuration);
        return changed;
    }

    private static bool MigrateV0ToV1(Configuration configuration)
    {
        var enabledChannels = new List<int>();
        foreach (var channel in configuration.EnabledChannels)
        {
            if (channel.Enabled)
                enabledChannels.Add(channel.ChatType);
        }

        configuration.Reactions =
        [
            new Reaction
            {
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
            },
        ];
        configuration.Version = 1;
        return true;
    }

    private static bool MigrateV1ToV2(Configuration configuration)
    {
        configuration.ShowReactionNotifications = false;
        configuration.ShowSuppressedReactionNotifications = false;
        configuration.DefaultCommandWhitelist = [];
        configuration.DefaultCommandBlacklist = [.. LegacySitCommands];
        configuration.DefaultAllowAllCommands = false;
        configuration.DefaultMotionOnly = true;
        configuration.DefaultEnabledChannels = [];
        configuration.Version = 2;
        return true;
    }

    private static bool MigrateV2ToV3(Configuration configuration)
    {
        configuration.Reactions ??= [];
        foreach (var reaction in configuration.Reactions)
        {
            reaction.ProgressNotifications = ReactionNotificationSetting.Inherit;
            reaction.SuppressedNotifications = ReactionNotificationSetting.Inherit;
        }
        configuration.Version = 3;
        return true;
    }

    private static bool NormalizeLegacyCommandRules(Configuration configuration)
    {
        var changed = false;
        if (configuration.EnabledChannels == null)
        {
            configuration.EnabledChannels = [];
            changed = true;
        }
        if (configuration.CustomChannels == null)
        {
            configuration.CustomChannels = [];
            changed = true;
        }
        if (configuration.Reactions == null)
        {
            configuration.Reactions = [];
            changed = true;
        }
        changed |= RemoveNullEntries(configuration.EnabledChannels);
        changed |= RemoveNullEntries(configuration.CustomChannels);
        changed |= RemoveNullEntries(configuration.Reactions);
        if (configuration.DefaultCommandWhitelist == null)
        {
            configuration.DefaultCommandWhitelist = [];
            changed = true;
        }
        if (configuration.DefaultCommandBlacklist == null)
        {
            configuration.DefaultCommandBlacklist = [.. LegacySitCommands];
            changed = true;
        }
        if (configuration.DefaultEnabledChannels == null)
        {
            configuration.DefaultEnabledChannels = [];
            changed = true;
        }
        changed |= NormalizeCustomChannels(configuration.CustomChannels);
        changed |= DeduplicateCommands(configuration.DefaultCommandWhitelist);
        changed |= DeduplicateCommands(configuration.DefaultCommandBlacklist);
        changed |= DeduplicateChannels(configuration.DefaultEnabledChannels);

        foreach (var reaction in configuration.Reactions)
        {
            if (reaction.Name == null) { reaction.Name = string.Empty; changed = true; }
            if (reaction.TriggerPhrase == null) { reaction.TriggerPhrase = Reaction.DefaultTriggerPhrase; changed = true; }
            if (reaction.CustomPhrase == null) { reaction.CustomPhrase = string.Empty; changed = true; }
            if (reaction.ReplaceMatch == null) { reaction.ReplaceMatch = string.Empty; changed = true; }
            if (reaction.TestInput == null) { reaction.TestInput = string.Empty; changed = true; }
            if (reaction.EnabledChannels == null) { reaction.EnabledChannels = []; changed = true; }
            if (reaction.CommandWhitelist == null) { reaction.CommandWhitelist = []; changed = true; }
            if (reaction.CommandBlacklist == null) { reaction.CommandBlacklist = []; changed = true; }

            changed |= DeduplicateChannels(reaction.EnabledChannels);
            changed |= DeduplicateCommands(reaction.CommandWhitelist);
            changed |= DeduplicateCommands(reaction.CommandBlacklist);

            if (!reaction.AllowSit)
            {
                foreach (var command in LegacySitCommands)
                {
                    if (!ContainsCommand(reaction.CommandBlacklist, command))
                    {
                        reaction.CommandBlacklist.Add(command);
                        changed = true;
                    }
                }

                reaction.AllowSit = true;
                changed = true;
            }

            if (reaction.CooldownSeconds < 0)
            {
                reaction.CooldownSeconds = 0;
                changed = true;
            }
            if (!Enum.IsDefined(reaction.ExecutionPolicy))
            {
                reaction.ExecutionPolicy = ReactionExecutionPolicy.QueueEveryTrigger;
                changed = true;
            }
            if (!Enum.IsDefined(reaction.ProgressNotifications))
            {
                reaction.ProgressNotifications = ReactionNotificationSetting.Inherit;
                changed = true;
            }
            if (!Enum.IsDefined(reaction.SuppressedNotifications))
            {
                reaction.SuppressedNotifications = ReactionNotificationSetting.Inherit;
                changed = true;
            }
        }
        return changed;
    }

    private static bool DeduplicateCommands(List<string> commands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < commands.Count; readIndex++)
        {
            var command = commands[readIndex];
            if (string.IsNullOrWhiteSpace(command) || !seen.Add(command))
                continue;
            commands[writeIndex++] = command;
        }

        if (writeIndex == commands.Count)
            return false;
        commands.RemoveRange(writeIndex, commands.Count - writeIndex);
        return true;
    }

    private static bool DeduplicateChannels(List<int> channels)
    {
        var seen = new HashSet<int>();
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < channels.Count; readIndex++)
        {
            if (!seen.Add(channels[readIndex]))
                continue;
            if (channels[readIndex] < ushort.MinValue || channels[readIndex] > ushort.MaxValue)
                continue;
            channels[writeIndex++] = channels[readIndex];
        }

        if (writeIndex == channels.Count)
            return false;
        channels.RemoveRange(writeIndex, channels.Count - writeIndex);
        return true;
    }

    private static bool ContainsCommand(List<string> commands, string command)
    {
        return commands.Exists(item => string.Equals(item, command, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NormalizeCustomChannels(List<ChannelSetting> channels)
    {
        var seen = new HashSet<int>();
        var writeIndex = 0;
        var changed = false;
        for (var readIndex = 0; readIndex < channels.Count; readIndex++)
        {
            var channel = channels[readIndex];
            if (channel.ChatType < ushort.MinValue || channel.ChatType > ushort.MaxValue ||
                !seen.Add(channel.ChatType))
            {
                changed = true;
                continue;
            }
            if (channel.Name == null)
            {
                channel.Name = string.Empty;
                changed = true;
            }
            channels[writeIndex++] = channel;
        }

        if (writeIndex == channels.Count)
            return changed;
        channels.RemoveRange(writeIndex, channels.Count - writeIndex);
        return true;
    }

    private static bool RemoveNullEntries<T>(List<T> items)
        where T : class
    {
        return items.RemoveAll(static item => item == null) > 0;
    }

    private static void Validate(Configuration configuration)
    {
        if (configuration.Version != ConfigVersion.CURRENT)
            throw new InvalidOperationException("Configuration migration did not reach the current version.");
        if (configuration.Reactions == null || configuration.EnabledChannels == null || configuration.CustomChannels == null)
            throw new InvalidOperationException("Configuration collections cannot be null.");
        if (configuration.DefaultCommandWhitelist == null || configuration.DefaultCommandBlacklist == null ||
            configuration.DefaultEnabledChannels == null)
            throw new InvalidOperationException("Configuration command defaults cannot be null.");
        foreach (var reaction in configuration.Reactions)
        {
            if (reaction.EnabledChannels == null || reaction.CommandWhitelist == null || reaction.CommandBlacklist == null)
                throw new InvalidOperationException("A migrated reaction contains a null collection.");
        }
    }
}

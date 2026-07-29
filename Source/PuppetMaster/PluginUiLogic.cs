using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PuppetMaster;

internal enum ReactionUiStatus
{
    Disabled,
    InvalidTrigger,
    NoChannels,
    Unsafe,
    Ready,
}

internal static class PluginUiLogic
{
    public static Dictionary<string, List<int>> GroupReactionIndexes(IReadOnlyList<Reaction> reactions)
    {
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < reactions.Count; index++)
        {
            if (!groups.TryGetValue(reactions[index].Name, out var indexes))
            {
                indexes = [];
                groups.Add(reactions[index].Name, indexes);
            }
            indexes.Add(index);
        }
        return groups;
    }

    public static List<int> FilterReactionIndexes(IReadOnlyList<Reaction> reactions, string search)
    {
        var indexes = new List<int>();
        for (var index = 0; index < reactions.Count; index++)
        {
            if (MatchesSearch(reactions[index], search))
                indexes.Add(index);
        }
        return indexes;
    }

    public static bool MatchesSearch(Reaction reaction, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var trigger = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
        return reaction.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               trigger.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    public static ReactionUiStatus GetStatus(Reaction reaction)
    {
        if (!reaction.Enabled)
            return ReactionUiStatus.Disabled;
        var pattern = ReactionCommandMatcher.SelectPattern(reaction);
        var trigger = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
        if (string.IsNullOrWhiteSpace(trigger) || pattern == null)
            return ReactionUiStatus.InvalidTrigger;
        if (reaction.EnabledChannels.Count == 0)
            return ReactionUiStatus.NoChannels;
        if (reaction.AllowAllCommands)
            return ReactionUiStatus.Unsafe;
        return ReactionUiStatus.Ready;
    }

    public static void SetRegexMode(Reaction reaction, bool useRegex)
    {
        reaction.UseRegex = useRegex;
    }

    public static int ClampCooldown(int seconds)
    {
        return Math.Clamp(seconds, 0, 86400);
    }

    public static void SetReactionEnabled(Reaction reaction, bool enabled, Action<Reaction>? cancel = null)
    {
        reaction.Enabled = enabled;
        if (!enabled)
            cancel?.Invoke(reaction);
    }

    public static void SetReactionGroupEnabled(
        IReadOnlyList<Reaction> reactions,
        IReadOnlyList<int> indexes,
        bool enabled,
        Action<Reaction>? cancel = null)
    {
        foreach (var index in indexes)
        {
            if (index >= 0 && index < reactions.Count)
                SetReactionEnabled(reactions[index], enabled, cancel);
        }
    }

    public static bool TryDeleteReaction(
        List<Reaction> reactions,
        int index,
        out int nextIndex,
        Action<Reaction>? cancel = null)
    {
        nextIndex = -1;
        if (reactions.Count <= 1 || index < 0 || index >= reactions.Count)
            return false;
        cancel?.Invoke(reactions[index]);
        reactions.RemoveAt(index);
        nextIndex = Math.Clamp(index, 0, reactions.Count - 1);
        return true;
    }

    public static string NormalizeCommand(string input)
    {
        input = input.Trim();
        if (input.Length == 0)
            return string.Empty;
        if (!input.StartsWith('/'))
            input = $"/{input}";
        input = input.Replace('[', '<').Replace(']', '>');
        var space = input.IndexOf(' ');
        return (space == -1 ? input : input[..space]).ToLowerInvariant();
    }

    public static bool ContainsCommand(IEnumerable<string> commands, string command)
    {
        return commands.Any(item => item.Equals(command, StringComparison.OrdinalIgnoreCase));
    }

    public static bool AddCommandRule(List<string> commands, List<string> oppositeCommands, string input)
    {
        var command = NormalizeCommand(input);
        if (command.Length == 0)
            return false;
        oppositeCommands.RemoveAll(item => item.Equals(command, StringComparison.OrdinalIgnoreCase));
        if (ContainsCommand(commands, command))
            return false;
        commands.Add(command);
        return true;
    }

    public static Reaction CloneReaction(Reaction source)
    {
        return new Reaction
        {
            Enabled = false,
            Name = $"{source.Name} Copy",
            TriggerPhrase = source.TriggerPhrase,
            AllowSit = source.AllowSit,
            MotionOnly = source.MotionOnly,
            CooldownSeconds = source.CooldownSeconds,
            ExecutionPolicy = source.ExecutionPolicy,
            AllowAllCommands = source.AllowAllCommands,
            UseRegex = source.UseRegex,
            CustomPhrase = source.CustomPhrase,
            ReplaceMatch = source.ReplaceMatch,
            TestInput = source.TestInput,
            EnabledChannels = new List<int>(source.EnabledChannels),
            CommandWhitelist = new List<string>(source.CommandWhitelist),
            CommandBlacklist = new List<string>(source.CommandBlacklist),
        };
    }

    public static Reaction CreateReactionFromLog(
        int chatTypeId,
        string triggerText,
        string channelName,
        Configuration configuration)
    {
        var reaction = Reaction.CreateDefault(
            $"Reaction from {channelName}",
            configuration.DefaultCommandWhitelist,
            configuration.DefaultCommandBlacklist,
            configuration.DefaultAllowAllCommands,
            configuration.DefaultMotionOnly,
            configuration.DefaultEnabledChannels);
        reaction.EnabledChannels.Clear();
        reaction.UseRegex = true;
        reaction.CustomPhrase = $"^{Regex.Escape(triggerText)}$";
        reaction.TestInput = triggerText;
        reaction.EnabledChannels.Add(chatTypeId);
        return reaction;
    }

    public static void SetChannel(List<int> selectedChannels, int chatTypeId, bool enabled)
    {
        if (enabled)
        {
            if (!selectedChannels.Contains(chatTypeId))
                selectedChannels.Add(chatTypeId);
        }
        else
        {
            selectedChannels.RemoveAll(id => id == chatTypeId);
        }
    }

    public static string? ValidateCustomChannelId(
        ChannelSetting channel,
        int channelId,
        IReadOnlyList<ChannelSetting> customChannels,
        Func<int, bool> isOfficial)
    {
        if (channelId < ushort.MinValue || channelId > ushort.MaxValue)
            return $"Channel ID must be between {ushort.MinValue} and {ushort.MaxValue}.";
        if (isOfficial(channelId))
            return "Official Dalamud channel IDs do not belong in Custom Channels.";
        if (customChannels.Any(candidate => !ReferenceEquals(candidate, channel) && candidate.ChatType == channelId))
            return "Another custom channel already uses this ID.";
        return null;
    }
}

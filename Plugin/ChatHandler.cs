using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PuppetMaster_Enhanced;

public class ChatHandler
{
    public static bool WhitelistPass(string ClearFromPlayer, string ClearFromWorld, out WhitelistedPlayer? foundWhitelistedPlayer)
    {
        foundWhitelistedPlayer = null;

        if (Configuration.Instance.EnableWhitelist && Configuration.Instance.WhitelistedPlayers.Count == 0)
            return false;

        foreach (var WhitelistedPlayer in Configuration.Instance.WhitelistedPlayers)
        {
            if (IsPlayerWhitelisted(ClearFromPlayer, ClearFromWorld, WhitelistedPlayer, out foundWhitelistedPlayer))
                return true;
        }

        if (!Configuration.Instance.EnableWhitelist)
            return true;

        return false;
    }

    private static bool IsPlayerWhitelisted(string clearFromPlayer, string clearFromWorld, WhitelistedPlayer whitelistedPlayer, out WhitelistedPlayer? foundWhitelistedPlayer)
    {
        foundWhitelistedPlayer = null;

        string clearFromWhitelistedPlayer = whitelistedPlayer.PlayerName.Trim().ToLower();
        string clearFromWhitelistedPlayerWorld = whitelistedPlayer.PlayerWorld.Trim().ToLower();

        if (
            clearFromPlayer != string.Empty && clearFromWhitelistedPlayer != string.Empty &&
            clearFromWorld != string.Empty && clearFromWhitelistedPlayerWorld != string.Empty &&
            whitelistedPlayer.Enabled && whitelistedPlayer.PlayerName != string.Empty && whitelistedPlayer.PlayerWorld != string.Empty
           )
        {
            bool playerNameMatch = whitelistedPlayer.StrictPlayerName ? clearFromPlayer == clearFromWhitelistedPlayer : CommonHelper.RegExpMatch(clearFromPlayer, whitelistedPlayer.PlayerName);
            bool playerWorldMatch = (clearFromWhitelistedPlayerWorld == "*") || (clearFromWorld == clearFromWhitelistedPlayerWorld);

            if (playerNameMatch && playerWorldMatch)
            {
                foundWhitelistedPlayer = whitelistedPlayer;
                return true;
            }
        }

        return false;
    }

    public static bool BlacklistPass(string ClearFromPlayer, string ClearFromWorld)
    {
        if (!Configuration.Instance.EnableBlacklist || Configuration.Instance.BlacklistedPlayers.Count == 0)
            return true;

        foreach (var blacklistedPlayer in Configuration.Instance.BlacklistedPlayers)
        {
            if (IsPlayerBlacklisted(ClearFromPlayer, ClearFromWorld, blacklistedPlayer))
                return false;
        }

        return true;
    }

    private static bool IsPlayerBlacklisted(string clearFromPlayer, string clearFromWorld, BlacklistedPlayer blacklistedPlayer)
    {
        string clearFromBlacklistedPlayer = blacklistedPlayer.PlayerName.Trim().ToLower();
        string clearFromBlacklistedPlayerWorld = blacklistedPlayer.PlayerWorld.Trim().ToLower();

        if (
            clearFromPlayer != string.Empty && clearFromBlacklistedPlayer != string.Empty &&
            clearFromWorld != string.Empty && clearFromBlacklistedPlayerWorld != string.Empty &&
            blacklistedPlayer.Enabled && blacklistedPlayer.PlayerName != string.Empty && blacklistedPlayer.PlayerWorld != string.Empty
           )
        {
            bool playerNameMatch = blacklistedPlayer.StrictPlayerName ? clearFromPlayer == clearFromBlacklistedPlayer : CommonHelper.RegExpMatch(clearFromPlayer, blacklistedPlayer.PlayerName);
            bool playerWorldMatch = (clearFromBlacklistedPlayerWorld == "*") || (clearFromWorld == clearFromBlacklistedPlayerWorld);

            if (playerNameMatch && playerWorldMatch)
                return true;
        }

        return false;
    }

    public static bool IsChannelEnabled(XivChatType type, List<ChannelSetting> Channels)
    {
        foreach (var enabledChannel in Channels)
        {
            if (enabledChannel.ChatType == type && enabledChannel.Enabled)
            {
                return true;
            }
        }

        return false;
    }

    public static void DoCommand(XivChatType type, string message, string sender, string sender_world)
    {
        string ClearFromPlayer = sender.Trim().ToLower();
        string ClearFromWorld = sender_world.Trim().ToLower();

        if (ClearFromPlayer == String.Empty || !Configuration.Instance.EnablePlugin || !BlacklistPass(ClearFromPlayer, ClearFromWorld) || !WhitelistPass(ClearFromPlayer, ClearFromWorld, out WhitelistedPlayer? foundWhitelistedPlayer))
            return;

        bool useAllDefaultSettings = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseAllDefaultSettings;
        bool useDefaultTrigger = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultTrigger;
        bool useDefaultRequests = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultRequests;
        bool useDefaultEnabledChannels = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultEnabledChannels;

        if (!IsChannelEnabled(type, (useAllDefaultSettings || useDefaultEnabledChannels) ? Configuration.Instance.DefaultEnabledChannels : foundWhitelistedPlayer.EnabledChannels))
            return;

        bool flag1 = (!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.UseRegex && foundWhitelistedPlayer.CustomRx != null : Configuration.Instance.DefaultUseRegex && Service.CustomRx != null;
        MatchCollection matchCollection = flag1 ? ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.CustomRx.Matches(message) : Service.CustomRx.Matches(message)) : ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.Rx.Matches(message) : Service.Rx.Matches(message));

        if (matchCollection.Count == 0)
        {
            return;
        }

        string command = string.Empty;

        try
        {
            command = flag1 ? ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.CustomRx.Replace(matchCollection[0].Value, foundWhitelistedPlayer.ReplaceMatch) : Service.CustomRx.Replace(matchCollection[0].Value, Configuration.Instance.DefaultReplaceMatch)) : ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.Rx.Replace(matchCollection[0].Value, foundWhitelistedPlayer.GetDefaultReplaceMatch()) : Service.Rx.Replace(matchCollection[0].Value, Service.GetDefaultReplaceMatch()));
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(ex, "[PuppetMaster] [Error] Regex error while listening for command");
        }

        Service.ParsedTextCommand parsedTextCommand = Service.FormatCommand(command);

        if (string.IsNullOrEmpty(parsedTextCommand.Main))
        {
            return;
        }

        var foundEmote = EmoteHelper.GetEmoteByCommand(parsedTextCommand.Main);
        bool flag2 = foundEmote != null;

        if (flag2)
        {
            if ((parsedTextCommand.Main == "/sit" || parsedTextCommand.Main == "/groundsit" || parsedTextCommand.Main == "/lounge") && ((!useAllDefaultSettings && !useDefaultRequests) ? !foundWhitelistedPlayer.AllowSit : !Configuration.Instance.DefaultAllowSit))
            {
                parsedTextCommand.Main = "/no";
            }

            if ((!useAllDefaultSettings && !useDefaultRequests) ? foundWhitelistedPlayer.MotionOnly : Configuration.Instance.DefaultMotionOnly)
            {
                parsedTextCommand.Args = "motion";
            }
        }

        if ((!useAllDefaultSettings && !useDefaultRequests) ? !(foundWhitelistedPlayer.AllowAllCommands | flag2) : !(Configuration.Instance.DefaultAllowAllCommands | flag2))
        {
            return;
        }

        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted(parsedTextCommand);
        ChatHelper.SendMessage(interpolatedStringHandler.ToStringAndClear());
    }

    public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var senderResolved = SeStringHelper.ResolveSender(sender);

        if (senderResolved != null)
            DoCommand(type, message.ToString(), senderResolved.PlayerName, senderResolved.Homeworld);
    }
}

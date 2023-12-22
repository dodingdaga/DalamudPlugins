using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;

#nullable enable
namespace PuppetMaster
{
    public class ChatHandler
    {
        public static bool WhitelistPass(string ClearFromPlayer, out WhitelistedPlayer foundWhitelistedPlayer)
        {
            foundWhitelistedPlayer = null;

            if (!Service.configuration.EnableWhitelist)
                return true;

            if (Service.configuration.WhitelistedPlayers.Count == 0)
                return false;

            foreach (var WhitelistedPlayer in Service.configuration.WhitelistedPlayers)
            {
                if (IsPlayerWhitelisted(ClearFromPlayer, WhitelistedPlayer, out foundWhitelistedPlayer))
                    return true;
            }

            return false;
        }

        private static bool IsPlayerWhitelisted(string clearFromPlayer, WhitelistedPlayer whitelistedPlayer, out WhitelistedPlayer foundWhitelistedPlayer)
        {
            foundWhitelistedPlayer = null;

            if (clearFromPlayer != string.Empty && whitelistedPlayer.Enabled && whitelistedPlayer.PlayerName != string.Empty)
            {
                bool flagWhitelist = CommonHelper.RegExpMatch(clearFromPlayer, whitelistedPlayer.PlayerName);

                if (flagWhitelist)
                {
                    foundWhitelistedPlayer = whitelistedPlayer;
                    return true;
                }
            }

            return false;
        }

        public static bool BlacklistPass(string ClearFromPlayer)
        {
            if (!Service.configuration.EnableBlacklist || Service.configuration.BlacklistedPlayers.Count == 0)
                return true;

            foreach (var blacklistedPlayer in Service.configuration.BlacklistedPlayers)
            {
                if (IsPlayerBlacklisted(ClearFromPlayer, blacklistedPlayer))
                    return false;
            }

            return true;
        }

        private static bool IsPlayerBlacklisted(string clearFromPlayer, BlacklistedPlayer blacklistedPlayer)
        {
            if (clearFromPlayer != string.Empty && blacklistedPlayer.Enabled && blacklistedPlayer.PlayerName != string.Empty)
            {
                bool playerNameMatch = CommonHelper.RegExpMatch(clearFromPlayer, blacklistedPlayer.PlayerName);

                if (playerNameMatch)
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

        public static void DoCommand(XivChatType type, string message, string sender)
        {
            string ClearFromPlayer = sender.Trim().ToLower();

            if (!Service.configuration.EnablePlugin || !BlacklistPass(ClearFromPlayer) || !WhitelistPass(ClearFromPlayer, out WhitelistedPlayer? foundWhitelistedPlayer))
                return;

            bool useAllDefaultSettings = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseAllDefaultSettings;
            bool useDefaultTrigger = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultTrigger;
            bool useDefaultRequests = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultRequests;
            bool useDefaultEnabledChannels = (foundWhitelistedPlayer == null) || foundWhitelistedPlayer.UseDefaultEnabledChannels;

            if (!IsChannelEnabled(type, (useAllDefaultSettings || useDefaultEnabledChannels) ? Service.configuration.DefaultEnabledChannels : foundWhitelistedPlayer.EnabledChannels))
                return;

            bool flag1 = (!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.UseRegex && foundWhitelistedPlayer.CustomRx != null : Service.configuration.DefaultUseRegex && Service.CustomRx != null;
            MatchCollection matchCollection = flag1 ? ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.CustomRx.Matches(message) : Service.CustomRx.Matches(message)) : ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.Rx.Matches(message) : Service.Rx.Matches(message));

            if (matchCollection.Count == 0)
            {
                return;
            }

            string command = string.Empty;

            try
            {
                command = flag1 ? ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.CustomRx.Replace(matchCollection[0].Value, foundWhitelistedPlayer.ReplaceMatch) : Service.CustomRx.Replace(matchCollection[0].Value, Service.configuration.DefaultReplaceMatch)) : ((!useAllDefaultSettings && !useDefaultTrigger) ? foundWhitelistedPlayer.Rx.Replace(matchCollection[0].Value, foundWhitelistedPlayer.GetDefaultReplaceMatch()) : Service.Rx.Replace(matchCollection[0].Value, Service.GetDefaultReplaceMatch()));
            }
            catch (Exception ex)
            {
            }

            Service.ParsedTextCommand parsedTextCommand = Service.FormatCommand(command);

            if (string.IsNullOrEmpty(parsedTextCommand.Main))
            {
                return;
            }

            bool flag2 = Service.Emotes.Contains(parsedTextCommand.Main);

            if (flag2)
            {
                if ((parsedTextCommand.Main == "/sit" || parsedTextCommand.Main == "/groundsit" || parsedTextCommand.Main == "/lounge") && ((!useAllDefaultSettings && !useDefaultRequests) ? !foundWhitelistedPlayer.AllowSit : !Service.configuration.DefaultAllowSit))
                {
                    parsedTextCommand.Main = "/no";
                }

                if ((!useAllDefaultSettings && !useDefaultRequests) ? foundWhitelistedPlayer.MotionOnly : Service.configuration.DefaultMotionOnly)
                {
                    parsedTextCommand.Args = "motion";
                }
            }

            if ((!useAllDefaultSettings && !useDefaultRequests) ? !(foundWhitelistedPlayer.AllowAllCommands | flag2) : !(Service.configuration.DefaultAllowAllCommands | flag2))
            {
                return;
            }

            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
            interpolatedStringHandler.AppendFormatted<Service.ParsedTextCommand>(parsedTextCommand);
            Chat.SendMessage(interpolatedStringHandler.ToStringAndClear());
        }

        public static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message,ref bool isHandled)
        {
            if (isHandled)
            {
                return;
            }

            ChatHandler.DoCommand(type, message.ToString(), sender.ToString());
        }
    }
}

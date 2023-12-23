using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Lumina;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;

#nullable enable
namespace PuppetMaster
{
    public class ChatHandler
    {
        public static bool WhitelistPass(string ClearFromPlayer, out WhitelistedPlayer? foundWhitelistedPlayer)
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

        private static bool IsPlayerWhitelisted(string clearFromPlayer, WhitelistedPlayer whitelistedPlayer, out WhitelistedPlayer? foundWhitelistedPlayer)
        {
            foundWhitelistedPlayer = null;

            string clearFromWhitelistedPlayer = whitelistedPlayer.PlayerName.Trim().ToLower();

            if (clearFromPlayer != string.Empty && clearFromWhitelistedPlayer != string.Empty && whitelistedPlayer.Enabled && whitelistedPlayer.PlayerName != string.Empty)
            {
                bool flagWhitelist = whitelistedPlayer.StrictPlayerName ? clearFromPlayer == clearFromWhitelistedPlayer : CommonHelper.RegExpMatch(clearFromPlayer, whitelistedPlayer.PlayerName);

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
            string clearFromBlacklistedPlayer = blacklistedPlayer.PlayerName.Trim().ToLower();

            if (clearFromPlayer != string.Empty && clearFromBlacklistedPlayer != String.Empty && blacklistedPlayer.Enabled && blacklistedPlayer.PlayerName != string.Empty)
            {
                bool playerNameMatch = blacklistedPlayer.StrictPlayerName ? clearFromPlayer == clearFromBlacklistedPlayer : CommonHelper.RegExpMatch(clearFromPlayer, blacklistedPlayer.PlayerName);

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

            if (ClearFromPlayer == String.Empty || !Service.configuration.EnablePlugin || !BlacklistPass(ClearFromPlayer) || !WhitelistPass(ClearFromPlayer, out WhitelistedPlayer? foundWhitelistedPlayer))
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
                Service.Logger.Error("[PuppetMaster] [Error] Regex error while listening for command");
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

        public static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (isHandled)
                return;

            string? player_name = GetRealPlayerNameFromSenderPayloads(sender.Payloads);

            if (player_name == null)
                return;

            ChatHandler.DoCommand(type, message.ToString(), player_name);
        }

        public static string? GetRealPlayerNameFromSenderPayloads(List<Payload> payloads)
        {
            if (payloads.Count == 0)
                return null;

            var foundPlayerPayload = payloads.FirstOrDefault(payload => payload.Type == PayloadType.Player);

            if (foundPlayerPayload != null)
            {
                PlayerPayload? playerPayload = foundPlayerPayload as PlayerPayload;

                if (playerPayload == null)
                    return null;

                return playerPayload.PlayerName;
            } else
            {
                var foundRawTextPayloads = payloads.Where(payload => payload.Type == PayloadType.RawText);

                if (foundRawTextPayloads.Count() == 0)
                    return null;

                foreach (var foundTextPayload in foundRawTextPayloads)
                {
                    TextPayload? textPayload = foundTextPayload as TextPayload;

                    if (textPayload != null)
                    {
                        string possiblePlayerName = textPayload.Text;

                        if (possiblePlayerName.Split(' ').Count() == 2)
                        {
                            return possiblePlayerName;
                        }
                    }
                }

                return null;
            }
        }

        public class SenderObject
        {
            public string? Id { get; set; }
            public List<Payload>? Payloads { get; set; }
            public string? TextValue { get; set; }
        }
    }
}

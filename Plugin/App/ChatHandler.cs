using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

#nullable enable
namespace PuppetMaster
{
    public class ChatHandler
    {
        public static bool WhitelistPass(string ClearFromPlayer, string ClearFromWorld, out WhitelistedPlayer? foundWhitelistedPlayer)
        {
            foundWhitelistedPlayer = null;

            if (Service.configuration.EnableWhitelist && Service.configuration.WhitelistedPlayers.Count == 0)
                return false;

            foreach (var WhitelistedPlayer in Service.configuration.WhitelistedPlayers)
            {
                if (IsPlayerWhitelisted(ClearFromPlayer, ClearFromWorld, WhitelistedPlayer, out foundWhitelistedPlayer))
                    return true;
            }

            if (!Service.configuration.EnableWhitelist)
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
            if (!Service.configuration.EnableBlacklist || Service.configuration.BlacklistedPlayers.Count == 0)
                return true;

            foreach (var blacklistedPlayer in Service.configuration.BlacklistedPlayers)
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

        public static void DoCommand(XivChatType type, string message, string sender, World sender_world)
        {
            string ClearFromPlayer = sender.Trim().ToLower();
            string ClearFromWorld = sender_world.Name.ToString().Trim().ToLower();

            if (ClearFromPlayer == String.Empty || !Service.configuration.EnablePlugin || !BlacklistPass(ClearFromPlayer, ClearFromWorld) || !WhitelistPass(ClearFromPlayer, ClearFromWorld, out WhitelistedPlayer? foundWhitelistedPlayer))
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

        public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (isHandled)
                return;

            // Service.Logger.Info("[PUPPETMASTER] Player payload : ");
            // Service.Logger.Info(sender.ToJson());

            object? player_data = GetRealPlayerNameFromSenderPayloads(sender.Payloads);

            string? player_name;
            World player_world;

            if (player_data is Array dataArray && dataArray.Length >= 3)
            {
                var payloadtype = (string?)dataArray.GetValue(0);

                if (payloadtype == "payload")
                {
                    player_name = (string?)dataArray.GetValue(1);
                    player_world = (World)dataArray.GetValue(2);
                } else
                {
                    return;
                }
            }
            else
            {
                // Service.Logger.Info("[PUPPETMASTER] Error parsing player payload, not an array.");
                return;
            }

            // Service.Logger.Info("[PUPPETMASTER] Found player : " + (player_name ?? "no player found"));

            if (player_name == null)
                return;

            ChatHandler.DoCommand(type, message.ToString(), player_name, player_world);
        }

        public static object? GetRealPlayerNameFromSenderPayloads(List<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                // Service.Logger.Info("[PUPPETMASTER] No Payloads");
                return null;
            }

            var foundPlayerPayload = payloads.FirstOrDefault(payload => payload.Type == PayloadType.Player);

            if (foundPlayerPayload != null)
            {
                PlayerPayload? playerPayload = foundPlayerPayload as PlayerPayload;

                if (playerPayload == null)
                {
                    return null;
                }

                object[] playerDataArray = new object[3];

                playerDataArray[0] = "payload";
                playerDataArray[1] = playerPayload.PlayerName;
                playerDataArray[2] = playerPayload.World.Value;

                return playerDataArray;
            } else
            {
                var foundRawTextPayloads = payloads.Where(payload => payload.Type == PayloadType.RawText);

                if (foundRawTextPayloads.Count() == 0)
                {
                    // Service.Logger.Info("[PUPPETMASTER] No raw text payload found");
                    return null;
                }

                foreach (var foundTextPayload in foundRawTextPayloads)
                {
                    TextPayload? textPayload = foundTextPayload as TextPayload;

                    if (textPayload != null)
                    {
                        string? possiblePlayerName = textPayload.Text;

                        // Service.Logger.Info("[PUPPETMASTER] Found raw text payload : " + possiblePlayerName);

                        if (possiblePlayerName == null || possiblePlayerName.Split(' ').Count() != 2)
                        {
                            continue;
                        }

                        object[] playerDataArray = new object[3];

                        playerDataArray[0] = "rawtext";
                        playerDataArray[1] = possiblePlayerName;
                        playerDataArray[2] = null;

                        return playerDataArray;
                    } else
                    {
                        // Service.Logger.Info("[PUPPETMASTER] Raw text payload error");
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

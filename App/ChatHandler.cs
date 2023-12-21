using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

#nullable enable
namespace PuppetMaster
{
    public class ChatHandler
    {
        public static void DoCommand(XivChatType type, string message, string sender)
        {

            if (!Service.configuration.EnablePlugin)
            {
                return;
            }

            string ClearFromPlayer = sender.Trim().ToLower();

            if (Service.configuration.EnableBlacklist && Service.configuration.BlacklistedPlayers.Count != 0)
            {
                foreach (var BlacklistedPlayer in Service.configuration.BlacklistedPlayers)
                {
                    if (ClearFromPlayer != String.Empty && BlacklistedPlayer.Enabled && BlacklistedPlayer.PlayerName != String.Empty)
                    {
                        bool flag_blacklist = CommonHelper.RegExpMatch(ClearFromPlayer, BlacklistedPlayer.PlayerName);

                        if (flag_blacklist)
                        {
                            //PluginLog.LogInformation("BLACKLISTED");
                            return; // The sender is blacklisted
                        }
                    }
                }
            }

            if (Service.configuration.EnableWhitelist)
            {
                if (Service.configuration.WhitelistedPlayers.Count == 0)
                {
                    //PluginLog.LogInformation("Whitelist empty, skipping command");
                    return;
                }

                bool flag_whitelist_pass = false;
                WhitelistedPlayer foundWhitelistedPlayer = null;

                foreach (var WhitelistedPlayer in Service.configuration.WhitelistedPlayers)
                {
                    if (ClearFromPlayer != String.Empty && WhitelistedPlayer.Enabled && WhitelistedPlayer.PlayerName != String.Empty)
                    {
                        bool flag_whitelist = CommonHelper.RegExpMatch(ClearFromPlayer, WhitelistedPlayer.PlayerName);

                        if (flag_whitelist)
                        {
                            //PluginLog.LogInformation("WHITELISTED");
                            flag_whitelist_pass = true;
                            foundWhitelistedPlayer = WhitelistedPlayer;
                            break;
                        }
                    }
                }

                if (!flag_whitelist_pass)
                {
                    //PluginLog.LogInformation("Player not found in whitelist. Skipping command.");
                    return;
                }

                bool useAllDefaultSettings = foundWhitelistedPlayer.UseAllDefaultSettings;
                bool useDefaultTrigger = foundWhitelistedPlayer.UseDefaultTrigger;
                bool useDefaultRequests = foundWhitelistedPlayer.UseDefaultRequests;
                bool useDefaultEnabledChannels = foundWhitelistedPlayer.UseDefaultEnabledChannels;

                if (useAllDefaultSettings || useDefaultEnabledChannels)
                {
                    bool flag_enabled_channel_default = false;

                    foreach (var enabledChannel in Service.configuration.DefaultEnabledChannels)
                    {
                        if (enabledChannel.ChatType == type && enabledChannel.Enabled)
                        {
                            flag_enabled_channel_default = true;
                            break;
                        }
                    }

                    if (!flag_enabled_channel_default)
                    {
                        return;
                    }
                } else
                {
                    bool flag_enabled_channel = false;

                    foreach (var enabledChannel in foundWhitelistedPlayer.EnabledChannels)
                    {
                        if (enabledChannel.ChatType == type && enabledChannel.Enabled)
                        {
                            flag_enabled_channel = true;
                            break;
                        }
                    }

                    if (!flag_enabled_channel)
                    {
                        return;
                    }
                }

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
            } else {
                bool flag_enabled_channel_default = false;

                foreach (var enabledChannel in Service.configuration.DefaultEnabledChannels)
                {
                    if (enabledChannel.ChatType == type && enabledChannel.Enabled)
                    {
                        flag_enabled_channel_default = true;
                        break;
                    }
                }

                if (!flag_enabled_channel_default)
                {
                    return;
                }

                bool flag1 = Service.configuration.DefaultUseRegex && Service.CustomRx != null;
                MatchCollection matchCollection = flag1 ? Service.CustomRx.Matches(message) : Service.Rx.Matches(message);

                if (matchCollection.Count == 0)
                {
                    return;
                }

                string command = string.Empty;

                try
                {
                    command = flag1 ? Service.CustomRx.Replace(matchCollection[0].Value, Service.configuration.DefaultReplaceMatch) : Service.Rx.Replace(matchCollection[0].Value, Service.GetDefaultReplaceMatch());
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
                    if ((parsedTextCommand.Main == "/sit" || parsedTextCommand.Main == "/groundsit" || parsedTextCommand.Main == "/lounge") && !Service.configuration.DefaultAllowSit)
                    {
                        parsedTextCommand.Main = "/no";
                    }

                    if (Service.configuration.DefaultMotionOnly)
                    {
                        parsedTextCommand.Args = "motion";
                    }
                }

                if (!(Service.configuration.DefaultAllowAllCommands | flag2))
                    return;
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
                interpolatedStringHandler.AppendFormatted<Service.ParsedTextCommand>(parsedTextCommand);
                Chat.SendMessage(interpolatedStringHandler.ToStringAndClear());
            }
        }

        public static void OnChatMessage(
          XivChatType type,
          uint senderId,
          ref SeString sender,
          ref SeString message,
          ref bool isHandled)
        {
            if (isHandled)
                return;
            ChatHandler.DoCommand(type, message.ToString(), sender.ToString());
        }
    }
}

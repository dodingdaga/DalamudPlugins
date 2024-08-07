using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    public class ChatHandler
    {
        public ChatHandler()
        {
        }

        public static void DoCommand(XivChatType type, String message)
        {
            // Check if part of enabled channels
            if (!Service.enabledChannels.Contains(type)) return;

            // Find command in message
            bool usingRegex = (Service.configuration!.UseRegex && Service.CustomRx != null);
            MatchCollection matches = usingRegex ? Service.CustomRx!.Matches(message) : Service.Rx!.Matches(message);
            if (matches.Count == 0) return;
            String command = String.Empty;
            try
            {
                command = usingRegex ?
                    Service.CustomRx!.Replace(matches[0].Value, Service.configuration.ReplaceMatch) :
                    Service.Rx!.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());
            } catch (Exception) { }
            Service.ParsedTextCommand textCommand = Service.FormatCommand(command);
            if (String.IsNullOrEmpty(textCommand.Main)) return;

            // Process emote
            bool isEmote = Service.Emotes.Contains(textCommand.Main);
            if (isEmote)
            {
                if ((textCommand.Main == "/sit" || textCommand.Main == "/groundsit" || textCommand.Main == "/lounge") && !Service.configuration.AllowSit)
                    textCommand.Main = "/no";
                if (Service.configuration.MotionOnly)
                    textCommand.Args = "motion";
            }

            // Execute command
            if (Service.configuration.AllowAllCommands || isEmote)
                Chat.SendMessage($"{textCommand}");
        }

#pragma warning disable IDE0060
        public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (isHandled) return;
            DoCommand(type, message.ToString());
        }
#pragma warning restore IDE0060
    }
}

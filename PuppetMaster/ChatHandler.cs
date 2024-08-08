using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

using System;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    public partial class ChatHandler
    {
        public ChatHandler()
        {
        }

        public static void DoCommand(int index, XivChatType type, String message)
        {
            if (Service.configuration == null) return;

            // Check if part of enabled channels
            if (!Service.configuration.Reactions[index].EnabledChannels.Contains((int)type)) return;

            // Find command in message
            var usingRegex = (Service.configuration.Reactions[index].UseRegex && Service.configuration.Reactions[index].CustomRx != null);
            var matches = usingRegex ? Service.configuration.Reactions[index].CustomRx!.Matches(message) : Service.configuration.Reactions[index].Rx!.Matches(message);
            if (matches.Count == 0) return;
            var command = string.Empty;
            try
            {
                command = usingRegex ?
                    Service.configuration.Reactions[index].CustomRx!.Replace(matches[0].Value, Service.configuration.Reactions[index].ReplaceMatch) :
                    Service.configuration.Reactions[index].Rx!.Replace(matches[0].Value, Service.GetDefaultReplaceMatch());
            } catch (Exception) { }


            var lines = MyRegex().Split(command.ToString());
            foreach (var line in lines)
            {
                var textCommand = Service.FormatCommand(line);
                if (!string.IsNullOrEmpty(textCommand.Main))
                {
                    // Process emote
                    var isEmote = Service.Emotes.Contains(textCommand.Main);
                    if (isEmote)
                    {
                        if ((textCommand.Main == "/sit" || textCommand.Main == "/groundsit" || textCommand.Main == "/lounge") && !Service.configuration.Reactions[index].AllowSit)
                            textCommand.Main = "/no";
                        if (Service.configuration.Reactions[index].MotionOnly)
                            textCommand.Args = "motion";
                    }

                    // Execute command
                    if (Service.configuration.Reactions[index].AllowAllCommands || isEmote)
                    {
                        Chat.SendMessage($"{textCommand}");
                    }
                }
            }
        }

        public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (Service.configuration == null) return;

            if (Service.configuration.DebugLogTypes && type != XivChatType.Debug)
            {
                var prefix = int.TryParse(type.ToString(), out var number)?"[" + number + "]":"[" + ((int)type) + "][" + type + "]";
                prefix += (sender.ToString().IsNullOrEmpty() ? "" : "<" + sender + "> ");
                Service.ChatGui.Print(prefix+" "+message);
            }

            if (isHandled) return;

            for (var index = 0; index < Service.configuration.Reactions.Count; index++)
            {
                if (Service.configuration.Reactions[index].Enabled)
                    DoCommand(index, type, message.ToString());
            }
        }

        [GeneratedRegex("\r\n|\r|\n")]
        private static partial Regex MyRegex();
    }
}

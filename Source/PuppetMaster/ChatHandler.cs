using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PuppetMaster
{
    public partial class ChatHandler
    {
        public ChatHandler()
        {
        }

        public static async Task RunMacroAsync(string[] lines, int index)
        {
            await Task.Run(() =>
            {
                Service.semaphore.WaitOne();
                var reaction = Service.configuration!.Reactions[index];
                Service.semaphore.Release();

                foreach (var line in lines)
                {
                    var textCommand = Service.FormatCommand(line);
                    if (!string.IsNullOrEmpty(textCommand.Main))
                    {
                        // Process emote
                        var isEmote = Service.Emotes.Contains(textCommand.Main);
                        if (isEmote)
                        {
                            if ((textCommand.Main == "/sit" || textCommand.Main == "/groundsit" || textCommand.Main == "/lounge") && reaction.AllowSit)
                                textCommand.Main = "/no";
                            if (reaction.MotionOnly)
                                textCommand.Args = "motion";
                        }

                        if (!reaction.CommandBlacklist.Contains(textCommand.Main))
                        {
                            // Execute command
                            if (reaction.AllowAllCommands || isEmote || reaction.CommandWhitelist.Contains(textCommand.Main))
                            {
                                if (textCommand.Main == "/wait" && float.TryParse(textCommand.Args, out var seconds))
                                    Thread.Sleep((int)(Math.Clamp(seconds, 0.0, 60.0) * 1000.0));
                                else
                                    Chat.SendMessage($"{textCommand}");
                            }
                        }
#if DEBUG
                        else
                        {
                            Service.ChatGui.Print($"{textCommand.Main} in CommandBlacklist");
                            return;
                        }
#endif
                    }
                }
            });
        }

        public static async void DoCommand(int index, XivChatType type, String message)
        {
            // Check if part of enabled channels
            if (!Service.configuration!.Reactions[index].EnabledChannels.Contains((int)type)) return;

            var usingRegex = (Service.configuration.Reactions[index].UseRegex && Service.configuration.Reactions[index].CustomRx != null);

            // Guard against whitespace regex
            if ((usingRegex && Service.configuration.Reactions[index].CustomRx!.ToString().IsNullOrWhitespace()) ||
                (!usingRegex && Service.configuration.Reactions[index].Rx!.ToString().IsNullOrWhitespace()))
            {
#if DEBUG
                Service.ChatGui.PrintError($"[PuppetMasster][ERR] Empty RegEx [{message}]");
#endif
                return;
            }

            // Find command in message
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
            var task = RunMacroAsync(lines, index);
            await task;
        }

        public static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (Service.configuration!.DebugLogTypes && type != XivChatType.Debug)
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

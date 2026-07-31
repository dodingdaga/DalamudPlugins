using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;

using ECommons;

namespace PuppetMaster
{
    public class Plugin : IDalamudPlugin
    {
        public static String Name => "PuppetMaster";
        private const String CommandName = "/puppetmaster";
        public WindowSystem windowSystem = new("PuppetMaster");
        public ConfigWindow configWindow;
        internal ReactionVisualizerWindow visualizerWindow;
        internal MessageLogWindow messageLogWindow;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            // Service
            pluginInterface.Create<Service>();
            Service.plugin = this;
            
            // Configuration
            Service.InitializeConfig();

            this.configWindow = new ConfigWindow();
            this.visualizerWindow = new ReactionVisualizerWindow();
            this.messageLogWindow = new MessageLogWindow();
            windowSystem.AddWindow(configWindow);
            windowSystem.AddWindow(visualizerWindow);
            windowSystem.AddWindow(messageLogWindow);

            // Handlers
            ChatHandler.Initialize();
            Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = @"Open settings dialog
/puppetmaster on|off - enable or disable all reactions
/puppetmaster on|off <ReactionName> - enable or disable reactions by name
/puppetmaster logging on|off - enable or disable message logging
/puppetmaster logging clear - clear captured logs and overload counters
/puppetmaster logging save - save captured logs to a timestamped file
/puppetmaster viz - open the read-only reaction visualizer"
            });
            Service.ChatGui.ChatMessage += ChatHandler.OnChatMessage;
            Service.PluginInterface.UiBuilder.Draw += DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;

            // Excel sheets
            Service.InitializeEmotes();

            // ECommons
            ECommonsMain.Init(pluginInterface, this, Module.All);
        }

        public void Dispose()
        {
            windowSystem.RemoveAllWindows();
            Service.ChatGui.ChatMessage -= ChatHandler.OnChatMessage;
            ChatHandler.Shutdown();
            Service.CommandManager.RemoveHandler(CommandName);
            GC.SuppressFinalize(this);

            ECommonsMain.Dispose();
        }

        private void OnCommand(String command, String args)
        {
            if (string.IsNullOrEmpty(args))
                DrawConfigUI();
            else
            {
                var ptc = Service.FormatCommand($"/{args}");
#if DEBUG
                Service.ChatGui.Print($"[PuppetMaster][Debug] PARSED TEXT COMMAND: {ptc}");
#endif
                void enableReactions(bool enable)
                {
                    if (string.IsNullOrEmpty(ptc.Args))
                        Service.SetEnabledAll(enable);
                    else
                        Service.SetEnabled(ptc.Args, enable);
                }
                if (ptc.Main.Equals("/on"))
                {
                    enableReactions(true);
                }
                else if (ptc.Main.Equals("/off"))
                {
                    enableReactions(false);
                }
                else if (ptc.Main.Equals("/logging"))
                {
                    HandleLoggingCommand(ptc.Args);
                }
                else if (ptc.Main.Equals("/viz") || ptc.Main.Equals("/visualizer"))
                {
                    this.visualizerWindow.IsOpen = true;
                }
            }
        }

        private static void HandleLoggingCommand(string args)
        {
            switch (args.Trim().ToLowerInvariant())
            {
                case "on":
                    Service.configuration!.DebugLogTypes = true;
                    Service.ChatGui.Print("[PuppetMaster] Message logging enabled for this session.");
                    break;
                case "off":
                    Service.configuration!.DebugLogTypes = false;
                    Service.ChatGui.Print("[PuppetMaster] Message logging disabled.");
                    break;
                case "clear":
                    var clearedCount = DebugLogBuffer.Snapshot().Length;
                    DebugLogBuffer.Clear();
                    ChatHandler.ResetDroppedMessageCount();
                    Service.ChatGui.Print($"[PuppetMaster] Cleared {clearedCount} captured log entr{(clearedCount == 1 ? "y" : "ies")} and overload counters.");
                    break;
                case "save":
                    try
                    {
                        var export = Service.SaveDebugLogs();
                        Service.ChatGui.Print($"[PuppetMaster] Saved {export.EntryCount} log entries to: {export.Path}");
                    }
                    catch (Exception exception)
                    {
                        Service.PluginLog.Error(exception, "Failed to save PuppetMaster message logs from command.");
                        Service.ChatGui.PrintError($"[PuppetMaster] Failed to save logs: {exception.Message}");
                    }
                    break;
                default:
                    Service.ChatGui.Print(
                        $"[PuppetMaster] Logging is {(Service.configuration!.DebugLogTypes ? "enabled" : "disabled")}. " +
                        "Use /puppetmaster logging on|off|clear|save.");
                    break;
            }
        }

        private void DrawUI()
        {
            this.windowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            this.configWindow.IsOpen = true;
            ConfigWindow.PreloadTestResult();
        }

        internal void DrawVisualizerUI()
        {
            this.visualizerWindow.IsOpen = true;
        }

        internal void DrawLogsUI()
        {
            this.messageLogWindow.IsOpen = true;
        }
    }
}

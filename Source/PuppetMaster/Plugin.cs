using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

using System;

namespace PuppetMaster
{
    public class Plugin : IDalamudPlugin
    {
        public static String Name => "PuppetMaster";
        private const String CommandName = "/puppetmaster";
        public WindowSystem windowSystem = new("PuppetMaster");
        public ConfigWindow configWindow;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            // Service
            pluginInterface.Create<Service>();
            Service.plugin = this;
            
            // Configuration
            Service.InitializeConfig();

            this.configWindow = new ConfigWindow();
            windowSystem.AddWindow(configWindow);

            // Handlers
            Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = @"Open settings dialog
/puppetmaster on|off - enable or disable all reactions
/puppetmaster on|off <ReactionName> - enable or disable reactions by name"
            });
            Service.ChatGui.ChatMessage += ChatHandler.OnChatMessage;
            Service.PluginInterface.UiBuilder.Draw += DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;

            // Excel sheets
            Service.InitializeEmotes();
        }

        public void Dispose()
        {
            windowSystem.RemoveAllWindows();
            Service.ChatGui.ChatMessage -= ChatHandler.OnChatMessage;
            Service.CommandManager.RemoveHandler(CommandName);
            GC.SuppressFinalize(this);
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
    }
}

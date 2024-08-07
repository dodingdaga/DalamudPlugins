using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;

namespace PuppetMaster
{
    public class Plugin : IDalamudPlugin
    {
        public static String Name => "PuppetMaster";
        private const String commandName = "/puppetmaster";
        public WindowSystem windowSystem = new("PuppetMaster");
        public ConfigWindow configWindow = new();

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            // Service
            pluginInterface.Create<Service>();
            Service.plugin = this;
            //Service.commonBase = new XivCommonBase();
            
            // Configuration
            Service.InitializeConfig();
            windowSystem.AddWindow(configWindow);

            // Handlers
            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Puppet Master settings"
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
            //Service.commonBase?.Dispose();
            Service.ChatGui.ChatMessage -= ChatHandler.OnChatMessage;
            Service.CommandManager.RemoveHandler(commandName);
            GC.SuppressFinalize(this);
        }

        private void OnCommand(String command, String args)
        {
            // in response to the slash command, just display our main ui
            DrawConfigUI();
        }

        private void DrawUI()
        {
            this.windowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            this.configWindow.IsOpen = true;
        }
    }
}

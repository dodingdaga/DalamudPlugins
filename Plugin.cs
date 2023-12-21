using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

#nullable enable
namespace PuppetMaster
{
    public sealed class Plugin : IDalamudPlugin
    {
        private readonly List<Tuple<string, string>> commandNames = new()
        {
            new Tuple<string, string>("/puppetmaster", "Open PuppetMaster settings"),
            new Tuple < string, string >("/ppm", "Alias for puppetmaster settings"),
            new Tuple < string, string >("/puppet", "Alias for puppetmaster settings")
        };

        public WindowSystem windowSystem = new("PuppetMaster");
        public ConfigWindow configWindow = new();

        public static string Name => "PuppetMaster";

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>(Array.Empty<object>());
            Service.plugin = this;
            Service.InitializeConfig();
            windowSystem.AddWindow(configWindow);

            foreach (var CommandName in commandNames)
            {
                Service.CommandManager.AddHandler(CommandName.Item1, new CommandInfo(OnCommand)
                {
                    HelpMessage = CommandName.Item2
                });
            }

            Service.ChatGui.ChatMessage += new IChatGui.OnMessageDelegate(PuppetMaster.ChatHandler.OnChatMessage);
            Service.PluginInterface.UiBuilder.Draw += new Action(this.DrawUI);
            Service.PluginInterface.UiBuilder.OpenConfigUi += new Action(this.DrawConfigUI);
            Service.InitializeEmotes();
        }

        public void Dispose()
        {
            this.windowSystem.RemoveAllWindows();
            Service.ChatGui.ChatMessage -= new IChatGui.OnMessageDelegate(PuppetMaster.ChatHandler.OnChatMessage);

            foreach (var CommandName in commandNames)
            {
                Service.CommandManager.RemoveHandler(CommandName.Item1);
            }

            GC.SuppressFinalize(this);
        }

        private void OnCommand(string command, string args)
        {
            this.DrawConfigUI();
        }

        private void DrawUI()
        {
            this.windowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            configWindow.IsOpen = !configWindow.IsOpen;
        }
    }
}

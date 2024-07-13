using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

#nullable enable
namespace PuppetMaster
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        public string Name => "PuppetMaster - A.S Fork";

        private readonly List<Tuple<string, string>> commandNames = new()
        {
            new Tuple<string, string>("/puppetmaster", "Open PuppetMaster settings"),
            new Tuple < string, string >("/ppm", "Alias for PuppetMaster settings"),
            new Tuple < string, string >("/puppet", "Alias for PuppetMaster settings")
        };

        public WindowSystem WindowSystem = new("PuppetMaster - A.S Fork");
        public ConfigWindow ConfigWindow { get; init; } = new();


        public Plugin()
        {
            PluginInterface.Create<Service>(Array.Empty<object>());

            Service.InitializeConfig();

            WindowSystem.AddWindow(ConfigWindow);

            foreach (var CommandName in commandNames)
            {
                Service.CommandManager.AddHandler(CommandName.Item1, new CommandInfo(OnCommand)
                {
                    HelpMessage = CommandName.Item2
                });
            }

            Service.ChatGui.ChatMessage += new IChatGui.OnMessageDelegate(ChatHandler.OnChatMessage);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Service.InitializeEmotes();
            Service.InitializeWorlds();
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();

            Service.ChatGui.ChatMessage -= new IChatGui.OnMessageDelegate(ChatHandler.OnChatMessage);

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
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            // Toggle open/close plugin
            ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        }
    }
}

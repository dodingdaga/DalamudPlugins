using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NoireLib;
using System;
using System.Collections.Generic;

namespace PuppetMaster_Enhanced;

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
        NoireLibMain.Initialize(PluginInterface, this);

        Service.InitializeConfig();

        WindowSystem.AddWindow(ConfigWindow);

        foreach (var CommandName in commandNames)
        {
            NoireService.CommandManager.AddHandler(CommandName.Item1, new CommandInfo(OnCommand)
            {
                HelpMessage = CommandName.Item2
            });
        }

        NoireService.ChatGui.ChatMessage += new IChatGui.OnMessageDelegate(ChatHandler.OnChatMessage);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

        NoireService.ChatGui.ChatMessage -= new IChatGui.OnMessageDelegate(ChatHandler.OnChatMessage);

        foreach (var CommandName in commandNames)
            NoireService.CommandManager.RemoveHandler(CommandName.Item1);

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        NoireLibMain.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        DrawConfigUI();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
    }
}

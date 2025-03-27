using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Copycat.Windows;
using System;
using Dalamud.IoC;
using ECommons;

namespace Copycat
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "Right Back At You";
        private const string CommandName = "/rbay";

        public WindowSystem WindowSystem = new("RightBackAtYou");
        private readonly EmoteReaderHooks emoteReader;
        private readonly EmoteHandler emoteHandler;
        private readonly ConfigWindow configWindow = new();

        [PluginService] internal static IDalamudPluginInterface pluginInterface { get; private set; } = null!;

        public Plugin()
        {
            pluginInterface.Create<Service>();
            Service.plugin = this;
            Service.pluginInterface = pluginInterface;
            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            if (Service.clientState != null)
                Service.clientState.Logout += ClientState_Logout;

            WindowSystem.AddWindow(configWindow);

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open settings"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            pluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;

            this.emoteHandler = new EmoteHandler();
            this.emoteHandler.isPlayerLoggedIn = new Func<bool>(isPlayerLoggedIn);
            this.emoteReader = new EmoteReaderHooks();
            emoteReader.OnEmote += this.emoteHandler.OnEmote;

            // ECommons
            ECommonsMain.Init(pluginInterface, this, ECommons.Module.All);
        }
        private static string? GetCurrentPlayerName()
        {
            if (Service.clientState == null || Service.clientState.LocalPlayer == null || Service.clientState.LocalPlayer.Name == null)
            {
                return null;
            }

            return Service.clientState.LocalPlayer.Name.TextValue;
        }

        public static bool isPlayerLoggedIn()
        {
            if (Service.playerName == null)
            {
                Service.playerName = GetCurrentPlayerName();
                return initSettingsForCurrentPlayer();
            }
            return true;
        }

        public static bool initSettingsForCurrentPlayer()
        {
            if (Service.playerName == null) return false;
            Service.playerIndex = Service.configuration!.PlayerConfigurations.FindIndex(i => i.PlayerName.Equals(Service.playerName));
            if (Service.playerIndex == -1)
            {
                var playerConfiguration = new PlayerConfiguration();
                playerConfiguration.PlayerName = Service.playerName;
                Service.configuration.PlayerConfigurations.Add(playerConfiguration);
                Service.configuration.Save();

                Service.playerIndex = Service.configuration.PlayerConfigurations.Count - 1;
                Service.chatGui.Print("[Right Back At You] Settings created for " + Service.playerName);
            }
            else
            {
                Service.chatGui.Print("[Right Back At You] Settings loaded for " + Service.playerName);
            }

            return true;
        }

        private void ClientState_Logout(int type, int code)
        {
            Service.playerName = null;
            Service.playerIndex = -1;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            this.emoteReader.Dispose();

            Service.clientState.Logout -= ClientState_Logout;
            Service.commandManager.RemoveHandler(CommandName);

            ECommonsMain.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            if (!isPlayerLoggedIn())
                return;

            // in response to the slash command, just display our main ui
            DrawConfigUI();
        }

        private void DrawUI()
        {
            if (!isPlayerLoggedIn())
                return;

            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            if (!isPlayerLoggedIn())
                return;

            this.configWindow.IsOpen = true;
        }
    }
}

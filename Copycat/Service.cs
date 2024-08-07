using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Copycat
{
    internal class Service
    {
        public static Plugin? plugin;
        public static Configuration? configuration;
        public static IDalamudPluginInterface pluginInterface = null!;
        public static string? playerName = null;
        public static int playerIndex = -1;

        [PluginService]
        public static ICommandManager commandManager { get; private set; } = null!;

        [PluginService]
        public static IClientState clientState { get; private set; } = null!;

        [PluginService]
        public static IChatGui chatGui { get; private set; } = null!;

        [PluginService]
        public static ISigScanner sigScanner { get; private set; } = null!;

        [PluginService]
        public static IGameInteropProvider gameInteropProvider { get; private set; } = null!;

        [PluginService]
        public static IObjectTable objectTable { get; private set; } = null!;

        [PluginService]
        public static ITargetManager targetManager { get; private set; } = null!;

        [PluginService]
        public static IDataManager dataManager { get; private set; } = null!;

        [PluginService]
        public static IFramework framework { get; private set; } = null!;

        [PluginService]
        public static IPluginLog logger { get; private set; } = null!;

    }
}

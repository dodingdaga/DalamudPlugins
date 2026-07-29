using Dalamud.Configuration;

namespace Dalamud.Configuration
{
    public interface IPluginConfiguration
    {
        int Version { get; set; }
    }
}

namespace Dalamud.Plugin
{
    public interface IDalamudPluginInterface
    {
        void SavePluginConfig(IPluginConfiguration configuration);
    }
}

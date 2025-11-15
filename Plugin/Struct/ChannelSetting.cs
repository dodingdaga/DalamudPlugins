using Dalamud.Game.Text;

namespace PuppetMaster_Enhanced;

public class ChannelSetting
{
    public XivChatType ChatType { get; set; }

    public bool Enabled { get; set; }

    public string Name { get; set; } = string.Empty;
}

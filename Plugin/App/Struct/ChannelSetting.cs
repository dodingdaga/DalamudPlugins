using Dalamud.Game.Text;

#nullable enable
namespace PuppetMaster
{
    public class ChannelSetting
    {
        public XivChatType ChatType { get; set; }

        public bool Enabled { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}

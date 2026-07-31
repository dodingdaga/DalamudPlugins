using Dalamud.Interface.Windowing;
using System.Numerics;

namespace PuppetMaster;

internal sealed class MessageLogWindow : Window
{
    public MessageLogWindow() : base("Puppet Master — Message Logs")
    {
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(620, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        ConfigWindow.DrawLogsContent();
    }
}

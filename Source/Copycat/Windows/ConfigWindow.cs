using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Copycat.Windows;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base(
        "Right Back At You",
        ImGuiWindowFlags.AlwaysAutoResize)
    {
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = Service.configuration!.PlayerConfigurations[Service.playerIndex].Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            Service.configuration.PlayerConfigurations[Service.playerIndex].Enabled = enabled;
            Service.configuration.Save();
        }
        ImGui.SameLine();

        var targetBack = Service.configuration!.PlayerConfigurations[Service.playerIndex].TargetBack;
        if (ImGui.Checkbox("Target Back", ref targetBack))
        {
            Service.configuration.PlayerConfigurations[Service.playerIndex].TargetBack = targetBack;
            Service.configuration.Save();
        }
        ImGui.Spacing();
        ImGui.Spacing();
        var motionOnly = (Service.configuration!.PlayerConfigurations[Service.playerIndex].MotionOnly != "");
        if (ImGui.Checkbox("Motion Only", ref motionOnly))
        {
            Service.configuration.PlayerConfigurations[Service.playerIndex].MotionOnly = motionOnly? "motion":"";
            Service.configuration.Save();
        }
        ImGui.SameLine();
        var friendsOnly = Service.configuration.PlayerConfigurations[Service.playerIndex].Allowed == PlayerFlags.Friend;
        if (ImGui.Checkbox("Friends Only", ref friendsOnly))
        {
            Service.configuration.PlayerConfigurations[Service.playerIndex].Allowed = friendsOnly ? PlayerFlags.Friend : PlayerFlags.All;
            Service.configuration.Save();
        }
    }
}

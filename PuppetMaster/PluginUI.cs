using ImGuiNET;
using System;
using Dalamud.Interface.Windowing;

namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const String Name = "Puppet Master settings";
        private static readonly ImGuiWindowFlags defaultFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse |ImGuiWindowFlags.AlwaysAutoResize;

        private static Service.ParsedTextCommand textCommand = new();

        public ConfigWindow() : base(Name, defaultFlags)
        {
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static void DrawCheckbox(int index)
        {
            if (index % 4 != 0) ImGui.SameLine();
            var enabled = Service.configuration!.EnabledChannels[index].Enabled;
            if (ImGui.Checkbox(Service.configuration.EnabledChannels[index].Name, ref enabled))
            {
                Service.configuration.EnabledChannels[index].Enabled = enabled;
                Service.configuration.Save();
                if (Service.configuration.EnabledChannels[index].Enabled) Service.enabledChannels.Add(Service.configuration.EnabledChannels[index].ChatType);
                else Service.enabledChannels.Remove(Service.configuration.EnabledChannels[index].ChatType);
            }
        }

        public override void Draw()
        {

            Service.InitializeRegex();

            ImGui.PushItemWidth(350);
            var inputText = Service.configuration!.UseRegex ? Service.configuration.CustomPhrase : Service.configuration.TriggerPhrase;
            if (ImGui.InputText(Service.configuration!.UseRegex ? "Pattern":"Trigger", ref inputText, 500))
            {
                if (!Service.configuration.UseRegex)
                {
                    Service.configuration.TriggerPhrase = inputText.Trim();
                    Service.configuration.Save();
                }
                else
                {
                    Service.configuration.CustomPhrase = inputText.Trim();
                    Service.configuration.Save();
                }
                Service.InitializeRegex(true);
            }
            ImGui.PopItemWidth();

            if (!Service.configuration.UseRegex)
            {
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
                    ImGui.EndTooltip();
                }
            }

            var useRegex = Service.configuration.UseRegex;
            if (ImGui.Checkbox("Regex", ref useRegex))
            {
                Service.configuration.UseRegex = useRegex;
                Service.configuration.Save();
            }

            if (Service.configuration.UseRegex)
            {
                ImGui.SameLine();
                if (ImGui.Button("Reset"))
                {
                    Service.configuration.CustomPhrase = String.Empty;
                    Service.InitializeRegex();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Initialize regex and replacement\nbased on current trigger phrase");
                    ImGui.EndTooltip();
                }
            }

            textCommand = Service.GetTestInputCommand();

            ImGui.PushItemWidth(350);

            var testInput = Service.configuration.TestInput;
            if (ImGui.InputText("Test Input", ref testInput, 500))
            {
                Service.configuration.TestInput = testInput;
                Service.configuration.Save();
                textCommand = Service.GetTestInputCommand();
            }

            if (Service.configuration.UseRegex)
            {
                ImGui.Text($"Matched: {textCommand.Args}");

                var replaceMatch = Service.configuration.ReplaceMatch;
                if (ImGui.InputText("Replacement", ref replaceMatch, 500))
                {
                    Service.configuration.ReplaceMatch = replaceMatch;
                    Service.configuration.Save();
                    textCommand = Service.GetTestInputCommand();
                }
            }
            ImGui.Text($"Result: {textCommand.Main}");

            ImGui.PopItemWidth();

            if (ImGui.Button("Send Test Input to Chat Window"))
            {
                if (!String.IsNullOrEmpty(Service.configuration.TestInput))
                    Chat.SendMessage(Service.configuration.TestInput);
            }

            ImGui.Separator();

            var allowSit = Service.configuration.AllowSit;
            if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref allowSit))
            {
                Service.configuration.AllowSit = allowSit;
                Service.configuration.Save();
            }
            var motionOnly = Service.configuration.MotionOnly;
            if (ImGui.Checkbox("Motion only", ref motionOnly))
            {
                Service.configuration.MotionOnly = motionOnly;
                Service.configuration.Save();
            }

            var allowAllCommands = Service.configuration.AllowAllCommands;
            if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
            {
                Service.configuration.AllowAllCommands = allowAllCommands;
                Service.configuration.Save();
            }
            if (!Service.configuration.UseRegex)
            {
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("If command has subcommands, enclose sequence in parentheses.");
                    ImGui.Text("For placeholders, replace angle brackets with square brackets.");
                    int found = Service.configuration.TriggerPhrase.IndexOf('|');
                    String firstTriggerPhrase = found == -1 ? Service.configuration.TriggerPhrase : Service.configuration.TriggerPhrase[..found];
                    ImGui.Text("Example: " + firstTriggerPhrase + " (ac \"Vercure\" [t])");
                    ImGui.EndTooltip();
                }
            }

            ImGui.Separator();
            for (int index = 16; index < 23; ++index)
            {
                DrawCheckbox(index);
            }
            ImGui.Separator();
            for (int index = 0; index < 8; ++index)
            {
                DrawCheckbox(index);
            }
            ImGui.Separator();
            for (int index = 8; index < 16; ++index)
            {
                DrawCheckbox(index);
            }
        }
    }
}

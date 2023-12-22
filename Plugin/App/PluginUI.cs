using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Data.Parsing.Uld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using static Dalamud.Interface.Utility.Raii.ImRaii;

#nullable enable
namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const string Name = "Puppet Master Settings";
        private static ImGuiWindowFlags defaultFlags = ImGuiWindowFlags.NoCollapse;

        private BlacklistedPlayer? selectedBlacklistedPlayer;
        private WhitelistedPlayer? selectedWhitelistedPlayer;
        private string currentBlacklistSelectorSearch = "";
        private string currentWhitelistSelectorSearch = "";
        private string viewModeBlacklist = "default";
        private string viewModeWhitelist = "default";
        private int currentDraggedBlacklistIndex = -1;
        private int currentDraggedWhitelistIndex = -1;


        public ConfigWindow() : base("Puppet Master Settings", ConfigWindow.defaultFlags, false)
        {

        }

        public void Dispose() => GC.SuppressFinalize((object)this);

        public override void Draw()
        {
            if (ImGui.BeginTabBar("SettingsTabs"))
            {
                if (ImGui.BeginTabItem("General Settings"))
                {
                    DrawGeneralSettings();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Default Settings"))
                {
                    DrawDefaultSettings();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Whitelist"))
                {
                    DrawWhitelistSettings();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Blacklist"))
                {
                    DrawBlacklistSettings();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Help"))
                {
                    DrawHelp();

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawGeneralSettings()
        {
            ImGui.BeginChild("General_Settings", new Vector2(-1f, 40f), true);

            bool enablePlugin = Service.configuration.EnablePlugin;

            if (ImGui.Checkbox("Enable Plugin", ref enablePlugin))
            {
                Service.configuration.EnablePlugin = enablePlugin;
                Service.configuration.Save();
            }

            ImGui.SameLine();

            bool enableWhitelist = Service.configuration.EnableWhitelist;

            if (ImGui.Checkbox("Enable Whitelist", ref enableWhitelist))
            {
                Service.configuration.EnableWhitelist = enableWhitelist;
                Service.configuration.Save();
            }

            ImGui.SameLine();

            bool enableBlacklist = Service.configuration.EnableBlacklist;

            if (ImGui.Checkbox("Enable Blacklist", ref enableBlacklist))
            {
                Service.configuration.EnableBlacklist = enableBlacklist;
                Service.configuration.Save();
            }

            ImGui.EndChild();
        }

        private void DrawDefaultSettings()
        {
            Service.InitializeRegex();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Default trigger");

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Default trigger that will be used if the whitelist feature is disabled or\nif the whitelisted person does not have a specific trigger set-up");

            ImGui.PushItemWidth(350f);

            string str = Service.configuration.DefaultUseRegex ? Service.configuration.DefaultCustomPhrase : Service.configuration.DefaultTriggerPhrase;

            if (ImGui.InputText(Service.configuration.DefaultUseRegex ? "Default pattern" : "Default trigger", ref str, 500U))
            {
                if (!Service.configuration.DefaultUseRegex)
                {
                    Service.configuration.DefaultTriggerPhrase = str.Trim();
                }
                else
                {
                    Service.configuration.DefaultCustomPhrase = str.Trim();
                }

                Service.configuration.Save();
                Service.InitializeRegex(true);
            }

            ImGui.PopItemWidth();

            if (!Service.configuration.DefaultUseRegex && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
                ImGui.EndTooltip();
            }

            bool useRegex = Service.configuration.DefaultUseRegex;

            if (ImGui.Checkbox("Default use Regex", ref useRegex))
            {
                Service.configuration.DefaultUseRegex = useRegex;
                Service.configuration.Save();
            }

            if (Service.configuration.DefaultUseRegex)
            {
                ImGui.SameLine();

                if (ImGui.Button("Reset"))
                {
                    Service.configuration.DefaultCustomPhrase = string.Empty;
                    Service.InitializeRegex();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Initialize regex and replacement\nbased on current trigger phrase");
                    ImGui.EndTooltip();
                }
            }

            ImGui.PushItemWidth(350f);

            if (Service.configuration.DefaultUseRegex)
            {
                string replaceMatch = Service.configuration.DefaultReplaceMatch;

                if (ImGui.InputText("Replacement", ref replaceMatch, 500U))
                {
                    Service.configuration.DefaultReplaceMatch = replaceMatch;
                    Service.configuration.Save();
                }
            }

            ImGui.Separator();
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Default requests allowed");

            bool allowSit = Service.configuration.DefaultAllowSit;

            if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref allowSit))
            {
                Service.configuration.DefaultAllowSit = allowSit;
                Service.configuration.Save();
            }

            bool motionOnly = Service.configuration.DefaultMotionOnly;

            if (ImGui.Checkbox("Motion only", ref motionOnly))
            {
                Service.configuration.DefaultMotionOnly = motionOnly;
                Service.configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("If enabled, the game's emotes text won't be displayed to chat" +
                    "\nFor exemple, a \"grovel\" request will be replace with \"/grovel motion\"");
                ImGui.EndTooltip();
            }

            bool allowAllCommands = Service.configuration.DefaultAllowAllCommands;

            if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
            {
                Service.configuration.DefaultAllowAllCommands = allowAllCommands;
                Service.configuration.Save();
            }

            if (!Service.configuration.DefaultUseRegex && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("If command has subcommands, enclose sequence in parentheses.");
                ImGui.Text("For placeholders, replace angle brackets with square brackets.");
                int length = Service.configuration.DefaultTriggerPhrase.IndexOf('|');
                ImGui.Text("Example: " + (length == -1 ? Service.configuration.DefaultTriggerPhrase : Service.configuration.DefaultTriggerPhrase.Substring(0, length)) + " (ac \"Vercure\" [t])");
                ImGui.EndTooltip();
            }

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Default enabled channels");

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("The list of default channels that will be listened for commands" +
                "\nFor exemple, if you enable the say chat only, you will only be able to receive command through /say");

            for (int index = 16; index < 23; ++index)
                ConfigWindow.DrawCheckbox(index);

            ImGui.Separator();

            for (int index = 0; index < 8; ++index)
                ConfigWindow.DrawCheckbox(index);

            ImGui.Separator();

            for (int index = 8; index < 16; ++index)
                ConfigWindow.DrawCheckbox(index);
        }

        private void DrawWhitelistSettings()
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Whitelist");

            string? SelectedWhitelistedPlayerId = this.selectedWhitelistedPlayer?.Id;
            List<WhitelistedPlayer> WhitelistedPlayers = Service.configuration.WhitelistedPlayers;

            if (ImGui.BeginChild("Whitelist_Selector", new Vector2(225f, -ImGui.GetFrameHeightWithSpacing()), true))
            {
                ImGui.InputText("Search", ref this.currentWhitelistSelectorSearch, 200U);
                ImGui.Spacing();

                for (int index = 0; index < WhitelistedPlayers.Count; index++)
                {
                    var WhitelistedPlayer = WhitelistedPlayers[index];

                    if (WhitelistedPlayer != null)
                    {
                        if (CommonHelper.RegExpMatch(WhitelistedPlayer.PlayerName, this.currentWhitelistSelectorSearch) || (WhitelistedPlayer.PlayerName == String.Empty && CommonHelper.RegExpMatch(WhitelistedPlayer.Id, this.currentWhitelistSelectorSearch)))
                        {
                            string isDisabled = WhitelistedPlayer.Enabled ? "" : "[Disabled] ";
                            string name = WhitelistedPlayer.PlayerName == String.Empty ? WhitelistedPlayer.Id : WhitelistedPlayer.PlayerName;

                            if (ImGui.Selectable(isDisabled + name, WhitelistedPlayer.Id == SelectedWhitelistedPlayerId))
                            {
                                this.selectedWhitelistedPlayer = WhitelistedPlayer;
                                this.viewModeWhitelist = "edit";
                            }

                            if (ImGui.BeginDragDropSource())
                            {
                                this.currentDraggedWhitelistIndex = index;
                                ImGui.Text("Dragging: " + (WhitelistedPlayer.PlayerName == String.Empty ? WhitelistedPlayer.Id : WhitelistedPlayer.PlayerName));

                                unsafe
                                {
                                    int* draggedIndex = &index;
                                    ImGui.SetDragDropPayload("DRAG_WHITELIST", new IntPtr(draggedIndex), sizeof(int));
                                }

                                ImGui.EndDragDropSource();
                            }

                            if (ImGui.BeginDragDropTarget())
                            {
                                ImGuiPayloadPtr acceptPayload = ImGui.AcceptDragDropPayload("DRAG_WHITELIST");
                                bool isDropping = false;
                                unsafe
                                {
                                    isDropping = acceptPayload.NativePtr != null;
                                }

                                if (isDropping)
                                {
                                    var temp = WhitelistedPlayers[this.currentDraggedWhitelistIndex];
                                    WhitelistedPlayers.RemoveAt(this.currentDraggedWhitelistIndex);
                                    WhitelistedPlayers.Insert(index, temp);
                                    Service.configuration.Save();
                                    this.currentDraggedWhitelistIndex = -1;
                                }

                                ImGui.EndDragDropTarget();
                            }
                        }
                    }
                }

                ImGui.EndChild();
            }
            ImGui.SameLine();

            if (ImGui.BeginChild("Whitelist_View", new Vector2(0.0f, -ImGui.GetFrameHeightWithSpacing()), true))
            {
                if (viewModeWhitelist == "default")
                {
                    ImGui.TextWrapped("Press \"Add\" at the bottom of this window to add an entry to the whitelist.");
                }
                else if (viewModeWhitelist == "edit")
                {
                    if (this.selectedWhitelistedPlayer != null)
                    {
                        selectedWhitelistedPlayer.InitializeRegex();

                        ImGui.TextWrapped("Editing entry N°" + this.selectedWhitelistedPlayer.Id);
                        ImGui.Spacing();

                        if (ImGui.Checkbox("Enabled", ref selectedWhitelistedPlayer.Enabled))
                        {
                            Service.configuration.Save();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("Will enable or disable this specific whitelist entry");
                            ImGui.EndTooltip();
                        }

                        ImGui.Spacing();

                        if (ImGui.InputText("Player name", ref selectedWhitelistedPlayer.PlayerName, 99U))
                        {
                            Service.configuration.Save();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("You can input a full name or a partial name");
                            ImGui.EndTooltip();
                        }

                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();

                        if (ImGui.Checkbox("Use all default settings", ref selectedWhitelistedPlayer.UseAllDefaultSettings))
                        {
                            Service.configuration.Save();
                        }

                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();

                        bool useAllDefaultSettings = selectedWhitelistedPlayer.UseAllDefaultSettings;

                        if (!useAllDefaultSettings)
                        {
                            if (ImGui.Checkbox("Use default trigger settings", ref selectedWhitelistedPlayer.UseDefaultTrigger))
                            {
                                Service.configuration.Save();
                            }

                            ImGui.SameLine();

                            if (ImGui.Checkbox("Use default requests settings", ref selectedWhitelistedPlayer.UseDefaultRequests))
                            {
                                Service.configuration.Save();
                            }

                            if (ImGui.Checkbox("Use default enabled channels", ref selectedWhitelistedPlayer.UseDefaultEnabledChannels))
                            {
                                Service.configuration.Save();
                            }

                            ImGui.Spacing();

                            bool useDefaultTrigger = selectedWhitelistedPlayer.UseDefaultTrigger;
                            bool useDefaultRequests = selectedWhitelistedPlayer.UseDefaultRequests;
                            bool useDefaultEnabledChannels = selectedWhitelistedPlayer.UseDefaultEnabledChannels;

                            if (!useDefaultTrigger)
                            {
                                ImGui.Separator();
                                ImGui.Spacing();

                                ImGui.TextColored(ImGuiColors.DalamudViolet, "Trigger");

                                ImGui.SameLine();
                                ImGuiComponents.HelpMarker("The trigger phrase(s) that this person can use");

                                ImGui.PushItemWidth(350f);

                                string str = selectedWhitelistedPlayer.UseRegex ? selectedWhitelistedPlayer.CustomPhrase : selectedWhitelistedPlayer.TriggerPhrase;

                                if (ImGui.InputText(selectedWhitelistedPlayer.UseRegex ? "Pattern" : "Trigger", ref str, 500U))
                                {
                                    if (!selectedWhitelistedPlayer.UseRegex)
                                    {
                                        selectedWhitelistedPlayer.TriggerPhrase = str.Trim();
                                    } else {
                                        selectedWhitelistedPlayer.CustomPhrase = str.Trim();
                                    }

                                    Service.configuration.Save();
                                    selectedWhitelistedPlayer.InitializeRegex(true);
                                }

                                ImGui.PopItemWidth();

                                if (!selectedWhitelistedPlayer.UseRegex && ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
                                    ImGui.EndTooltip();
                                }

                                if (ImGui.Checkbox("Use Regex", ref selectedWhitelistedPlayer.UseRegex))
                                {
                                    Service.configuration.Save();
                                }

                                if (selectedWhitelistedPlayer.UseRegex)
                                {
                                    ImGui.SameLine();

                                    if (ImGui.Button("Reset"))
                                    {
                                        selectedWhitelistedPlayer.CustomPhrase = string.Empty;
                                        selectedWhitelistedPlayer.InitializeRegex();
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.BeginTooltip();
                                        ImGui.TextUnformatted("Initialize regex and replacement\nbased on current trigger phrase");
                                        ImGui.EndTooltip();
                                    }
                                }

                                ImGui.PushItemWidth(350f);

                                if (selectedWhitelistedPlayer.UseRegex)
                                {
                                    if (ImGui.InputText("Replacement", ref selectedWhitelistedPlayer.ReplaceMatch, 500U))
                                    {
                                        Service.configuration.Save();
                                    }
                                }
                            }

                            if (!useDefaultRequests)
                            {
                                ImGui.Separator();
                                ImGui.TextColored(ImGuiColors.DalamudViolet, "Allowed Requests");

                                ImGui.SameLine();
                                ImGuiComponents.HelpMarker("The requests that this person can use");

                                if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref selectedWhitelistedPlayer.AllowSit))
                                {
                                    Service.configuration.Save();
                                }

                                if (ImGui.Checkbox("Motion only", ref selectedWhitelistedPlayer.MotionOnly))
                                {
                                    Service.configuration.Save();
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.TextUnformatted("If enabled, the game's emotes text won't be displayed to chat" +
                                        "\nFor exemple, a \"grovel\" request will be replace with \"/grovel motion\"");
                                    ImGui.EndTooltip();
                                }

                                if (ImGui.Checkbox("Allow all text commands", ref selectedWhitelistedPlayer.AllowAllCommands))
                                {
                                    Service.configuration.Save();
                                }

                                if (!selectedWhitelistedPlayer.UseRegex && ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text("If command has subcommands, enclose sequence in parentheses.");
                                    ImGui.Text("For placeholders, replace angle brackets with square brackets.");
                                    int length = selectedWhitelistedPlayer.TriggerPhrase.IndexOf('|');
                                    ImGui.Text("Example: " + (length == -1 ? selectedWhitelistedPlayer.TriggerPhrase : selectedWhitelistedPlayer.TriggerPhrase.Substring(0, length)) + " (ac \"Vercure\" [t])");
                                    ImGui.EndTooltip();
                                }
                            }

                            if (!useDefaultEnabledChannels)
                            {
                                ImGui.Separator();

                                ImGui.TextColored(ImGuiColors.DalamudViolet, "Enabled channels");

                                ImGui.SameLine();
                                ImGuiComponents.HelpMarker("The list of channels that will be listened for commands for this specific user" +
                                    "\nFor exemple, if you enable the say chat only, you will only be able to receive command through /say");

                                List<(int, int)> ranges = new List<(int, int)>
                                {
                                    (16, 23),
                                    (0, 8),
                                    (8, 16)
                                };

                                foreach (var (range, index) in ranges.Select((r, i) => (r, i)))
                                {
                                    for (int i = range.Item1; i < range.Item2; ++i)
                                    {
                                        if (i % 4 != 0)
                                        {
                                            ImGui.SameLine();
                                        }

                                        bool enabled = selectedWhitelistedPlayer.EnabledChannels[i].Enabled;

                                        if (ImGui.Checkbox(selectedWhitelistedPlayer.EnabledChannels[i].Name, ref enabled))
                                        {
                                            selectedWhitelistedPlayer.EnabledChannels[i].Enabled = enabled;
                                            Service.configuration.Save();
                                        }
                                    }

                                    if (index != ranges.Count - 1)
                                    {
                                        ImGui.Separator();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ImGui.EndChild();

            if (ImGui.Button("Add"))
            {
                WhitelistedPlayer NewWhitelistedPlayer = new();
                Service.configuration.AddWhitelistedPlayer(NewWhitelistedPlayer);
                Service.configuration.Save();

                selectedWhitelistedPlayer = NewWhitelistedPlayer;
                this.viewModeWhitelist = "edit";
            }

            ImGui.SameLine();

            if (ImGui.Button("Delete"))
            {
                if (selectedWhitelistedPlayer != null)
                {
                    Service.configuration.RemoveWhitelistedPlayer(selectedWhitelistedPlayer);
                    Service.configuration.Save();
                }

                selectedWhitelistedPlayer = null;
                this.viewModeWhitelist = "default";
            }
        }

        private void DrawBlacklistSettings()
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Blacklist");

            string? SelectedBlacklistedPlayerId = this.selectedBlacklistedPlayer?.Id;
            List<BlacklistedPlayer> BlacklistedPlayers = Service.configuration.BlacklistedPlayers;

            if (ImGui.BeginChild("Blacklist_Selector", new Vector2(225f, -ImGui.GetFrameHeightWithSpacing()), true))
            {
                ImGui.InputText("Search", ref this.currentBlacklistSelectorSearch, 200U);
                ImGui.Spacing();

                for (int index = 0; index < BlacklistedPlayers.Count; index++)
                {
                    var BlacklistedPlayer = BlacklistedPlayers[index];

                    if (BlacklistedPlayer != null)
                    {
                        if (CommonHelper.RegExpMatch(BlacklistedPlayer.PlayerName, this.currentBlacklistSelectorSearch) || (BlacklistedPlayer.PlayerName == String.Empty && CommonHelper.RegExpMatch(BlacklistedPlayer.Id, this.currentBlacklistSelectorSearch)))
                        {
                            string isDisabled = BlacklistedPlayer.Enabled ? "" : "[Disabled] ";
                            string name = BlacklistedPlayer.PlayerName == String.Empty ? BlacklistedPlayer.Id : BlacklistedPlayer.PlayerName;

                            if (ImGui.Selectable(isDisabled + name, BlacklistedPlayer.Id == SelectedBlacklistedPlayerId))
                            {
                                this.selectedBlacklistedPlayer = BlacklistedPlayer;
                                this.viewModeBlacklist = "edit";
                            }

                            if (ImGui.BeginDragDropSource())
                            {
                                this.currentDraggedBlacklistIndex = index;
                                ImGui.Text("Dragging: " + (BlacklistedPlayer.PlayerName == String.Empty ? BlacklistedPlayer.Id : BlacklistedPlayer.PlayerName));

                                unsafe
                                {
                                    int* draggedIndexBl = &index;
                                    ImGui.SetDragDropPayload("DRAG_BLACKLIST", new IntPtr(draggedIndexBl), sizeof(int));
                                }

                                ImGui.EndDragDropSource();
                            }

                            if (ImGui.BeginDragDropTarget())
                            {
                                ImGuiPayloadPtr acceptPayload = ImGui.AcceptDragDropPayload("DRAG_BLACKLIST");
                                bool isDropping = false;
                                unsafe
                                {
                                    isDropping = acceptPayload.NativePtr != null;
                                }

                                if (isDropping)
                                {
                                    var temp = BlacklistedPlayers[this.currentDraggedBlacklistIndex];
                                    BlacklistedPlayers.RemoveAt(this.currentDraggedBlacklistIndex);
                                    BlacklistedPlayers.Insert(index, temp);
                                    Service.configuration.Save();
                                    this.currentDraggedBlacklistIndex = -1;
                                }

                                ImGui.EndDragDropTarget();
                            }
                        }
                    }
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();

            if (ImGui.BeginChild("Blacklist_View", new Vector2(0.0f, -ImGui.GetFrameHeightWithSpacing()), true))
            {
                if (viewModeBlacklist == "default")
                {
                    ImGui.TextWrapped("Press \"Add\" at the bottom of this window to add an entry to the blacklist.");
                }
                else if (viewModeBlacklist == "edit")
                {
                    if (this.selectedBlacklistedPlayer != null)
                    {
                        ImGui.TextWrapped("Editing entry N°" + this.selectedBlacklistedPlayer.Id);
                        ImGui.Spacing();

                        if (ImGui.Checkbox("Enabled", ref selectedBlacklistedPlayer.Enabled))
                        {
                            Service.configuration.Save();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("Will enable or disable this specific blacklist entry");
                            ImGui.EndTooltip();
                        }

                        ImGui.Spacing();

                        if (ImGui.InputText("Player name", ref selectedBlacklistedPlayer.PlayerName, 99U))
                        {
                            Service.configuration.Save();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("You can input a full name or a partial name");
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
            ImGui.EndChild();

            if (ImGui.Button("Add"))
            {
                BlacklistedPlayer NewBlacklistedPlayer = new();
                Service.configuration.AddBlacklistedPlayer(NewBlacklistedPlayer);
                Service.configuration.Save();

                selectedBlacklistedPlayer = NewBlacklistedPlayer;
                this.viewModeBlacklist = "edit";
            }

            ImGui.SameLine();

            if (ImGui.Button("Delete"))
            {
                if (selectedBlacklistedPlayer != null)
                {
                    Service.configuration.RemoveBlacklistedPlayer(selectedBlacklistedPlayer);
                    Service.configuration.Save();
                }

                selectedBlacklistedPlayer = null;
                this.viewModeBlacklist = "default";
            }
        }

        private void DrawHelp()
        {
            ImGui.BeginChild("Help_Usage", new Vector2(-1f, 140f), true);

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Usage :");
            ImGui.Text("Open menu : /puppetmaster, /puppet, /ppm");
            ImGui.Text("Trigger usage (emote) : <trigger_phrase> <emote>");
            ImGui.TextColored(ImGuiColors.DalamudOrange, "        Exemple:");
            ImGui.SameLine();
            ImGui.Text("please do grovel");
            ImGui.Text("Trigger usage (command) : <trigger_phrase> (<command> <subcommand> [placeholder])");
            ImGui.TextColored(ImGuiColors.DalamudOrange, "        Exemple:");
            ImGui.SameLine();
            ImGui.Text("please do (ac \"Vercure\" [t])");

            ImGui.EndChild();

            ImGui.BeginChild("Help_PluginInfos", new Vector2(-1f, 55f), true);

            // Gets the assembly full version
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Plugin informations :");
            ImGui.Text("App version : " + versionString);

            ImGui.EndChild();

            ImGui.BeginChild("Help_Development", new Vector2(-1f, 75f), true);

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Development :");
            ImGui.Text("Developed by DodingDaga");
            ImGui.Text("Forked by A.S.");

            ImGui.EndChild();
        }

        private static void DrawCheckbox(int index)
        {
            if (index % 4 != 0)
            {
                ImGui.SameLine();
            }

            bool enabled = Service.configuration.DefaultEnabledChannels[index].Enabled;

            if (ImGui.Checkbox(Service.configuration.DefaultEnabledChannels[index].Name, ref enabled))
            {
                Service.configuration.DefaultEnabledChannels[index].Enabled = enabled;
                Service.configuration.Save();
            }
        }
    }
}

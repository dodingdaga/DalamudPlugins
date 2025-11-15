using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using NoireLib;
using Dalamud.Game.ClientState.Objects.SubKinds;
using NoireLib.Models;

namespace PuppetMaster_Enhanced;

public class ConfigWindow : Window, IDisposable
{
    public const string Name = "Puppet Master Settings - A.S. Fork";
    private static ImGuiWindowFlags defaultFlags = ImGuiWindowFlags.NoCollapse;
    private static Service.ParsedTextCommand textCommand = new Service.ParsedTextCommand();

    private BlacklistedPlayer? selectedBlacklistedPlayer;
    private WhitelistedPlayer? selectedWhitelistedPlayer;
    private string currentBlacklistSelectorSearch = "";
    private string currentWhitelistSelectorSearch = "";
    private string viewModeBlacklist = "default";
    private string viewModeWhitelist = "default";
    private int currentDraggedBlacklistIndex = -1;
    private int currentDraggedWhitelistIndex = -1;

    public ConfigWindow() : base("Puppet Master Settings - A.S. Fork", defaultFlags, false) { }

    public void Dispose() { }

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

        bool enablePlugin = Configuration.Instance.EnablePlugin;

        if (ImGui.Checkbox("Enable Plugin", ref enablePlugin))
        {
            Configuration.Instance.EnablePlugin = enablePlugin;
        }

        ImGui.SameLine();

        bool enableWhitelist = Configuration.Instance.EnableWhitelist;

        if (ImGui.Checkbox("Enable Whitelist", ref enableWhitelist))
        {
            Configuration.Instance.EnableWhitelist = enableWhitelist;
        }

        ImGui.SameLine();

        bool enableBlacklist = Configuration.Instance.EnableBlacklist;

        if (ImGui.Checkbox("Enable Blacklist", ref enableBlacklist))
        {
            Configuration.Instance.EnableBlacklist = enableBlacklist;
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

        string str = Configuration.Instance.DefaultUseRegex ? Configuration.Instance.DefaultCustomPhrase : Configuration.Instance.DefaultTriggerPhrase;

        if (ImGui.InputText(Configuration.Instance.DefaultUseRegex ? "Default pattern" : "Default trigger", ref str, 500))
        {
            if (!Configuration.Instance.DefaultUseRegex)
            {
                Configuration.Instance.DefaultTriggerPhrase = str.Trim();
            }
            else
            {
                Configuration.Instance.DefaultCustomPhrase = str.Trim();
            }

            Service.InitializeRegex(true);
        }

        ImGui.PopItemWidth();

        if (!Configuration.Instance.DefaultUseRegex && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
            ImGui.EndTooltip();
        }

        bool useRegex = Configuration.Instance.DefaultUseRegex;

        if (ImGui.Checkbox("Default use Regex", ref useRegex))
        {
            Configuration.Instance.DefaultUseRegex = useRegex;
        }

        if (Configuration.Instance.DefaultUseRegex)
        {
            ImGui.SameLine();

            if (ImGui.Button("Reset"))
            {
                Configuration.Instance.DefaultCustomPhrase = string.Empty;
                Service.InitializeRegex();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Initialize regex and replacement\nbased on current trigger phrase");
                ImGui.EndTooltip();
            }
        }

        string testInput = Configuration.Instance.DefaultTestInput;

        if (ImGui.InputText("Test Input", ref testInput))
        {
            Configuration.Instance.DefaultTestInput = testInput;
            textCommand = Service.GetTestInputCommand();
        }

        ImGui.PushItemWidth(350f);

        if (Configuration.Instance.DefaultUseRegex)
        {
            ImGui.Text("Matched: " + textCommand.Args);

            string replaceMatch = Configuration.Instance.DefaultReplaceMatch;

            if (ImGui.InputText("Replacement", ref replaceMatch))
            {
                Configuration.Instance.DefaultReplaceMatch = replaceMatch;
                textCommand = Service.GetTestInputCommand();
            }
        }

        ImGui.Text("Result: " + textCommand.Main);
        ImGui.PopItemWidth();

        ImGui.Separator();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Default requests allowed");

        bool allowSit = Configuration.Instance.DefaultAllowSit;

        if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref allowSit))
        {
            Configuration.Instance.DefaultAllowSit = allowSit;
        }

        bool motionOnly = Configuration.Instance.DefaultMotionOnly;

        if (ImGui.Checkbox("Motion only", ref motionOnly))
        {
            Configuration.Instance.DefaultMotionOnly = motionOnly;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("If enabled, the game's emotes text won't be displayed to chat" +
                "\nFor example, a \"grovel\" request will be replace with \"/grovel motion\"");
            ImGui.EndTooltip();
        }

        bool allowAllCommands = Configuration.Instance.DefaultAllowAllCommands;

        if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
        {
            Configuration.Instance.DefaultAllowAllCommands = allowAllCommands;
        }

        if (!Configuration.Instance.DefaultUseRegex && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("If command has subcommands, enclose sequence in parentheses.");
            ImGui.Text("For placeholders, replace angle brackets with square brackets.");
            int length = Configuration.Instance.DefaultTriggerPhrase.IndexOf('|');
            ImGui.Text("Example: " + (length == -1 ? Configuration.Instance.DefaultTriggerPhrase : Configuration.Instance.DefaultTriggerPhrase.Substring(0, length)) + " (ac \"Vercure\" [t])");
            ImGui.EndTooltip();
        }

        ImGui.Separator();

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Default enabled channels");

        ImGui.SameLine();
        ImGuiComponents.HelpMarker("The list of default channels that will be listened for commands" +
            "\nFor example, if you enable the say chat only, you will only be able to receive command through /say");

        for (int index = 16; index < 23; ++index)
            DrawCheckbox(index);

        ImGui.Separator();

        for (int index = 0; index < 8; ++index)
            DrawCheckbox(index);

        ImGui.Separator();

        for (int index = 8; index < 16; ++index)
            DrawCheckbox(index);
    }

    private void DrawWhitelistSettings()
    {
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Whitelist");

        string? SelectedWhitelistedPlayerId = this.selectedWhitelistedPlayer?.Id;
        List<WhitelistedPlayer> WhitelistedPlayers = Configuration.Instance.WhitelistedPlayers;

        if (ImGui.BeginChild("Whitelist_Selector", new Vector2(225f, -ImGui.GetFrameHeightWithSpacing()), true))
        {
            ImGui.InputText("Search", ref this.currentWhitelistSelectorSearch, 200);
            ImGui.Spacing();

            for (int index = 0; index < WhitelistedPlayers.Count; index++)
            {
                var WhitelistedPlayer = WhitelistedPlayers[index];

                if (WhitelistedPlayer != null)
                {
                    if (CommonHelper.RegExpMatch(WhitelistedPlayer.PlayerName, this.currentWhitelistSelectorSearch) || (WhitelistedPlayer.PlayerName == String.Empty && CommonHelper.RegExpMatch(WhitelistedPlayer.Id, this.currentWhitelistSelectorSearch)))
                    {
                        string isDisabled = WhitelistedPlayer.Enabled ? "" : "[Disabled] ";
                        string name = WhitelistedPlayer.PlayerName.Trim() == String.Empty ? WhitelistedPlayer.Id : WhitelistedPlayer.PlayerName.Trim();

                        if (ImGui.Selectable(isDisabled + name, WhitelistedPlayer.Id == SelectedWhitelistedPlayerId))
                        {
                            this.selectedWhitelistedPlayer = WhitelistedPlayer;
                            this.viewModeWhitelist = "edit";
                        }

                        if (ImGui.BeginDragDropSource())
                        {
                            this.currentDraggedWhitelistIndex = index;
                            ImGui.Text("Dragging: " + (WhitelistedPlayer.PlayerName.Trim() == String.Empty ? WhitelistedPlayer.Id : WhitelistedPlayer.PlayerName.Trim()));

                            ImGui.SetDragDropPayload("DRAG_WHITELIST", null, 0);

                            ImGui.EndDragDropSource();
                        }

                        if (ImGui.BeginDragDropTarget())
                        {
                            ImGuiPayloadPtr acceptPayload = ImGui.AcceptDragDropPayload("DRAG_WHITELIST");
                            bool isDropping = !acceptPayload.IsNull;

                            if (isDropping)
                            {
                                var temp = WhitelistedPlayers[this.currentDraggedWhitelistIndex];
                                WhitelistedPlayers.RemoveAt(this.currentDraggedWhitelistIndex);
                                WhitelistedPlayers.Insert(index, temp);
                                Configuration.Instance.Save();
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

                    ImGui.Text("Editing entry N°" + this.selectedWhitelistedPlayer.Id);
                    ImGui.Spacing();

                    var enabled = selectedWhitelistedPlayer.Enabled;
                    if (ImGui.Checkbox("Enabled", ref enabled))
                    {
                        selectedWhitelistedPlayer.Enabled = enabled;
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Will enable or disable this specific whitelist entry");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    var playerName = selectedWhitelistedPlayer.PlayerName;
                    if (ImGui.InputText("Player name", ref playerName, 500))
                    {
                        selectedWhitelistedPlayer.PlayerName = playerName;
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("The " + (selectedWhitelistedPlayer.StrictPlayerName ? "full" : "partial") + " name of the whitelisted player (Example: Kitty Cat)" + (!selectedWhitelistedPlayer.StrictPlayerName ? "\nYou can use Regex" : ""));
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    var strictPlayerName = selectedWhitelistedPlayer.StrictPlayerName;
                    if (ImGui.Checkbox("Strict player name check", ref strictPlayerName))
                    {
                        selectedWhitelistedPlayer.StrictPlayerName = strictPlayerName;
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("If checked, the player name in chat has to match exactly with the one set above");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    // To do, maybe ? Add a dropdown menu with all worlds extracted from Lumina Sheets (in Service.Worlds)

                    var playerWorld = selectedWhitelistedPlayer.PlayerWorld;
                    if (ImGui.InputText("Player World", ref playerWorld, 500))
                    {
                        selectedWhitelistedPlayer.PlayerWorld = playerWorld;
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("The world name of the whitelisted player (Example: Balmung).\nType * to match any world.");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    var useAllDefaultSettings = selectedWhitelistedPlayer.UseAllDefaultSettings;
                    if (ImGui.Checkbox("Use all default settings", ref useAllDefaultSettings))
                    {
                        selectedWhitelistedPlayer.UseAllDefaultSettings = useAllDefaultSettings;
                        Configuration.Instance.Save();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    useAllDefaultSettings = selectedWhitelistedPlayer.UseAllDefaultSettings;

                    if (!useAllDefaultSettings)
                    {
                        var useDefaultTrigger = selectedWhitelistedPlayer.UseDefaultTrigger;
                        if (ImGui.Checkbox("Use default trigger settings", ref useDefaultTrigger))
                        {
                            selectedWhitelistedPlayer.UseDefaultTrigger = useDefaultTrigger;
                            Configuration.Instance.Save();
                        }

                        ImGui.SameLine();

                        var useDefaultRequests = selectedWhitelistedPlayer.UseDefaultRequests;
                        if (ImGui.Checkbox("Use default requests settings", ref useDefaultRequests))
                        {
                            selectedWhitelistedPlayer.UseDefaultRequests = useDefaultRequests;
                            Configuration.Instance.Save();
                        }

                        var useDefaultEnabledChannels = selectedWhitelistedPlayer.UseDefaultEnabledChannels;
                        if (ImGui.Checkbox("Use default enabled channels", ref useDefaultEnabledChannels))
                        {
                            selectedWhitelistedPlayer.UseDefaultEnabledChannels = useDefaultEnabledChannels;
                            Configuration.Instance.Save();
                        }

                        ImGui.Spacing();

                        useDefaultTrigger = selectedWhitelistedPlayer.UseDefaultTrigger;
                        useDefaultRequests = selectedWhitelistedPlayer.UseDefaultRequests;
                        useDefaultEnabledChannels = selectedWhitelistedPlayer.UseDefaultEnabledChannels;

                        if (!useDefaultTrigger)
                        {
                            ImGui.Separator();
                            ImGui.Spacing();

                            ImGui.TextColored(ImGuiColors.DalamudViolet, "Trigger");

                            ImGui.SameLine();
                            ImGuiComponents.HelpMarker("The trigger phrase(s) that this person can use");

                            string str = selectedWhitelistedPlayer.UseRegex ? selectedWhitelistedPlayer.CustomPhrase : selectedWhitelistedPlayer.TriggerPhrase;

                            if (ImGui.InputText(selectedWhitelistedPlayer.UseRegex ? "Pattern" : "Trigger", ref str, 500))
                            {
                                if (!selectedWhitelistedPlayer.UseRegex)
                                {
                                    selectedWhitelistedPlayer.TriggerPhrase = str.Trim();
                                }
                                else
                                {
                                    selectedWhitelistedPlayer.CustomPhrase = str.Trim();
                                }

                                Configuration.Instance.Save();
                                selectedWhitelistedPlayer.InitializeRegex(true);
                            }

                            if (!selectedWhitelistedPlayer.UseRegex && ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
                                ImGui.EndTooltip();
                            }

                            var useRegex = selectedWhitelistedPlayer.UseRegex;
                            if (ImGui.Checkbox("Use Regex", ref useRegex))
                            {
                                selectedWhitelistedPlayer.UseRegex = useRegex;
                                Configuration.Instance.Save();
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

                            var testInput = selectedWhitelistedPlayer.TestInput;
                            if (ImGui.InputText("Test Input", ref testInput, 500))
                            {
                                selectedWhitelistedPlayer.TestInput = testInput;
                                Configuration.Instance.Save();
                                selectedWhitelistedPlayer.TextCommand = selectedWhitelistedPlayer.GetTestInputCommand();
                            }

                            if (selectedWhitelistedPlayer.UseRegex)
                            {
                                ImGui.Text("Matched: " + selectedWhitelistedPlayer.TextCommand.Args);

                                var replaceMatch = selectedWhitelistedPlayer.ReplaceMatch;
                                if (ImGui.InputText("Replacement", ref replaceMatch, 500))
                                {
                                    selectedWhitelistedPlayer.ReplaceMatch = replaceMatch;
                                    Configuration.Instance.Save();
                                    selectedWhitelistedPlayer.TextCommand = selectedWhitelistedPlayer.GetTestInputCommand();
                                }
                            }


                            ImGui.Text("Result: " + selectedWhitelistedPlayer.TextCommand.Main);
                        }

                        if (!useDefaultRequests)
                        {
                            ImGui.Separator();
                            ImGui.TextColored(ImGuiColors.DalamudViolet, "Allowed Requests");

                            ImGui.SameLine();
                            ImGuiComponents.HelpMarker("The requests that this person can use");

                            var allowSit = selectedWhitelistedPlayer.AllowSit;
                            if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref allowSit))
                            {
                                selectedWhitelistedPlayer.AllowSit = allowSit;
                                Configuration.Instance.Save();
                            }

                            var motionOnly = selectedWhitelistedPlayer.MotionOnly;
                            if (ImGui.Checkbox("Motion only", ref motionOnly))
                            {
                                selectedWhitelistedPlayer.MotionOnly = motionOnly;
                                Configuration.Instance.Save();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted("If enabled, the game's emotes text won't be displayed to chat" +
                                    "\nFor example, a \"grovel\" request will be replace with \"/grovel motion\"");
                                ImGui.EndTooltip();
                            }

                            var allowAllCommands = selectedWhitelistedPlayer.AllowAllCommands;
                            if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
                            {
                                selectedWhitelistedPlayer.AllowAllCommands = allowAllCommands;
                                Configuration.Instance.Save();
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
                                "\nFor example, if you enable the say chat only, you will only be able to receive command through /say");

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

                                    bool enabledC = selectedWhitelistedPlayer.EnabledChannels[i].Enabled;

                                    if (ImGui.Checkbox(selectedWhitelistedPlayer.EnabledChannels[i].Name, ref enabledC))
                                    {
                                        selectedWhitelistedPlayer.EnabledChannels[i].Enabled = enabledC;
                                        Configuration.Instance.Save();
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
            Configuration.Instance.WhitelistedPlayers.Add(NewWhitelistedPlayer);
            Configuration.Instance.Save();

            selectedWhitelistedPlayer = NewWhitelistedPlayer;
            viewModeWhitelist = "edit";
        }

        ImGui.SameLine();

        var target = NoireService.TargetManager.Target;
        if (target is IPlayerCharacter playerChar)
        {
            var playerModel = new PlayerModel(playerChar);

            if (ImGui.Button($"Add {playerModel.FullName}"))
            {
                WhitelistedPlayer NewWhitelistedPlayer = new(playerModel.PlayerName);
                NewWhitelistedPlayer.PlayerWorld = playerModel.Homeworld;
                Configuration.Instance.WhitelistedPlayers.Add(NewWhitelistedPlayer);
                Configuration.Instance.Save();

                selectedWhitelistedPlayer = NewWhitelistedPlayer;
                viewModeWhitelist = "edit";
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Delete"))
        {
            if (selectedWhitelistedPlayer != null)
            {
                Configuration.Instance.WhitelistedPlayers.Remove(selectedWhitelistedPlayer);
                Configuration.Instance.Save();
            }

            selectedWhitelistedPlayer = null;
            this.viewModeWhitelist = "default";
        }
    }

    private void DrawBlacklistSettings()
    {
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Blacklist");

        string? SelectedBlacklistedPlayerId = selectedBlacklistedPlayer?.Id;
        List<BlacklistedPlayer> BlacklistedPlayers = Configuration.Instance.BlacklistedPlayers;

        if (ImGui.BeginChild("Blacklist_Selector", new Vector2(225f, -ImGui.GetFrameHeightWithSpacing()), true))
        {
            ImGui.InputText("Search", ref currentBlacklistSelectorSearch, 200);
            ImGui.Spacing();

            for (int index = 0; index < BlacklistedPlayers.Count; index++)
            {
                var BlacklistedPlayer = BlacklistedPlayers[index];

                if (BlacklistedPlayer != null)
                {
                    if (CommonHelper.RegExpMatch(BlacklistedPlayer.PlayerName, currentBlacklistSelectorSearch) || (BlacklistedPlayer.PlayerName == String.Empty && CommonHelper.RegExpMatch(BlacklistedPlayer.Id, this.currentBlacklistSelectorSearch)))
                    {
                        string isDisabled = BlacklistedPlayer.Enabled ? "" : "[Disabled] ";
                        string name = BlacklistedPlayer.PlayerName.Trim() == String.Empty ? BlacklistedPlayer.Id : BlacklistedPlayer.PlayerName.Trim();

                        if (ImGui.Selectable(isDisabled + name, BlacklistedPlayer.Id == SelectedBlacklistedPlayerId))
                        {
                            selectedBlacklistedPlayer = BlacklistedPlayer;
                            viewModeBlacklist = "edit";
                        }

                        if (ImGui.BeginDragDropSource())
                        {
                            currentDraggedBlacklistIndex = index;
                            ImGui.Text("Dragging: " + (BlacklistedPlayer.PlayerName.Trim() == String.Empty ? BlacklistedPlayer.Id : BlacklistedPlayer.PlayerName.Trim()));

                            ImGui.SetDragDropPayload("DRAG_BLACKLIST", null, 0);

                            ImGui.EndDragDropSource();
                        }

                        if (ImGui.BeginDragDropTarget())
                        {
                            ImGuiPayloadPtr acceptPayload = ImGui.AcceptDragDropPayload("DRAG_BLACKLIST");
                            bool isDropping = acceptPayload.IsDelivery();

                            if (isDropping)
                            {
                                var temp = BlacklistedPlayers[currentDraggedBlacklistIndex];
                                BlacklistedPlayers.RemoveAt(currentDraggedBlacklistIndex);
                                BlacklistedPlayers.Insert(index, temp);
                                Configuration.Instance.Save();
                                currentDraggedBlacklistIndex = -1;
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
                if (selectedBlacklistedPlayer != null)
                {
                    ImGui.Text("Editing entry N°" + selectedBlacklistedPlayer.Id);
                    ImGui.Spacing();

                    if (ImGui.Checkbox("Enabled", ref selectedBlacklistedPlayer.Enabled))
                    {
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Will enable or disable this specific blacklist entry");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    if (ImGui.InputText("Player name", ref selectedBlacklistedPlayer.PlayerName, 99))
                    {
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("The " + (selectedBlacklistedPlayer.StrictPlayerName ? "full" : "partial") + " name of the blacklisted player (Example: Kitty Cat)");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    if (ImGui.Checkbox("Strict player name check", ref selectedBlacklistedPlayer.StrictPlayerName))
                    {
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("If checked, the player name in chat has to match exactly with the one set above");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    // To do, maybe ? Add a dropdown menu with all worlds extracted from Lumina Sheets (in Service.Worlds)

                    if (ImGui.InputText("Player World", ref selectedBlacklistedPlayer.PlayerWorld, 500))
                    {
                        Configuration.Instance.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("The world name of the blacklisted player (Example: Balmung).\nType * to match any world.");
                        ImGui.EndTooltip();
                    }
                }
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("Add"))
        {
            BlacklistedPlayer NewBlacklistedPlayer = new();
            Configuration.Instance.BlacklistedPlayers.Add(NewBlacklistedPlayer);
            Configuration.Instance.Save();

            selectedBlacklistedPlayer = NewBlacklistedPlayer;
            viewModeBlacklist = "edit";
        }

        ImGui.SameLine();

        var target = NoireService.TargetManager.Target;
        if (target is IPlayerCharacter playerChar)
        {
            var playerModel = new PlayerModel(playerChar);

            if (ImGui.Button($"Add {playerModel.FullName}"))
            {
                BlacklistedPlayer NewBlacklistedPlayer = new(playerModel.PlayerName);
                NewBlacklistedPlayer.PlayerWorld = playerModel.Homeworld;
                Configuration.Instance.BlacklistedPlayers.Add(NewBlacklistedPlayer);
                Configuration.Instance.Save();

                selectedBlacklistedPlayer = NewBlacklistedPlayer;
                viewModeBlacklist = "edit";
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Delete"))
        {
            if (selectedBlacklistedPlayer != null)
            {
                Configuration.Instance.BlacklistedPlayers.Remove(selectedBlacklistedPlayer);
                Configuration.Instance.Save();
            }

            selectedBlacklistedPlayer = null;
            viewModeBlacklist = "default";
        }
    }

    private void DrawHelp()
    {
        ImGui.BeginChild("Help_Usage", new Vector2(-1f, 140f), true);

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Usage :");
        ImGui.Text("Open menu : /puppetmaster, /puppet, /ppm");
        ImGui.Text("Trigger usage (emote) : <trigger_phrase> <emote>");
        ImGui.TextColored(ImGuiColors.DalamudOrange, "        Example:");
        ImGui.SameLine();
        ImGui.Text("please do grovel");
        ImGui.Text("Trigger usage (command) : <trigger_phrase> (<command> <subcommand> [placeholder])");
        ImGui.TextColored(ImGuiColors.DalamudOrange, "        Example:");
        ImGui.SameLine();
        ImGui.Text("please do (ac \"Vercure\" [t])");

        ImGui.EndChild();

        ImGui.BeginChild("Help_PluginInfos", new Vector2(-1f, 55f), true);

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

        bool enabled = Configuration.Instance.DefaultEnabledChannels[index].Enabled;

        if (ImGui.Checkbox(Configuration.Instance.DefaultEnabledChannels[index].Name, ref enabled))
        {
            Configuration.Instance.DefaultEnabledChannels[index].Enabled = enabled;
            Configuration.Instance.Save();
        }
    }
}

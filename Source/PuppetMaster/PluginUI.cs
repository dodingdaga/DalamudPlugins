using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const String Name = "Puppet Master Settings";

        private static Service.ParsedTextCommand TextCommand = new();
        private static int CurrentReactionIndex;

        public ConfigWindow() : base(Name)
        {
            CurrentReactionIndex = Service.configuration!.CurrentReactionEdit;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public static void PreloadTestResult()
        {
            TextCommand = Service.GetTestInputCommand(Service.configuration!.CurrentReactionEdit);
        }

        private static void DrawReaction(int index)
        {
            var enabled = Service.configuration!.Reactions[index].Enabled;
            if (ImGui.Checkbox($"##{Service.configuration.Reactions[index].Name}##ReactionCheckBox{index}", ref enabled))
            {
                Service.semaphore.WaitOne();
                Service.configuration.Reactions[index].Enabled = enabled;
                Service.configuration.Save();
                Service.semaphore.Release();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.PushItemWidth(150);
            var reactionName = Service.configuration.Reactions[index].Name;
            if (ImGui.InputText($"##CustomChannelLabel##{index}", ref reactionName, 100))
            {
                Service.semaphore.WaitOne();
                Service.configuration.Reactions[index].Name = reactionName;
                Service.configuration.Save();
                Service.semaphore.Release();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            if (ImGui.Button($"{Localization.Get("Reaction.Delete")}##ReactionDelete##{index}"))
            {
                Service.semaphore.WaitOne();
                Service.configuration.Reactions.RemoveAt(index);
                Service.configuration.Save();
                Service.semaphore.Release();
            }
        }

        private static void DrawChannelCheckbox(int reactionIndex, int channelIndex, bool isGlobal = false)
        {
            if (channelIndex % 4 != 0) ImGui.SameLine();

            var chatType = Service.configuration!.EnabledChannels[channelIndex].ChatType;
            bool enabled;

            if (isGlobal)
            {
                var global = Service.configuration.GlobalSettings;
                enabled = global.GlobalEnabledChannels.Contains(chatType);
            }
            else
            {
                enabled = Service.configuration.Reactions[reactionIndex].EnabledChannels.Contains(chatType);
            }

            var checkboxName = isGlobal ?
                $"{Service.configuration.EnabledChannels[channelIndex].Name}##GlobalChannel{channelIndex}" :
                $"{Service.configuration.EnabledChannels[channelIndex].Name}##Channel{reactionIndex}_{channelIndex}";

            if (ImGui.Checkbox(checkboxName, ref enabled))
            {
                Service.semaphore.WaitOne();
                if (isGlobal)
                {
                    var global = Service.configuration.GlobalSettings;
                    if (enabled)
                    {
                        if (!global.GlobalEnabledChannels.Contains(chatType))
                            global.GlobalEnabledChannels.Add(chatType);
                    }
                    else
                    {
                        global.GlobalEnabledChannels.Remove(chatType);
                    }
                }
                else
                {
                    if (enabled)
                    {
                        Service.configuration.Reactions[reactionIndex].EnabledChannels.Add(chatType);
                    }
                    else
                    {
                        Service.configuration.Reactions[reactionIndex].EnabledChannels.Remove(chatType);
                    }
                }
                Service.configuration.Save();
                Service.semaphore.Release();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"ID:{Service.configuration.EnabledChannels[channelIndex].ChatType}");
                ImGui.EndTooltip();
            }
        }

        private static void DrawCustomChannelCheckbox(int reactionIndex, int channelIndex, bool isGlobal = false)
        {
            if (channelIndex % 4 != 0) ImGui.SameLine();

            var chatType = Service.configuration!.CustomChannels[channelIndex].ChatType;
            bool enabled;

            if (isGlobal)
            {
                var global = Service.configuration.GlobalSettings;
                enabled = global.GlobalEnabledChannels.Contains(chatType);
            }
            else
            {
                enabled = Service.configuration.Reactions[reactionIndex].EnabledChannels.Contains(chatType);
            }

            var checkboxName = isGlobal ?
                $"{Service.configuration.CustomChannels[channelIndex].Name}##GlobalCustomChannel{channelIndex}" :
                $"{Service.configuration.CustomChannels[channelIndex].Name}##CustomChannel{reactionIndex}_{channelIndex}";

            if (ImGui.Checkbox(checkboxName, ref enabled))
            {
                Service.semaphore.WaitOne();
                if (isGlobal)
                {
                    var global = Service.configuration.GlobalSettings;
                    if (enabled)
                    {
                        if (!global.GlobalEnabledChannels.Contains(chatType))
                            global.GlobalEnabledChannels.Add(chatType);
                    }
                    else
                    {
                        global.GlobalEnabledChannels.Remove(chatType);
                    }
                }
                else
                {
                    if (enabled)
                    {
                        Service.configuration.Reactions[reactionIndex].EnabledChannels.Add(chatType);
                    }
                    else
                    {
                        Service.configuration.Reactions[reactionIndex].EnabledChannels.Remove(chatType);
                    }
                }
                Service.configuration.Save();
                Service.semaphore.Release();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"ID:{Service.configuration.CustomChannels[channelIndex].ChatType}");
                ImGui.EndTooltip();
            }
        }

        private static void DrawCustomChannels(int index)
        {
            ImGui.PushItemWidth(100);
            var channelID = (int)Service.configuration!.CustomChannels[index].ChatType;
            if (ImGui.InputInt($"##CustomChannelID##{index}", ref channelID))
            {
                Service.configuration.CustomChannels[index].ChatType = channelID;
                Service.configuration.Save();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.PushItemWidth(150);
            var channelName = Service.configuration.CustomChannels[index].Name;
            if (ImGui.InputText($"##CustomChannelLabel##{index}", ref channelName, 100))
            {
                Service.configuration.CustomChannels[index].Name = channelName;
                Service.configuration.Save();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (ImGui.Button($"{Localization.Get("Custom.Delete")}##CustomChannelDelete#{index}"))
            {
                Service.semaphore.WaitOne();
                var chatType = Service.configuration.CustomChannels[index].ChatType;

                Service.configuration.GlobalSettings.GlobalEnabledChannels.Remove(chatType);

                for (var i = 0; i < Service.configuration.Reactions.Count; i++)
                {
                    Service.configuration.Reactions[i].EnabledChannels.Remove(chatType);
                }

                Service.configuration.CustomChannels.RemoveAt(index);
                Service.configuration.Save();
                Service.semaphore.Release();
            }
        }

        private static void DrawGlobalSettings()
        {
            ImGui.BeginTabBar("GlobalSettingsInnerTabs", ImGuiTabBarFlags.None);

            // Filter Settings Tab
            if (ImGui.BeginTabItem(Localization.Get("Tab.FilterSettings")))
            {
                DrawFilterSettings();
                ImGui.EndTabItem();
            }

            // Basic Settings Tab
            if (ImGui.BeginTabItem(Localization.Get("Tab.BasicSettings")))
            {
                DrawBasicSettings();
                ImGui.EndTabItem();
            }

            // Command Settings Tab
            if (ImGui.BeginTabItem(Localization.Get("Tab.CommandSettings")))
            {
                DrawCommandSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        private static void DrawFilterSettings()
        {
            var global = Service.configuration!.GlobalSettings;

            // ========== Speaker Filter (First) ==========
            ImGui.Text(Localization.Get("Global.SpeakerFilter"));
            var globalSpeakerFilter = (int)global.GlobalSpeakerFilter;
            var speakerFilterNames = new string[]
            {
        Localization.Get("Speaker.All"),
        Localization.Get("Speaker.IgnoreSelf"),
        Localization.Get("Speaker.SelfOnly")
            };
            if (ImGui.Combo("##GlobalSpeakerFilter", ref globalSpeakerFilter, speakerFilterNames, speakerFilterNames.Length))
            {
                global.GlobalSpeakerFilter = (SpeakerFilterMode)globalSpeakerFilter;
                Service.configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ========== Channel Settings (Second) ==========
            ImGui.Text(Localization.Get("Global.ChannelSettings"));
            var useGlobalChannels = global.UseGlobalChannels;
            if (ImGui.Checkbox(Localization.Get("Global.EnableChannels"), ref useGlobalChannels))
            {
                global.UseGlobalChannels = useGlobalChannels;
                Service.configuration.Save();
            }

            if (global.UseGlobalChannels)
            {
                ImGui.Indent(20);
                ImGui.TextColored(new Vector4(1, 1, 0, 1), Localization.Get("Global.ChannelNote1"));
                ImGui.TextColored(new Vector4(1, 1, 0, 1), Localization.Get("Global.ChannelNote2"));

                ImGui.Text(Localization.Get("UI_ChannelSelection"));

                // Default channels in groups
                ImGui.Text(Localization.Get("Global.DefaultChannels"));
                ImGui.Indent(10);

                // Group 1: CWLS
                for (var channelIndex = 16; channelIndex < 23; ++channelIndex)
                {
                    DrawChannelCheckbox(-1, channelIndex, true);
                }

                // Group 2: LS
                ImGui.Spacing();
                for (var channelIndex = 0; channelIndex < 8; ++channelIndex)
                {
                    DrawChannelCheckbox(-1, channelIndex, true);
                }

                // Group 3: Other channels
                ImGui.Spacing();
                for (var channelIndex = 8; channelIndex < 16; ++channelIndex)
                {
                    DrawChannelCheckbox(-1, channelIndex, true);
                }

                ImGui.Unindent(10);

                // Custom channels
                if (Service.configuration.CustomChannels.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text(Localization.Get("Global.CustomChannels"));
                    ImGui.Indent(10);
                    for (var channelIndex = 0; channelIndex < Service.configuration.CustomChannels.Count; ++channelIndex)
                    {
                        DrawCustomChannelCheckbox(-1, channelIndex, true);
                    }
                    ImGui.Unindent(10);
                }

                ImGui.Unindent(20);
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ========== Player and Command Lists (Third - with collapsible header) ==========
            bool listsExpanded = ImGui.CollapsingHeader(Localization.Get("Global.PlayerLists"), ImGuiTreeNodeFlags.DefaultOpen);
            if (listsExpanded)
            {
                var useGlobalPlayerLists = global.UseGlobalPlayerLists;
                if (ImGui.Checkbox(Localization.Get("Global.EnablePlayerLists"), ref useGlobalPlayerLists))
                {
                    global.UseGlobalPlayerLists = useGlobalPlayerLists;
                    Service.configuration.Save();
                }

                if (global.UseGlobalPlayerLists)
                {
                    ImGui.Columns(2, "ListsColumns", true);

                    // Left column: Player whitelist
                    ImGui.Text(Localization.Get("Global.PlayerWhitelist"));
                    var playerWhitelistText = string.Join("\n", global.GlobalPlayerWhitelist);
                    if (ImGui.InputTextMultiline("##GlobalPlayerWhitelist", ref playerWhitelistText, 1000,
                        new Vector2(ImGui.GetColumnWidth() - 10, 80)))
                    {
                        global.GlobalPlayerWhitelist = new List<string>(
                            playerWhitelistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                        Service.configuration.Save();
                    }

                    ImGui.NextColumn();

                    // Right column: Command whitelist
                    ImGui.Text(Localization.Get("Global.CommandWhitelist"));
                    var commandWhitelistText = string.Join("\n", global.GlobalCommandWhitelist);
                    if (ImGui.InputTextMultiline("##GlobalCommandWhitelist", ref commandWhitelistText, 1000,
                        new Vector2(ImGui.GetColumnWidth() - 10, 80)))
                    {
                        global.GlobalCommandWhitelist = new List<string>(
                            commandWhitelistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                        Service.configuration.Save();
                    }

                    ImGui.Columns(1);

                    // Second row: Blacklists
                    ImGui.Columns(2, "BlacklistsColumns", true);

                    // Left column: Player blacklist
                    ImGui.Text(Localization.Get("Global.PlayerBlacklist"));
                    var playerBlacklistText = string.Join("\n", global.GlobalPlayerBlacklist);
                    if (ImGui.InputTextMultiline("##GlobalPlayerBlacklist", ref playerBlacklistText, 1000,
                        new Vector2(ImGui.GetColumnWidth() - 10, 80)))
                    {
                        global.GlobalPlayerBlacklist = new List<string>(
                            playerBlacklistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                        Service.configuration.Save();
                    }

                    ImGui.NextColumn();

                    // Right column: Command blacklist
                    ImGui.Text(Localization.Get("Global.CommandBlacklist"));
                    var commandBlacklistText = string.Join("\n", global.GlobalCommandBlacklist);
                    if (ImGui.InputTextMultiline("##GlobalCommandBlacklist", ref commandBlacklistText, 1000,
                        new Vector2(ImGui.GetColumnWidth() - 10, 80)))
                    {
                        global.GlobalCommandBlacklist = new List<string>(
                            commandBlacklistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                        Service.configuration.Save();
                    }

                    ImGui.Columns(1);

                    // Help text
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), Localization.Get("Help.PlayerLists"));
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), Localization.Get("Help.CommandLists"));
                }
            }
        }

        private static void DrawBasicSettings()
        {
            var global = Service.configuration!.GlobalSettings;

            // ========== Delay and Cooldown ==========
            ImGui.Text(Localization.Get("Global.BasicSettings"));

            var globalDelay = global.GlobalDelaySeconds;
            if (ImGui.InputFloat(Localization.Get("Global.GlobalDelay"), ref globalDelay, 0.1f, 1.0f,
                $"%.1f {Localization.Get("Unit.Seconds")}"))
            {
                global.GlobalDelaySeconds = Math.Max(0, globalDelay);
                Service.configuration.Save();
            }

            var globalCooldown = global.GlobalCooldownSeconds;
            if (ImGui.InputFloat(Localization.Get("Global.GlobalCooldown"), ref globalCooldown, 0.1f, 1.0f,
                $"%.1f {Localization.Get("Unit.Seconds")}"))
            {
                global.GlobalCooldownSeconds = Math.Max(0, globalCooldown);
                Service.configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();
         
            // ========== Game State Restrictions ==========
            ImGui.Text(Localization.Get("Global.GameStateRestrictions"));
            var useGlobalGameState = global.UseGlobalGameStateRestrictions;
            if (ImGui.Checkbox(Localization.Get("Global.EnableGameState"), ref useGlobalGameState))
            {
                global.UseGlobalGameStateRestrictions = useGlobalGameState;
                Service.configuration.Save();
            }

            if (global.UseGlobalGameStateRestrictions)
            {
                ImGui.Indent(20);

                var disableInCombat = global.GlobalDisableInCombat;
                if (ImGui.Checkbox(Localization.Get("Global.DisableInCombat"), ref disableInCombat))
                {
                    global.GlobalDisableInCombat = disableInCombat;
                    Service.configuration.Save();
                }

                var disableInCutscene = global.GlobalDisableInCutscene;
                if (ImGui.Checkbox(Localization.Get("Global.DisableInCutscene"), ref disableInCutscene))
                {
                    global.GlobalDisableInCutscene = disableInCutscene;
                    Service.configuration.Save();
                }

                var disableWhileLoading = global.GlobalDisableWhileLoading;
                if (ImGui.Checkbox(Localization.Get("Global.DisableWhileLoading"), ref disableWhileLoading))
                {
                    global.GlobalDisableWhileLoading = disableWhileLoading;
                    Service.configuration.Save();
                }

                ImGui.Unindent(20);
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ========== Language Settings ==========
            ImGui.TextColored(ImGuiColors.DalamudYellow, Localization.Get("UI_LanguageSettings"));

            var language = Service.configuration.Language;
            if (ImGui.BeginCombo(Localization.Get("UI_Language"), language.ToString()))
            {
                foreach (PluginLanguage lang in Enum.GetValues(typeof(PluginLanguage)))
                {
                    if (ImGui.Selectable(lang.ToString(), language == lang))
                    {
                        Service.configuration.Language = lang;
                        Service.configuration.Save();
                    }
                }
                ImGui.EndCombo();
            }
        }

        private static void DrawCommandSettings()
        {
            // ========== Command Prefix ==========
            ImGui.TextColored(ImGuiColors.DalamudYellow, Localization.Get("UI_CommandSettings"));

            var prefix = Service.configuration!.CommandPrefix ?? "/puppetmaster";
            if (ImGui.InputText(Localization.Get("UI_CommandPrefix"), ref prefix, 50))
            {
                Service.configuration.CommandPrefix = prefix;
                Service.configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Localization.Get("UI_CommandPrefix_Help"));
            }

            // ========== Short Command ==========
            var enableShort = Service.configuration.EnableShortCommand;
            if (ImGui.Checkbox(Localization.Get("UI_EnableShortCommand"), ref enableShort))
            {
                Service.configuration.EnableShortCommand = enableShort;
                Service.configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Localization.Get("UI_EnableShortCommand_Help"));
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ========== Debug Settings ==========
            ImGui.Text(Localization.Get("Global.DebugSettings"));
            var enableVerboseDebug = Service.configuration.EnableVerboseDebug;
            if (ImGui.Checkbox(Localization.Get("Global.EnableDebug"), ref enableVerboseDebug))
            {
                Service.configuration.EnableVerboseDebug = enableVerboseDebug;
                Service.configuration.Save();
                Service.ChatGui.Print($"[PuppetMaster] {(enableVerboseDebug ?
                    Localization.Get("Command.DebugEnabled") :
                    Localization.Get("Command.DebugDisabled"))}");
            }
            ImGui.TextColored(new Vector4(1, 1, 0, 1), Localization.Get("Global.DebugWarning"));

            ImGui.Spacing();
            ImGui.Separator();

            // ========== Available Commands ==========
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"{Localization.Get("UI_AvailableCommands")}:");
            ImGui.Text($"  {Service.configuration.CommandPrefix} on/off");
            ImGui.Text($"  {Service.configuration.CommandPrefix} on/off <ReactionName>");
            ImGui.Text($"  {Service.configuration.CommandPrefix} debug on/off");
            ImGui.Text($"  {Service.configuration.CommandPrefix} list");

            if (Service.configuration.EnableShortCommand)
            {
                ImGui.Text($"  /pm (alias)");
            }

            ImGui.TextColored(ImGuiColors.DalamudOrange, Localization.Get("Command.RestartRequired"));
        }

        private static void DrawReactionGlobalOverrides(int index)
        {
            var reaction = Service.configuration!.Reactions[index];
            var global = Service.configuration.GlobalSettings;

            float effectiveDelay = reaction.GetEffectiveDelaySeconds(global);
            float effectiveCooldown = reaction.GetEffectiveCooldownSeconds(global);
            SpeakerFilterMode effectiveSpeakerFilter = reaction.GetEffectiveSpeakerFilter(global);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text(Localization.Get("Override.Title"));
            ImGui.Indent(20);

            ImGui.Text($"{Localization.Get("Override.Delay")} {effectiveDelay:F1} {Localization.Get("Unit.Seconds")} (Global: {global.GlobalDelaySeconds:F1} {Localization.Get("Unit.Seconds")})");
            ImGui.Indent(20);
            var useGlobalDelay = reaction.UseGlobalDelay;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalDelay"), ref useGlobalDelay))
            {
                reaction.UseGlobalDelay = useGlobalDelay;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalDelay)
            {
                var overrideDelay = reaction.OverrideDelaySeconds;
                if (ImGui.InputFloat(Localization.Get("Override.CustomDelay"), ref overrideDelay, 0.1f, 1.0f, $"%.1f {Localization.Get("Unit.Seconds")}"))
                {
                    reaction.OverrideDelaySeconds = Math.Max(0, overrideDelay);
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            ImGui.Text($"{Localization.Get("Override.Cooldown")} {effectiveCooldown:F1} {Localization.Get("Unit.Seconds")} (Global: {global.GlobalCooldownSeconds:F1} {Localization.Get("Unit.Seconds")})");
            ImGui.Indent(20);
            var useGlobalCooldown = reaction.UseGlobalCooldown;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalCooldown"), ref useGlobalCooldown))
            {
                reaction.UseGlobalCooldown = useGlobalCooldown;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalCooldown)
            {
                var overrideCooldown = reaction.OverrideCooldownSeconds;
                if (ImGui.InputFloat(Localization.Get("Override.CustomCooldown"), ref overrideCooldown, 0.1f, 1.0f, $"%.1f {Localization.Get("Unit.Seconds")}"))
                {
                    reaction.OverrideCooldownSeconds = Math.Max(0, overrideCooldown);
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            var speakerFilterNames = new string[]
            {
                Localization.Get("Speaker.All"),
                Localization.Get("Speaker.IgnoreSelf"),
                Localization.Get("Speaker.SelfOnly")
            };
            ImGui.Text($"{Localization.Get("Override.SpeakerFilter")} {speakerFilterNames[(int)effectiveSpeakerFilter]} (Global: {speakerFilterNames[(int)global.GlobalSpeakerFilter]})");
            ImGui.Indent(20);
            var useGlobalSpeakerFilter = reaction.UseGlobalSpeakerFilter;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalSpeaker"), ref useGlobalSpeakerFilter))
            {
                reaction.UseGlobalSpeakerFilter = useGlobalSpeakerFilter;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalSpeakerFilter)
            {
                var overrideFilter = (int)reaction.OverrideSpeakerFilter;
                if (ImGui.Combo(Localization.Get("Override.CustomSpeaker"), ref overrideFilter, speakerFilterNames, speakerFilterNames.Length))
                {
                    reaction.OverrideSpeakerFilter = (SpeakerFilterMode)overrideFilter;
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            ImGui.Text(Localization.Get("Override.PlayerLists"));
            ImGui.Indent(20);
            var useGlobalPlayerLists = reaction.UseGlobalPlayerLists;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalPlayers"), ref useGlobalPlayerLists))
            {
                reaction.UseGlobalPlayerLists = useGlobalPlayerLists;
                if (reaction.TriggerMode == TriggerMode.SpecificPlayer && useGlobalPlayerLists)
                {
                    reaction.UseGlobalPlayerLists = false;
                }
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalPlayerLists)
            {
                ImGui.Text(Localization.Get("Override.Whitelist"));
                var whitelistText = string.Join("\n", reaction.OverridePlayerWhitelist);
                if (ImGui.InputTextMultiline("##OverridePlayerWhitelist", ref whitelistText, 1000, new Vector2(300, 40)))
                {
                    reaction.OverridePlayerWhitelist = new List<string>(whitelistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    Service.configuration.Save();
                }

                ImGui.Text(Localization.Get("Override.Blacklist"));
                var blacklistText = string.Join("\n", reaction.OverridePlayerBlacklist);
                if (ImGui.InputTextMultiline("##OverridePlayerBlacklist", ref blacklistText, 1000, new Vector2(300, 40)))
                {
                    reaction.OverridePlayerBlacklist = new List<string>(blacklistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            ImGui.Text(Localization.Get("Override.CommandLists"));
            ImGui.Indent(20);
            var useGlobalCommandLists = reaction.UseGlobalCommandLists;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalCommands"), ref useGlobalCommandLists))
            {
                reaction.UseGlobalCommandLists = useGlobalCommandLists;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalCommandLists)
            {
                ImGui.Text(Localization.Get("Override.CommandWhitelist"));
                var whitelistText = string.Join("\n", reaction.OverrideCommandWhitelist);
                if (ImGui.InputTextMultiline("##OverrideCommandWhitelist", ref whitelistText, 1000, new Vector2(300, 40)))
                {
                    reaction.OverrideCommandWhitelist = new List<string>(whitelistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    Service.configuration.Save();
                }

                ImGui.Text(Localization.Get("Override.CommandBlacklist"));
                var blacklistText = string.Join("\n", reaction.OverrideCommandBlacklist);
                if (ImGui.InputTextMultiline("##OverrideCommandBlacklist", ref blacklistText, 1000, new Vector2(300, 40)))
                {
                    reaction.OverrideCommandBlacklist = new List<string>(blacklistText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            ImGui.Text(Localization.Get("Override.GameState"));
            ImGui.Indent(20);
            var useGlobalGameState = reaction.UseGlobalGameStateRestrictions;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalGameState"), ref useGlobalGameState))
            {
                reaction.UseGlobalGameStateRestrictions = useGlobalGameState;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalGameStateRestrictions)
            {
                var disableInCombat = reaction.OverrideDisableInCombat;
                if (ImGui.Checkbox(Localization.Get("Global.DisableInCombat"), ref disableInCombat))
                {
                    reaction.OverrideDisableInCombat = disableInCombat;
                    Service.configuration.Save();
                }

                var disableInCutscene = reaction.OverrideDisableInCutscene;
                if (ImGui.Checkbox(Localization.Get("Global.DisableInCutscene"), ref disableInCutscene))
                {
                    reaction.OverrideDisableInCutscene = disableInCutscene;
                    Service.configuration.Save();
                }

                var disableWhileLoading = reaction.OverrideDisableWhileLoading;
                if (ImGui.Checkbox(Localization.Get("Global.DisableWhileLoading"), ref disableWhileLoading))
                {
                    reaction.OverrideDisableWhileLoading = disableWhileLoading;
                    Service.configuration.Save();
                }
            }
            ImGui.Unindent(20);

            ImGui.Text(Localization.Get("Override.ChannelSettings"));
            ImGui.Indent(20);
            var useGlobalChannels = reaction.UseGlobalChannels;
            if (ImGui.Checkbox(Localization.Get("Override.UseGlobalChannels"), ref useGlobalChannels))
            {
                reaction.UseGlobalChannels = useGlobalChannels;
                Service.configuration.Save();
            }

            if (!reaction.UseGlobalChannels)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), Localization.Get("Override.ChannelNote"));

                ImGui.Separator();
                for (var channelIndex = 16; channelIndex < 23; ++channelIndex)
                {
                    DrawChannelCheckbox(index, channelIndex, false);
                }

                ImGui.Separator();
                for (var channelIndex = 0; channelIndex < 8; ++channelIndex)
                {
                    DrawChannelCheckbox(index, channelIndex, false);
                }

                ImGui.Separator();
                for (var channelIndex = 8; channelIndex < 16; ++channelIndex)
                {
                    DrawChannelCheckbox(index, channelIndex, false);
                }

                if (Service.configuration.CustomChannels.Count > 0)
                {
                    ImGui.Separator();
                    for (var channelIndex = 0; channelIndex < Service.configuration.CustomChannels.Count; ++channelIndex)
                    {
                        DrawCustomChannelCheckbox(index, channelIndex, false);
                    }
                }
            }
            else
            {
                int globalChannelCount = global.GlobalEnabledChannels?.Count ?? 0;
                ImGui.Text(string.Format(Localization.Get("Override.UsingGlobalChannels"), globalChannelCount));
            }
            ImGui.Unindent(20);

            ImGui.Unindent(20);
        }

        public override void Draw()
        {
            ImGui.SetNextWindowSize(new Vector2(520, 750), ImGuiCond.FirstUseEver);

            ImGui.BeginTabBar("PuppetMaster Config Tabs");

            if (ImGui.BeginTabItem(Localization.Get("Tab.GlobalSettings")))
            {
                DrawGlobalSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Localization.Get("Tab.Reactions")))
            {
                if (ImGui.Button($"{Localization.Get("Reaction.Add")}##ReactionAddButton"))
                {
                    Service.semaphore.WaitOne();
                    Service.configuration!.Reactions.Add(new Reaction() { Name = "Reaction" });
                    Service.configuration.Save();
                    Service.semaphore.Release();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                for (var index = 0; index < Service.configuration!.Reactions.Count; index++)
                {
                    DrawReaction(index);
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Localization.Get("Tab.EditReaction")))
            {
                var reactionNames = new List<string> { };
                foreach (var reaction in Service.configuration!.Reactions)
                    reactionNames.Add(reaction.Name);

                ImGui.SetNextItemWidth(450);
                if (ImGui.Combo("##ReactEditSelector", ref CurrentReactionIndex, [.. reactionNames], reactionNames.Count))
                {
                    Service.configuration.CurrentReactionEdit = CurrentReactionIndex;
                    Service.configuration.Save();
                    Service.InitializeRegex(CurrentReactionIndex);
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();

                if (Service.IsValidReactionIndex(Service.configuration.CurrentReactionEdit))
                {
                    ImGui.PushItemWidth(350);
                    ImGui.Indent(40);

                    ImGui.Text(Localization.Get("Edit.TriggerMode"));
                    ImGui.SameLine();
                    ImGui.SameLine();

                    var triggerMode = (int)Service.configuration.Reactions[CurrentReactionIndex].TriggerMode;
                    var triggerModeNames = new string[]
                    {
                        Localization.Get("Edit.TriggerMode.Keyword"),
                        Localization.Get("Edit.TriggerMode.Regex"),
                        Localization.Get("Edit.TriggerMode.SpecificPlayer")
                    };
                    if (ImGui.Combo("##TriggerMode", ref triggerMode, triggerModeNames, triggerModeNames.Length))
                    {
                        Service.semaphore.WaitOne();
                        var newTriggerMode = (TriggerMode)triggerMode;
                        var reaction = Service.configuration.Reactions[CurrentReactionIndex];
                        reaction.TriggerMode = newTriggerMode;

                        switch (newTriggerMode)
                        {
                            case TriggerMode.Regex:
                                reaction.UseRegex = true;
                                break;
                            case TriggerMode.Keyword:
                                reaction.UseRegex = false;
                                break;
                            case TriggerMode.SpecificPlayer:
                                reaction.UseGlobalPlayerLists = false;
                                reaction.UseRegex = false;
                                break;
                        }

                        Service.InitializeRegex(CurrentReactionIndex, true);
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.configuration.Save();
                        Service.semaphore.Release();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(Localization.Get("Edit.TriggerMode.Tooltip"));
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    switch ((TriggerMode)triggerMode)
                    {
                        case TriggerMode.SpecificPlayer:
                            ImGui.Text(Localization.Get("Edit.SpecificPlayers"));
                            var specificPlayersText = string.Join("\n", Service.configuration.Reactions[CurrentReactionIndex].SpecificPlayers);
                            if (ImGui.InputTextMultiline("##SpecificPlayers", ref specificPlayersText, 1000, new Vector2(350, 80)))
                            {
                                Service.semaphore.WaitOne();
                                Service.configuration.Reactions[CurrentReactionIndex].SpecificPlayers =
                                    new List<string>(specificPlayersText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                                Service.configuration.Save();
                                Service.semaphore.Release();
                            }
                            ImGui.TextColored(new Vector4(1, 1, 0, 1), Localization.Get("Edit.SpecificPlayersNote"));
                            break;

                        case TriggerMode.Keyword:
                        case TriggerMode.Regex:
                            if ((TriggerMode)triggerMode == TriggerMode.Keyword)
                            {
                                ImGui.Text(Localization.Get("Edit.Keyword"));
                                ImGui.SameLine();

                                var trigger = Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase;
                                if (ImGui.InputText("##Trigger", ref trigger, Service.configuration.MaxRegexLength))
                                {
                                    Service.semaphore.WaitOne();
                                    Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase = trigger;
                                    Service.InitializeRegex(CurrentReactionIndex, true);
                                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                                    Service.configuration.Save();
                                    Service.semaphore.Release();
                                }
                            }
                            else
                            {
                                ImGui.Text(Localization.Get("Edit.Regex"));
                                ImGui.SameLine();

                                var customPhrase = Service.configuration.Reactions[CurrentReactionIndex].CustomPhrase;
                                if (ImGui.InputText("##CustomPhrase", ref customPhrase, Service.configuration.MaxRegexLength))
                                {
                                    Service.semaphore.WaitOne();
                                    Service.configuration.Reactions[CurrentReactionIndex].CustomPhrase = customPhrase;
                                    Service.InitializeRegex(CurrentReactionIndex, true);
                                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                                    Service.configuration.Save();
                                    Service.semaphore.Release();
                                }

                                ImGui.Text(Localization.Get("Edit.Replacement"));
                                var replaceMatch = Service.configuration.Reactions[CurrentReactionIndex].ReplaceMatch;
                                if (ImGui.InputTextMultiline("##Replacement", ref replaceMatch, 500, new Vector2(350, 80)))
                                {
                                    Service.semaphore.WaitOne();
                                    Service.configuration.Reactions[CurrentReactionIndex].ReplaceMatch = replaceMatch;
                                    Service.configuration.Save();
                                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                                    Service.semaphore.Release();
                                }
                            }
                            break;
                    }

                    if ((TriggerMode)triggerMode != TriggerMode.SpecificPlayer)
                    {
                        ImGui.Indent(50);
                        ImGui.Text(Localization.Get("Edit.TestInput"));
                        ImGui.SameLine();

                        var testInput = Service.configuration.Reactions[CurrentReactionIndex].TestInput;
                        if (ImGui.InputText("##TestInput", ref testInput, 500))
                        {
                            Service.semaphore.WaitOne();
                            Service.configuration.Reactions[CurrentReactionIndex].TestInput = testInput;
                            Service.configuration.Save();
                            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                            Service.semaphore.Release();
                        }

                        ImGui.Unindent(45);

                        if ((TriggerMode)triggerMode == TriggerMode.Regex)
                        {
                            ImGui.Text($"{Localization.Get("Edit.Matched")} {TextCommand.Args}");
                        }
                        else if ((TriggerMode)triggerMode == TriggerMode.SpecificPlayer)
                        {
                            ImGui.Text($"{Localization.Get("Edit.DetectedEmote")} {TextCommand.Main}");
                        }
                        ImGui.Text($"{Localization.Get("Edit.WillExecute")} {TextCommand.Main}");
                    }

                    ImGui.PopItemWidth();
                    ImGui.Spacing();
                    ImGui.Spacing();

                    ImGui.Separator();

                    DrawReactionGlobalOverrides(CurrentReactionIndex);

                    ImGui.Spacing();
                    ImGui.Separator();

                    var allowAllCommands = Service.configuration.Reactions[CurrentReactionIndex].AllowAllCommands;
                    if (ImGui.Checkbox(Localization.Get("Common.AllowAllCommands"), ref allowAllCommands))
                    {
                        Service.semaphore.WaitOne();
                        Service.configuration.Reactions[CurrentReactionIndex].AllowAllCommands = allowAllCommands;
                        Service.configuration.Save();
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.semaphore.Release();
                    }

                    if ((TriggerMode)triggerMode == TriggerMode.Keyword)
                    {
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(Localization.Get("Common.Tooltip.AllowAllCommands"));
                            ImGui.EndTooltip();
                        }
                    }
                    var scanFullMessage = Service.configuration.Reactions[CurrentReactionIndex].ScanFullMessageForEmote;
                    if (ImGui.Checkbox(Localization.Get("Common.ScanFullMessage"), ref scanFullMessage))
                    {
                        Service.semaphore.WaitOne();
                        Service.configuration.Reactions[CurrentReactionIndex].ScanFullMessageForEmote = scanFullMessage;
                        Service.configuration.Save();
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.semaphore.Release();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();

                        if (Service.configuration.Reactions[CurrentReactionIndex].TriggerMode == TriggerMode.SpecificPlayer)
                        {
                            ImGui.TextUnformatted(Localization.Get("Common.Tooltip.ScanFullMessage.SpecificPlayer"));
                        }
                        else
                        {
                            ImGui.TextUnformatted(Localization.Get("Common.Tooltip.ScanFullMessage.Default"));
                        }

                        ImGui.EndTooltip();
                    }

                    var allowSit = Service.configuration.Reactions[CurrentReactionIndex].AllowSit;
                    if (ImGui.Checkbox(Localization.Get("Common.AllowSit"), ref allowSit))
                    {
                        Service.configuration.Reactions[CurrentReactionIndex].AllowSit = allowSit;
                        Service.configuration.Save();
                    }

                    var motionOnly = Service.configuration.Reactions[CurrentReactionIndex].MotionOnly;
                    if (ImGui.Checkbox(Localization.Get("Common.MotionOnly"), ref motionOnly))
                    {
                        Service.configuration.Reactions[CurrentReactionIndex].MotionOnly = motionOnly;
                        Service.configuration.Save();
                    }
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Localization.Get("Tab.CustomChannels")))
            {
                ImGui.SetNextItemWidth(400);

                var debugLogTypes = Service.configuration!.DebugLogTypes;
                if (ImGui.Checkbox(Localization.Get("Custom.EnableDebugTypes"), ref debugLogTypes))
                {
                    Service.configuration.DebugLogTypes = debugLogTypes;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(Localization.Get("Custom.DebugTooltip"));
                    ImGui.EndTooltip();
                }

                ImGui.SameLine();

                if (ImGui.Button($"{Localization.Get("Custom.Add")}##CustomChannelAdd"))
                {
                    Service.configuration.CustomChannels.Add(new ChannelSetting() { ChatType = (int)0, Name = "Custom", Enabled = false });
                    Service.configuration.Save();
                }

                ImGui.Spacing();
                ImGui.Spacing();

                if (Service.configuration.CustomChannels.Count > 0)
                {
                    for (var index = 0; index < Service.configuration.CustomChannels.Count; ++index)
                    {
                        DrawCustomChannels(index);
                    }
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}

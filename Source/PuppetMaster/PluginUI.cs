using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface.ImGuiNotification;

namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const String Name = "Puppet Master settings";

        private static Service.ParsedTextCommand TextCommand = new();
        private static int CurrentReactionIndex;
        private static int PendingReactionDelete = -1;
        private static bool OpenReactionDeletePopup;
        private static bool SelectReactionEditor;
        private static int ReactionEditorSection;
        private static int SettingsSection;
        private static string ReactionEditorSearch = string.Empty;
        private static string ChannelSearch = string.Empty;
        private static string ReactionSearch = string.Empty;
        private static int PendingReactionDuplicate = -1;
        private static int PendingAllowAllReaction = -1;
        private static string WhitelistCommandInput = string.Empty;
        private static string BlacklistCommandInput = string.Empty;
        private static string WhitelistCommandSearch = string.Empty;
        private static string BlacklistCommandSearch = string.Empty;
        private static string DefaultWhitelistCommandInput = string.Empty;
        private static string DefaultBlacklistCommandInput = string.Empty;
        private static string DefaultWhitelistCommandSearch = string.Empty;
        private static string DefaultBlacklistCommandSearch = string.Empty;
        private static readonly Dictionary<ChannelSetting, string> CustomChannelValidationErrors = new();

        private static readonly int[] CommonChannelIndexes = [16, 17, 18, 19, 20, 21, 22];
        private static readonly int[] CrossWorldLinkshellIndexes = [0, 1, 2, 3, 4, 5, 6, 7];
        private static readonly int[] LinkshellIndexes = [8, 9, 10, 11, 12, 13, 14, 15];
        private static readonly XivChatType[] ChatTypes = Enum.GetValues<XivChatType>();


        public ConfigWindow() : base(Name)
        {
            CurrentReactionIndex = Service.configuration!.CurrentReactionEdit;
            SizeConstraints = new()
            {
                MinimumSize = new Vector2(760, 500),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };
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
            var configuration = Service.configuration!;
            var reaction = configuration.Reactions[index];

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var enabled = reaction.Enabled;
            if (ImGui.Checkbox($"##{reaction.Name}##ReactionCheckBox{index}", ref enabled))
                SetReactionEnabled(reaction, enabled);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var reactionName = reaction.Name;
            if (ImGui.InputText($"##CustomChannelLabel##{index}", ref reactionName, 100))
            {
                Service.semaphore.WaitOne();
                reaction.Name = reactionName;
                configuration.Save();
                Service.semaphore.Release();
            }
            ImGui.TableNextColumn();
            var triggerPreview = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
            if (string.IsNullOrWhiteSpace(triggerPreview))
                ImGui.TextDisabled(reaction.UseRegex ? "Custom regex not set" : "Trigger not set");
            else
                ImGui.TextUnformatted(triggerPreview);
            if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(triggerPreview))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(triggerPreview);
                ImGui.EndTooltip();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(reaction.EnabledChannels.Count.ToString());

            ImGui.TableNextColumn();
            DrawReactionStatus(reaction);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Edit##ReactionEdit{index}"))
            {
                SelectReaction(index);
                SelectReactionEditor = true;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"Copy##ReactionDuplicate{index}"))
                PendingReactionDuplicate = index;
            ImGui.SameLine();
            if (configuration.Reactions.Count <= 1)
                ImGui.BeginDisabled();
            if (ImGui.SmallButton($"Delete##ReactionDelete{index}"))
            {
                PendingReactionDelete = index;
                OpenReactionDeletePopup = true;
            }
            if (configuration.Reactions.Count <= 1)
                ImGui.EndDisabled();
        }

        private static void SetReactionEnabled(Reaction reaction, bool enabled)
        {
            Service.semaphore.WaitOne();
            try
            {
                PluginUiLogic.SetReactionEnabled(reaction, enabled, ChatHandler.CancelReaction);
                Service.configuration!.Save();
            }
            finally
            {
                Service.semaphore.Release();
            }
        }

        private static void DrawReactionTable(IReadOnlyList<int> reactionIndexes, string tableId)
        {
            if (!ImGui.BeginTable(tableId, 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                return;

            ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 35);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("Trigger", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Channels", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 155);
            ImGui.TableHeadersRow();

            foreach (var index in reactionIndexes)
                DrawReaction(index);

            ImGui.EndTable();
        }

        private static void SetReactionGroupEnabled(IReadOnlyList<int> reactionIndexes, bool enabled)
        {
            var configuration = Service.configuration!;
            Service.semaphore.WaitOne();
            try
            {
                PluginUiLogic.SetReactionGroupEnabled(
                    configuration.Reactions,
                    reactionIndexes,
                    enabled,
                    ChatHandler.CancelReaction);
                configuration.Save();
            }
            finally
            {
                Service.semaphore.Release();
            }
        }

        private static bool ReactionMatchesSearch(Reaction reaction)
        {
            return PluginUiLogic.MatchesSearch(reaction, ReactionSearch);
        }

        private static void DrawReactionStatus(Reaction reaction)
        {
            var status = GetReactionStatus(reaction);
            ImGui.TextColored(status.Color, status.Label);
        }

        private static (string Label, Vector4 Color, bool NeedsAttention) GetReactionStatus(Reaction reaction)
        {
            return PluginUiLogic.GetStatus(reaction) switch
            {
                ReactionUiStatus.Disabled => ("Disabled", new Vector4(0.65f, 0.65f, 0.65f, 1), false),
                ReactionUiStatus.InvalidTrigger => ("Invalid trigger", new Vector4(1f, 0.35f, 0.35f, 1), true),
                ReactionUiStatus.NoChannels => ("No channels", new Vector4(1f, 0.75f, 0.2f, 1), true),
                ReactionUiStatus.Unsafe => ("Unsafe", new Vector4(1f, 0.55f, 0.2f, 1), true),
                _ => ("Ready", new Vector4(0.35f, 0.9f, 0.45f, 1), false),
            };
        }

        private static void SelectReaction(int index)
        {
            var configuration = Service.configuration!;
            CurrentReactionIndex = index;
            configuration.CurrentReactionEdit = index;
            Service.InitializeRegex(index);
            TextCommand = Service.GetTestInputCommand(index);
            WhitelistCommandInput = string.Empty;
            BlacklistCommandInput = string.Empty;
            configuration.Save();
        }

        private static void CreateReactionFromLog(DebugLogEntry entry)
        {
            var configuration = Service.configuration!;
            var officialChannelName = entry.ChatTypeId is >= ushort.MinValue and <= ushort.MaxValue
                ? Enum.GetName(typeof(XivChatType), (ushort)entry.ChatTypeId)
                : null;
            var channelName = officialChannelName ?? $"channel {entry.ChatTypeId}";
            var reaction = PluginUiLogic.CreateReactionFromLog(
                entry.ChatTypeId,
                entry.TriggerText,
                channelName,
                configuration);

            Service.semaphore.WaitOne();
            try
            {
                configuration.Reactions.Add(reaction);
            }
            finally
            {
                Service.semaphore.Release();
            }

            SelectReaction(configuration.Reactions.Count - 1);
            SelectReactionEditor = true;
        }

        private static void DrawCommandListEditor(
            string label,
            string description,
            List<string> commands,
            List<string> oppositeCommands,
            ref string input,
            ref string search,
            string id,
            bool refreshReactionTest = true,
            bool showDescriptionWarning = false,
            Reaction? reactionToInvalidate = null)
        {
            var configuration = Service.configuration!;
            ImGui.TextUnformatted($"{label} ({commands.Count})");
            if (!string.IsNullOrWhiteSpace(description))
            {
                if (showDescriptionWarning)
                    ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), description);
                else
                    ImGui.TextDisabled(description);
            }

            if (commands.Count >= 5)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint($"##{id}Search", "Search commands...", ref search, 100);
            }

            var searchText = search;
            var visibleCommands = commands
                .Where(command => string.IsNullOrWhiteSpace(searchText) || command.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var listHeight = Math.Min(140f, Math.Max(48f, visibleCommands.Length * ImGui.GetTextLineHeightWithSpacing() + 12f));
            if (ImGui.BeginChild($"##{id}List", new Vector2(0, listHeight), true))
            {
                foreach (var command in visibleCommands)
                {
                    ImGui.TextUnformatted(command);
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Remove##{id}{command}"))
                    {
                        commands.Remove(command);
                        if (reactionToInvalidate != null)
                            ChatHandler.InvalidateReaction(reactionToInvalidate, true);
                        configuration.Save();
                        if (refreshReactionTest)
                            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    }
                }

                if (visibleCommands.Length == 0)
                    ImGui.TextDisabled(commands.Count == 0 ? "No commands added." : "No matching commands.");
            }
            ImGui.EndChild();

            ImGui.SetNextItemWidth(-55);
            var addFromEnter = ImGui.InputTextWithHint(
                $"##{id}Input",
                "/command",
                ref input,
                100,
                ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            var addFromButton = ImGui.SmallButton($"Add##{id}Add");
            if (addFromEnter || addFromButton)
            {
                if (PluginUiLogic.AddCommandRule(commands, oppositeCommands, input))
                {
                    input = string.Empty;
                    if (reactionToInvalidate != null)
                        ChatHandler.InvalidateReaction(reactionToInvalidate, true);
                    configuration.Save();
                    if (refreshReactionTest)
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                }
            }
        }

        private static void DrawCommandRulesEditor(Reaction reaction)
        {
            ImGui.TextDisabled("Emotes are allowed by default. Commands listed under Denied commands are blocked.");

            DrawCommandListEditor(
                "Allowed commands",
                reaction.AllowAllCommands && reaction.CommandWhitelist.Count > 0
                    ? "Ignored while Allow all text commands is enabled."
                    : string.Empty,
                reaction.CommandWhitelist,
                reaction.CommandBlacklist,
                ref WhitelistCommandInput,
                ref WhitelistCommandSearch,
                "Whitelist",
                true,
                reaction.AllowAllCommands && reaction.CommandWhitelist.Count > 0,
                reaction);

            ImGui.Spacing();
            DrawCommandListEditor(
                "Denied commands",
                string.Empty,
                reaction.CommandBlacklist,
                reaction.CommandWhitelist,
                ref BlacklistCommandInput,
                ref BlacklistCommandSearch,
                "Blacklist",
                reactionToInvalidate: reaction);
        }

        private static void DrawTriggerEditor(Reaction reaction)
        {
            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);

            var trigger = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
            ImGui.TextUnformatted(reaction.UseRegex ? "Regex pattern" : "Trigger phrase");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Trigger", ref trigger, Service.configuration!.MaxRegexLength))
            {
                if (reaction.UseRegex)
                    reaction.CustomPhrase = trigger;
                else
                    reaction.TriggerPhrase = trigger;
                Service.InitializeRegex(CurrentReactionIndex, true);
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration.Save();
            }

            if (!reaction.UseRegex)
                ImGui.TextDisabled("Separate alternatives with |, for example: please do|simon says");

            var useRegex = reaction.UseRegex;
            if (ImGui.Checkbox("Use Regex", ref useRegex))
            {
                PluginUiLogic.SetRegexMode(reaction, useRegex);
                Service.InitializeRegex(CurrentReactionIndex, true);
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration.Save();
            }

            if (reaction.UseRegex)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Reset regex"))
                {
                    reaction.CustomPhrase = Service.GetDefaultRegex(CurrentReactionIndex);
                    reaction.ReplaceMatch = Service.GetDefaultReplaceMatch();
                    Service.InitializeRegex(CurrentReactionIndex, true);
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    ChatHandler.InvalidateReaction(reaction, false);
                    Service.configuration.Save();
                }

                var replacement = reaction.ReplaceMatch;
                ImGui.TextUnformatted("Replacement");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextMultiline("##Replacement", ref replacement, 500, new Vector2(-1, 65)))
                {
                    reaction.ReplaceMatch = replacement;
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    ChatHandler.InvalidateReaction(reaction, false);
                    Service.configuration.Save();
                }
            }

            var testInput = reaction.TestInput;
            ImGui.TextUnformatted("Test message");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##TestInput", ref testInput, 500))
            {
                reaction.TestInput = testInput;
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                Service.configuration.Save();
            }

            if (string.IsNullOrWhiteSpace(testInput))
                ImGui.TextDisabled("Enter a test message to preview the generated command.");
            else if (string.IsNullOrWhiteSpace(TextCommand.Main))
                ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), "No match");
            else
            {
                var generatedLines = TextCommand.Main.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
                var allowedCount = 0;
                var blockedCount = 0;
                ImGui.TextUnformatted("Generated commands");
                foreach (var line in generatedLines)
                {
                    var generatedCommand = Service.FormatCommand(line);
                    if (string.IsNullOrWhiteSpace(generatedCommand.Main))
                        continue;

                    if (Service.IsCommandAllowed(reaction, generatedCommand.Main, out _))
                    {
                        allowedCount++;
                        var allowedColor = new Vector4(0.35f, 0.9f, 0.45f, 1);
                        FontAwesome.Print(allowedColor, FontAwesome.Check);
                        ImGui.SameLine();
                        ImGui.TextColored(allowedColor, generatedCommand.ToString());
                    }
                    else
                    {
                        blockedCount++;
                        var blockedColor = new Vector4(1f, 0.35f, 0.35f, 1);
                        FontAwesome.Print(blockedColor, FontAwesome.Cross);
                        ImGui.SameLine();
                        ImGui.TextColored(blockedColor, generatedCommand.ToString());
                    }
                }

                if (blockedCount == 0)
                    ImGui.TextColored(new Vector4(0.35f, 0.9f, 0.45f, 1), $"All {allowedCount} commands will execute.");
                else
                    ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), $"{allowedCount} allowed, {blockedCount} blocked.");
                if (reaction.UseRegex)
                    ImGui.TextDisabled($"Matched: {TextCommand.Args}");
            }
        }

        private static void DrawCommandPermissionsEditor(Reaction reaction)
        {
            var allowAllCommands = reaction.AllowAllCommands;
            if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
            {
                if (allowAllCommands)
                {
                    PendingAllowAllReaction = CurrentReactionIndex;
                    ImGui.OpenPopup("Enable Allow All?");
                }
                else
                {
                    reaction.AllowAllCommands = false;
                    ChatHandler.InvalidateReaction(reaction, true);
                    Service.configuration!.Save();
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                }
            }

            if (ImGui.BeginPopupModal("Enable Allow All?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted("This permits any text command that is not explicitly denied.");
                ImGui.TextUnformatted("Only enable it for trusted triggers and channels.");
                if (ImGui.Button("Enable") && Service.IsValidReactionIndex(PendingAllowAllReaction))
                {
                    var pendingReaction = Service.configuration!.Reactions[PendingAllowAllReaction];
                    pendingReaction.AllowAllCommands = true;
                    ChatHandler.InvalidateReaction(pendingReaction, true);
                    Service.configuration.Save();
                    TextCommand = Service.GetTestInputCommand(PendingAllowAllReaction);
                    PendingAllowAllReaction = -1;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    PendingAllowAllReaction = -1;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private static void DrawEmoteBehaviorEditor(Reaction reaction)
        {
            var motionOnly = reaction.MotionOnly;
            if (ImGui.Checkbox("Motion only", ref motionOnly))
            {
                reaction.MotionOnly = motionOnly;
                ChatHandler.InvalidateReaction(reaction, true);
                Service.configuration!.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Suppresses emote chat text while still playing the animation.");
                ImGui.EndTooltip();
            }
        }

        private static void DuplicateReaction(int index)
        {
            var configuration = Service.configuration!;
            var source = configuration.Reactions[index];
            var copy = PluginUiLogic.CloneReaction(source);

            configuration.Reactions.Insert(index + 1, copy);
            SelectReaction(index + 1);
            SelectReactionEditor = true;
        }

        private static void DrawDeleteReactionConfirmation()
        {
            if (OpenReactionDeletePopup)
            {
                ImGui.OpenPopup("Delete reaction?");
                OpenReactionDeletePopup = false;
            }

            if (!ImGui.BeginPopupModal("Delete reaction?", ImGuiWindowFlags.AlwaysAutoResize))
                return;

            var configuration = Service.configuration!;
            var isValid = PendingReactionDelete >= 0 && PendingReactionDelete < configuration.Reactions.Count;
            var reactionName = isValid ? configuration.Reactions[PendingReactionDelete].Name : "this reaction";
            ImGui.TextUnformatted($"Delete '{reactionName}'? This cannot be undone.");
            ImGui.Spacing();

            if (ImGui.Button("Delete") && isValid && configuration.Reactions.Count > 1)
            {
                if (PluginUiLogic.TryDeleteReaction(
                        configuration.Reactions,
                        PendingReactionDelete,
                        out var nextIndex,
                        ChatHandler.CancelReaction))
                {
                    PendingReactionDelete = -1;
                    SelectReaction(nextIndex);
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                PendingReactionDelete = -1;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private static void DrawChannelGroup(string name, List<int> selectedChannels, string idSuffix, IReadOnlyList<ChannelSetting> channels, Action? onChanged = null)
        {
            var configuration = Service.configuration!;
            var visibleChannels = new List<ChannelSetting>();
            foreach (var channel in channels)
            {
                if (string.IsNullOrWhiteSpace(ChannelSearch) ||
                    channel.Name.Contains(ChannelSearch, StringComparison.OrdinalIgnoreCase) ||
                    channel.ChatType.ToString().Contains(ChannelSearch, StringComparison.OrdinalIgnoreCase))
                {
                    visibleChannels.Add(channel);
                }
            }

            if (visibleChannels.Count == 0)
                return;

            var selectedCount = 0;

            foreach (var channel in visibleChannels)
            {
                if (selectedChannels.Contains(channel.ChatType))
                    selectedCount++;
            }

            if (!string.IsNullOrWhiteSpace(ChannelSearch))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

            if (!ImGui.CollapsingHeader($"{name} ({selectedCount}/{visibleChannels.Count})##ChannelGroup{idSuffix}{name}"))
                return;

            if (ImGui.SmallButton($"All##ChannelGroupAll{idSuffix}{name}"))
            {
                foreach (var channel in visibleChannels)
                {
                    if (!selectedChannels.Contains(channel.ChatType))
                        selectedChannels.Add(channel.ChatType);
                }
                onChanged?.Invoke();
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"None##ChannelGroupNone{idSuffix}{name}"))
            {
                foreach (var channel in visibleChannels)
                    selectedChannels.Remove(channel.ChatType);
                onChanged?.Invoke();
                configuration.Save();
            }

            if (ImGui.BeginTable($"##ChannelTable{idSuffix}{name}", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                for (var index = 0; index < visibleChannels.Count; index++)
                {
                    ImGui.TableNextColumn();
                    var channel = visibleChannels[index];
                    var enabled = selectedChannels.Contains(channel.ChatType);
                    if (ImGui.Checkbox($"{channel.Name}##GroupedChannel{idSuffix}{name}{index}{channel.ChatType}", ref enabled))
                    {
                        PluginUiLogic.SetChannel(selectedChannels, channel.ChatType, enabled);
                        onChanged?.Invoke();
                        configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted($"Log type ID: {channel.ChatType}");
                        ImGui.EndTooltip();
                    }
                }
                ImGui.EndTable();
            }
        }

        private static void DrawDefaultChannelGroup(string name, List<int> selectedChannels, string idSuffix, int[] indexes, Action? onChanged = null)
        {
            var defaultChannels = Service.configuration!.EnabledChannels;
            var channels = new List<ChannelSetting>(indexes.Length);
            foreach (var index in indexes)
            {
                if (index < defaultChannels.Count)
                    channels.Add(defaultChannels[index]);
            }
            DrawChannelGroup(name, selectedChannels, idSuffix, channels, onChanged);
        }

        private static List<ChannelSetting> GetAdvancedChannels()
        {
            var configuration = Service.configuration!;
            var standardIds = new HashSet<int>();
            foreach (var channel in configuration.EnabledChannels)
                standardIds.Add(channel.ChatType);

            var seenIds = new HashSet<int>();
            var channels = new List<ChannelSetting>();
            foreach (var type in ChatTypes)
            {
                var id = (int)type;
                if (standardIds.Contains(id) || !seenIds.Add(id))
                    continue;
                channels.Add(new ChannelSetting
                {
                    ChatType = id,
                    Name = type.ToString(),
                });
            }
            return channels;
        }

        private static List<ChannelSetting> GetCustomChannels()
        {
            var channels = new List<ChannelSetting>();
            foreach (var channel in Service.configuration!.CustomChannels)
            {
                if (channel.ChatType >= 0 &&
                    channel.ChatType <= ushort.MaxValue &&
                    Array.FindIndex(ChatTypes, type => (int)type == channel.ChatType) < 0)
                    channels.Add(channel);
            }
            return channels;
        }

        private static bool IsOfficialChatType(int chatTypeId)
        {
            return Array.FindIndex(ChatTypes, type => (int)type == chatTypeId) >= 0;
        }

        private static bool IsConfiguredCustomChannel(int chatTypeId)
        {
            return Service.configuration!.CustomChannels.Exists(channel => channel.ChatType == chatTypeId);
        }

        private static void AddCustomChannel(int chatTypeId)
        {
            var configuration = Service.configuration!;
            if (IsOfficialChatType(chatTypeId) || IsConfiguredCustomChannel(chatTypeId))
                return;

            configuration.CustomChannels.Add(new ChannelSetting
            {
                ChatType = chatTypeId,
                Name = $"Custom {chatTypeId}",
            });
            configuration.Save();
        }

        private static void DrawChannelSelector(int reactionIndex)
        {
            var configuration = Service.configuration!;
            var reaction = configuration.Reactions[reactionIndex];
            DrawChannelSelector(
                reaction.EnabledChannels,
                $"Reaction{reactionIndex}",
                "This reaction will not listen to any messages.",
                () => ChatHandler.InvalidateReaction(reaction, false));
        }

        private static void DrawChannelSelector(List<int> selectedChannels, string idSuffix, string emptyMessage, Action? onChanged = null)
        {
            var configuration = Service.configuration!;

            ImGui.TextUnformatted($"Enabled Channels ({selectedChannels.Count} selected)");
            ImGui.SameLine();
            if (ImGui.SmallButton($"All##AllChannels{idSuffix}"))
            {
                selectedChannels.Clear();
                foreach (var channel in configuration.EnabledChannels)
                    selectedChannels.Add(channel.ChatType);
                foreach (var channel in GetAdvancedChannels())
                    selectedChannels.Add(channel.ChatType);
                foreach (var channel in GetCustomChannels())
                {
                    if (!selectedChannels.Contains(channel.ChatType))
                        selectedChannels.Add(channel.ChatType);
                }
                onChanged?.Invoke();
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"None##NoChannels{idSuffix}"))
            {
                selectedChannels.Clear();
                onChanged?.Invoke();
                configuration.Save();
            }

            if (selectedChannels.Count == 0)
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1), emptyMessage);

            DrawSelectedChannels(selectedChannels, idSuffix, onChanged);

            ImGui.SetNextItemWidth(-70);
            ImGui.InputTextWithHint($"##ChannelSearch{idSuffix}", "Search channels by name or ID...", ref ChannelSearch, 100);
            if (!string.IsNullOrEmpty(ChannelSearch))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Clear##ChannelSearchClear{idSuffix}"))
                    ChannelSearch = string.Empty;
            }

            DrawDefaultChannelGroup("Common", selectedChannels, idSuffix, CommonChannelIndexes, onChanged);
            DrawDefaultChannelGroup("Cross-world Linkshells", selectedChannels, idSuffix, CrossWorldLinkshellIndexes, onChanged);
            DrawDefaultChannelGroup("Linkshells", selectedChannels, idSuffix, LinkshellIndexes, onChanged);

            var advancedChannels = GetAdvancedChannels();
            if (advancedChannels.Count > 0)
                DrawChannelGroup("Advanced", selectedChannels, idSuffix, advancedChannels, onChanged);

            var customChannels = GetCustomChannels();
            if (customChannels.Count > 0)
                DrawChannelGroup("Custom", selectedChannels, idSuffix, customChannels, onChanged);
        }

        private static void DrawSelectedChannels(List<int> selectedChannels, string idSuffix, Action? onChanged = null)
        {
            var configuration = Service.configuration!;
            if (selectedChannels.Count == 0)
                return;

            var availableChannels = new Dictionary<int, string>();
            foreach (var channel in configuration.EnabledChannels)
                availableChannels.TryAdd(channel.ChatType, channel.Name);
            foreach (var channel in GetAdvancedChannels())
                availableChannels.TryAdd(channel.ChatType, channel.Name);
            foreach (var channel in GetCustomChannels())
                availableChannels.TryAdd(channel.ChatType, channel.Name);

            ImGui.Spacing();
            ImGui.TextUnformatted($"Selected Channels ({selectedChannels.Count})");
            ImGui.Separator();

            if (ImGui.BeginTable($"##SelectedChannelTable{idSuffix}", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                var selectedSnapshot = selectedChannels.ToArray();
                for (var index = 0; index < selectedSnapshot.Length; index++)
                {
                    var chatTypeId = selectedSnapshot[index];
                    var name = availableChannels.TryGetValue(chatTypeId, out var channelName)
                        ? channelName
                        : $"Unknown {chatTypeId}";
                    var enabled = true;

                    ImGui.TableNextColumn();
                    if (ImGui.Checkbox($"{name}##SelectedChannel{idSuffix}{chatTypeId}_{index}", ref enabled) && !enabled)
                    {
                        selectedChannels.Remove(chatTypeId);
                        onChanged?.Invoke();
                        configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted($"Log type ID: {chatTypeId}\nUncheck to remove this channel.");
                        ImGui.EndTooltip();
                    }
                }
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
        }

        private static void DrawCustomChannels(int index)
        {
            var configuration = Service.configuration!;
            var channel = configuration.CustomChannels[index];
            var channelID = channel.ChatType;
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt($"##CustomChannelID##{index}", ref channelID))
            {
                var validationError = ValidateCustomChannelId(channel, channelID);
                if (validationError != null)
                {
                    CustomChannelValidationErrors[channel] = validationError;
                }
                else
                {
                    CustomChannelValidationErrors.Remove(channel);
                    Service.semaphore.WaitOne();
                    try
                    {
                        var previousChannelId = channel.ChatType;
                        channel.ChatType = channelID;
                        foreach (var reaction in configuration.Reactions)
                        {
                            if (!reaction.EnabledChannels.Remove(previousChannelId))
                                continue;
                            if (!reaction.EnabledChannels.Contains(channelID))
                                reaction.EnabledChannels.Add(channelID);
                            ChatHandler.InvalidateReaction(reaction, false);
                        }
                        configuration.Save();
                    }
                    finally
                    {
                        Service.semaphore.Release();
                    }
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Numeric log type ID. Use this for undocumented types.");
                ImGui.EndTooltip();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.PushItemWidth(150);
            var channelName = channel.Name;
            if(ImGui.InputText($"##CustomChannelLabel##{index}",ref channelName,100))
            {
                channel.Name = channelName;
                configuration.Save();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (ImGui.Button($"Delete##CustomChannelDelete#{index}"))
            {
                Service.semaphore.WaitOne();
                try
                {
                    foreach (var reaction in configuration.Reactions)
                    {
                        if (reaction.EnabledChannels.Remove(channelID))
                            ChatHandler.InvalidateReaction(reaction, false);
                    }
                    configuration.CustomChannels.RemoveAt(index);
                    CustomChannelValidationErrors.Remove(channel);
                    configuration.Save();
                }
                finally
                {
                    Service.semaphore.Release();
                }
            }

            if (CustomChannelValidationErrors.TryGetValue(channel, out var validationErrorText))
                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), validationErrorText);
        }

        private static string? ValidateCustomChannelId(ChannelSetting channel, int channelId)
        {
            return PluginUiLogic.ValidateCustomChannelId(
                channel,
                channelId,
                Service.configuration!.CustomChannels,
                IsOfficialChatType);
        }

        private static void DrawCustomChannelSettings()
        {
            var configuration = Service.configuration!;
            ImGui.TextUnformatted("Custom Channels");
            ImGui.Separator();
            if (ImGui.Button("Add##CustomChannelAdd"))
            {
                var channel = new ChannelSetting
                {
                    ChatType = -1,
                    Name = "Custom",
                    Enabled = false,
                };
                configuration.CustomChannels.Add(channel);
                CustomChannelValidationErrors[channel] = "Enter a valid undocumented channel ID.";
                configuration.Save();
            }

            ImGui.SameLine();
            ImGui.TextDisabled("Add undocumented numeric log types discovered through message logging.");
            ImGui.Spacing();

            var customChannelCount = 0;
            for (var index = 0; index < configuration.CustomChannels.Count; ++index)
            {
                if (Array.FindIndex(ChatTypes, type => (int)type == configuration.CustomChannels[index].ChatType) < 0)
                {
                    customChannelCount++;
                    DrawCustomChannels(index);
                }
            }

            if (customChannelCount == 0)
                ImGui.TextDisabled("No custom channels configured.");
        }


        public override void Draw()
        {            
            ImGui.BeginTabBar("PuppetMaster Config Tabs");

            if (ImGui.BeginTabItem("Reactions"))
            {
                var configuration = Service.configuration!;
                var enabledCount = 0;
                var attentionCount = 0;
                foreach (var reaction in configuration.Reactions)
                {
                    if (reaction.Enabled)
                        enabledCount++;
                    if (GetReactionStatus(reaction).NeedsAttention)
                        attentionCount++;
                }

                ImGui.TextUnformatted($"{configuration.Reactions.Count} reactions  |  {enabledCount} enabled  |  {attentionCount} need attention");
                ImGui.SameLine();
                if (ImGui.SmallButton("Enable all"))
                    Service.SetEnabledAll(true);
                ImGui.SameLine();
                if (ImGui.SmallButton("Disable all"))
                    Service.SetEnabledAll(false);
                ImGui.SameLine();
                if (ImGui.SmallButton("Add New##ReactionAddButton"))
                {
                    Service.semaphore.WaitOne();
                    configuration.Reactions.Add(Reaction.CreateDefault(
                        commandWhitelist: configuration.DefaultCommandWhitelist,
                        commandBlacklist: configuration.DefaultCommandBlacklist,
                        allowAllCommands: configuration.DefaultAllowAllCommands,
                        motionOnly: configuration.DefaultMotionOnly,
                        enabledChannels: configuration.DefaultEnabledChannels));
                    Service.semaphore.Release();
                    SelectReaction(configuration.Reactions.Count - 1);
                    SelectReactionEditor = true;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##ReactionSearch", "Search reactions by name or trigger...", ref ReactionSearch, 100);

                var groups = PluginUiLogic.GroupReactionIndexes(configuration.Reactions);

                var visibleCount = 0;
                var individualIndexes = new List<int>();
                foreach (var group in groups)
                {
                    if (group.Value.Count == 1)
                    {
                        if (ReactionMatchesSearch(configuration.Reactions[group.Value[0]]))
                            individualIndexes.Add(group.Value[0]);
                        continue;
                    }

                    var visibleGroupIndexes = new List<int>();
                    var enabledInGroup = 0;
                    foreach (var index in group.Value)
                    {
                        var reaction = configuration.Reactions[index];
                        if (reaction.Enabled)
                            enabledInGroup++;
                        if (ReactionMatchesSearch(reaction))
                            visibleGroupIndexes.Add(index);
                    }

                    if (visibleGroupIndexes.Count == 0)
                        continue;

                    visibleCount += visibleGroupIndexes.Count;
                    if (!string.IsNullOrWhiteSpace(ReactionSearch))
                        ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                    var displayName = string.IsNullOrWhiteSpace(group.Key) ? "Unnamed" : group.Key;
                    var expanded = ImGui.CollapsingHeader(
                        $"{displayName} — {group.Value.Count} reactions, {enabledInGroup} enabled##ReactionGroup{group.Value[0]}");
                    if (expanded)
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled($"Group actions ({group.Value.Count} reactions)");
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Enable all##ReactionGroupEnable{group.Value[0]}"))
                            SetReactionGroupEnabled(group.Value, true);
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Disable all##ReactionGroupDisable{group.Value[0]}"))
                            SetReactionGroupEnabled(group.Value, false);
                        DrawReactionTable(visibleGroupIndexes, $"##ReactionGroupTable{group.Value[0]}");
                        ImGui.Unindent();
                    }
                    ImGui.Spacing();
                }

                visibleCount += individualIndexes.Count;
                if (individualIndexes.Count > 0)
                {
                    ImGui.TextUnformatted("Individual reactions");
                    DrawReactionTable(individualIndexes, "##IndividualReactionTable");
                }

                if (visibleCount == 0)
                    ImGui.TextDisabled("No reactions match the current search.");

                if (PendingReactionDuplicate >= 0 && PendingReactionDuplicate < configuration.Reactions.Count)
                {
                    var duplicateIndex = PendingReactionDuplicate;
                    PendingReactionDuplicate = -1;
                    DuplicateReaction(duplicateIndex);
                }

                DrawDeleteReactionConfirmation();

                ImGui.Spacing();
                ImGui.TextDisabled($"Configuration schema: v{configuration.Version} (current: v{ConfigVersion.CURRENT})");
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Saved settings format version. Older configurations are migrated automatically.");
                    ImGui.EndTooltip();
                }

                ImGui.EndTabItem();
            }

            var editorFlags = SelectReactionEditor ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (ImGui.BeginTabItem("Reaction Editor", editorFlags))
            {
                SelectReactionEditor = false;
                var reactions = Service.configuration!.Reactions;
                ImGui.TextUnformatted("Find reaction");
                ImGui.SetNextItemWidth(-1);
                var editorSearchChanged = ImGui.InputTextWithHint(
                    "##ReactionEditorSearch",
                    "Search reactions by name or trigger...",
                    ref ReactionEditorSearch,
                    100);

                var filteredReactionIndexes = PluginUiLogic.FilterReactionIndexes(reactions, ReactionEditorSearch);
                var filteredReactionNames = new List<string>();
                foreach (var index in filteredReactionIndexes)
                {
                    var candidate = reactions[index];
                    var trigger = candidate.UseRegex ? candidate.CustomPhrase : candidate.TriggerPhrase;
                    filteredReactionNames.Add(string.IsNullOrWhiteSpace(trigger)
                        ? candidate.Name
                        : $"{candidate.Name} — {trigger}");
                }

                if (filteredReactionIndexes.Count > 0)
                {
                    if (editorSearchChanged && !filteredReactionIndexes.Contains(CurrentReactionIndex))
                        SelectReaction(filteredReactionIndexes[0]);

                    var filteredSelection = filteredReactionIndexes.IndexOf(CurrentReactionIndex);
                    ImGui.TextUnformatted("Editing");
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##ReactEditSelector", ref filteredSelection, [.. filteredReactionNames], filteredReactionNames.Count) &&
                        filteredSelection >= 0 && filteredSelection < filteredReactionIndexes.Count)
                    {
                        SelectReaction(filteredReactionIndexes[filteredSelection]);
                    }
                }
                else
                    ImGui.TextDisabled("No reactions match the current search.");

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();

                if (filteredReactionIndexes.Count > 0 &&
                    Service.IsValidReactionIndex(Service.configuration.CurrentReactionEdit))
                {
                    var reaction = Service.configuration.Reactions[CurrentReactionIndex];
                    var status = GetReactionStatus(reaction);
                    var enabled = reaction.Enabled;
                    if (ImGui.Checkbox("Enabled##ReactionEditorEnabled", ref enabled))
                        SetReactionEnabled(reaction, enabled);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Disabled reactions do not listen for or execute matching messages.");
                        ImGui.EndTooltip();
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(status.Color, status.Label);
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{reaction.EnabledChannels.Count} channels");

                    ImGui.Spacing();
                    ImGui.TextUnformatted(reaction.Name);
                    ImGui.SameLine();
                    if (Service.configuration.Reactions.Count <= 1)
                        ImGui.BeginDisabled();
                    if (ImGui.SmallButton("Delete Reaction##ReactionEditorDelete"))
                    {
                        PendingReactionDelete = CurrentReactionIndex;
                        OpenReactionDeletePopup = true;
                    }
                    if (Service.configuration.Reactions.Count <= 1)
                        ImGui.EndDisabled();

                    ImGui.Spacing();
                    if (ImGui.BeginChild("##ReactionEditorSectionNav", new Vector2(165, 0), true))
                    {
                        if (ImGui.Selectable("Trigger & Test", ReactionEditorSection == 0))
                            ReactionEditorSection = 0;
                        if (ImGui.Selectable("Commands", ReactionEditorSection == 1))
                            ReactionEditorSection = 1;
                        if (ImGui.Selectable($"Channels ({reaction.EnabledChannels.Count})##ReactionEditorChannelSection", ReactionEditorSection == 2))
                            ReactionEditorSection = 2;
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();

                    if (ImGui.BeginChild("##ReactionEditorSectionContent", new Vector2(0, 0), true))
                    {
                        if (ReactionEditorSection == 0)
                            DrawTriggerEditor(reaction);
                        else if (ReactionEditorSection == 1)
                        {
                            ImGui.TextUnformatted("Execution Behavior");
                            ImGui.Separator();
                            var executionPolicy = (int)reaction.ExecutionPolicy;
                            ImGui.SetNextItemWidth(240);
                            if (ImGui.Combo(
                                    "Retriggers while busy",
                                    ref executionPolicy,
                                    ["Queue every trigger", "Ignore", "Queue latest trigger"],
                                    3))
                            {
                                reaction.ExecutionPolicy = (ReactionExecutionPolicy)executionPolicy;
                                ChatHandler.InvalidateReaction(reaction, false);
                                Service.configuration.Save();
                            }
                            ImGui.TextDisabled(reaction.ExecutionPolicy switch
                            {
                                ReactionExecutionPolicy.QueueEveryTrigger => "Queues every trigger while running or waiting on queued work (maximum 16 pending).",
                                ReactionExecutionPolicy.QueueLatestTrigger => "Keeps only the newest trigger while running or waiting on queued work.",
                                _ => "Discards triggers received while the reaction is running or has queued work.",
                            });
                            ImGui.Spacing();
                            var cooldownSeconds = reaction.CooldownSeconds;
                            ImGui.SetNextItemWidth(180);
                            if (ImGui.InputInt("Cooldown (seconds)", ref cooldownSeconds, 1, 10))
                            {
                                reaction.CooldownSeconds = PluginUiLogic.ClampCooldown(cooldownSeconds);
                                ChatHandler.InvalidateReaction(reaction, false);
                                Service.configuration.Save();
                            }
                            ImGui.TextDisabled("Start-to-start delay. Only one instance of this reaction can run at a time.");
                            ImGui.Spacing();

                            ImGui.TextUnformatted("Text Command Rules");
                            ImGui.Separator();
                            DrawCommandPermissionsEditor(reaction);
                            DrawEmoteBehaviorEditor(reaction);
                            ImGui.Spacing();
                            DrawCommandRulesEditor(reaction);
                        }
                        else
                            DrawChannelSelector(CurrentReactionIndex);
                    }
                    ImGui.EndChild();
                }

                DrawDeleteReactionConfirmation();
                
                ImGui.EndTabItem();
            }
        
            if (ImGui.BeginTabItem("Settings"))
            {
                var configuration = Service.configuration!;
                if (ImGui.BeginChild("##SettingsSectionNav", new Vector2(210, 0), true))
                {
                    if (ImGui.Selectable("Notifications", SettingsSection == 0))
                        SettingsSection = 0;
                    if (ImGui.Selectable("Default Command Rules", SettingsSection == 1))
                        SettingsSection = 1;
                    if (ImGui.Selectable("Default Channel Rules", SettingsSection == 2))
                        SettingsSection = 2;
                    if (ImGui.Selectable("Custom Channels", SettingsSection == 3))
                        SettingsSection = 3;
                }
                ImGui.EndChild();
                ImGui.SameLine();

                if (ImGui.BeginChild("##SettingsSectionContent", new Vector2(0, 0), true))
                {
                    if (SettingsSection == 0)
                    {
                        ImGui.TextUnformatted("Notifications");
                        ImGui.Separator();
                        var showReactionNotifications = configuration.ShowReactionNotifications;
                        if (ImGui.Checkbox("Show reaction progress notifications", ref showReactionNotifications))
                        {
                            configuration.ShowReactionNotifications = showReactionNotifications;
                            configuration.Save();
                        }
                        ImGui.TextDisabled("Shows command progress and a Cancel button while a reaction is running.");

                        var showSuppressedReactionNotifications = configuration.ShowSuppressedReactionNotifications;
                        if (ImGui.Checkbox("Notify when a reaction is suppressed", ref showSuppressedReactionNotifications))
                        {
                            configuration.ShowSuppressedReactionNotifications = showSuppressedReactionNotifications;
                            configuration.Save();
                        }
                        ImGui.TextDisabled("Shows rate-limited notices for retriggers blocked by single-flight or cooldown.");
                    }
                    else if (SettingsSection == 1)
                    {
                        ImGui.TextUnformatted("Default Command Rules");
                        ImGui.Separator();
                        ImGui.TextDisabled("Command rules copied into newly created reactions. Existing reactions are unchanged.");

                        var defaultAllowAllCommands = configuration.DefaultAllowAllCommands;
                        if (ImGui.Checkbox("Allow all text commands by default", ref defaultAllowAllCommands))
                        {
                            configuration.DefaultAllowAllCommands = defaultAllowAllCommands;
                            configuration.Save();
                        }
                        if (configuration.DefaultAllowAllCommands)
                            ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), "New reactions will permit any command that is not denied.");

                        var defaultMotionOnly = configuration.DefaultMotionOnly;
                        if (ImGui.Checkbox("Motion only for emotes by default", ref defaultMotionOnly))
                        {
                            configuration.DefaultMotionOnly = defaultMotionOnly;
                            configuration.Save();
                        }
                        ImGui.TextDisabled("Suppresses emote chat text while still playing the animation.");
                        ImGui.Spacing();

                        DrawCommandListEditor(
                            "Allowed by Default",
                            "Non-emote commands allowed by default.",
                            configuration.DefaultCommandWhitelist,
                            configuration.DefaultCommandBlacklist,
                            ref DefaultWhitelistCommandInput,
                            ref DefaultWhitelistCommandSearch,
                            "DefaultWhitelist",
                            false);
                        ImGui.Spacing();
                        DrawCommandListEditor(
                            "Denied by Default",
                            "Commands blocked by default, including emotes.",
                            configuration.DefaultCommandBlacklist,
                            configuration.DefaultCommandWhitelist,
                            ref DefaultBlacklistCommandInput,
                            ref DefaultBlacklistCommandSearch,
                            "DefaultBlacklist",
                            false);
                    }
                    else if (SettingsSection == 2)
                    {
                        ImGui.TextUnformatted("Default Channel Rules");
                        ImGui.Separator();
                        ImGui.TextDisabled("Channels copied into newly created reactions. Log-created reactions use only their source channel.");
                        ImGui.Spacing();
                        DrawChannelSelector(
                            configuration.DefaultEnabledChannels,
                            "Defaults",
                            "New reactions will start without any enabled channels.");
                    }
                    else
                    {
                        DrawCustomChannelSettings();
                    }
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Logs"))
            {
                var configuration = Service.configuration!;
                var entries = DebugLogBuffer.Snapshot();
                var debugLogTypes = configuration.DebugLogTypes;
                if (ImGui.Checkbox("Enable message logging", ref debugLogTypes))
                {
                    configuration.DebugLogTypes = debugLogTypes;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Capture game messages in this window with their log type ID, type name, and sender.");
                    ImGui.EndTooltip();
                }

                ImGui.SameLine();
                if (entries.Length == 0)
                    ImGui.BeginDisabled();
                if (ImGui.Button("Save to file"))
                {
                    try
                    {
                        var export = Service.SaveDebugLogs();
                        Service.NotificationManager.AddNotification(new Notification
                        {
                            Title = "Puppet Master",
                            Content = $"Saved {export.EntryCount} log entries.\n{export.Path}",
                            Type = NotificationType.Success,
                            InitialDuration = TimeSpan.FromSeconds(6),
                        });
                    }
                    catch (Exception exception)
                    {
                        Service.PluginLog.Error(exception, "Failed to save PuppetMaster message logs.");
                        Service.NotificationManager.AddNotification(new Notification
                        {
                            Title = "Puppet Master",
                            Content = $"Failed to save logs.\n{exception.Message}",
                            Type = NotificationType.Error,
                            InitialDuration = TimeSpan.FromSeconds(6),
                        });
                    }
                }
                if (entries.Length == 0)
                    ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                {
                    DebugLogBuffer.Clear();
                    ChatHandler.ResetDroppedMessageCount();
                }

                ImGui.SameLine();
                var droppedMessageCount = ChatHandler.DroppedMessageCount;
                var droppedRetriggerCount = ChatHandler.DroppedRetriggerCount;
                if (droppedMessageCount > 0 || droppedRetriggerCount > 0)
                {
                    ImGui.TextColored(
                        new Vector4(1f, 0.65f, 0.2f, 1f),
                        $"Queue overload: {droppedMessageCount} message(s), {droppedRetriggerCount} retrigger(s) dropped");
                }
                else
                {
                    ImGui.TextDisabled("Queue overload: none");
                }

                ImGui.Separator();

                if (!string.IsNullOrWhiteSpace(Service.LastDebugLogExportPath))
                {
                    ImGui.TextDisabled("Last saved file:");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(Service.LastDebugLogExportPath);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(Service.LastDebugLogExportPath);
                        ImGui.EndTooltip();
                    }
                }

                if (ImGui.BeginChild("##PuppetMasterMessageLog", new Vector2(0, 0), true))
                {
                    for (var index = 0; index < entries.Length; index++)
                    {
                        var entry = entries[index];
                        if (ImGui.SmallButton($"Create reaction##LogReaction{entry.ChatTypeId}_{index}"))
                            CreateReactionFromLog(entry);
                        ImGui.SameLine();
                        if (!IsOfficialChatType(entry.ChatTypeId) && !IsConfiguredCustomChannel(entry.ChatTypeId))
                        {
                            if (ImGui.SmallButton($"Add custom channel##LogCustomChannel{entry.ChatTypeId}_{index}"))
                                AddCustomChannel(entry.ChatTypeId);
                            ImGui.SameLine();
                        }
                        ImGui.TextUnformatted(entry.Text);
                    }
                }
                ImGui.EndChild();

                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
}

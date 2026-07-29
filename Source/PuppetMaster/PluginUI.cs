using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

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
        private static string ReactionEditorSearch = string.Empty;
        private static string ChannelSearch = string.Empty;
        private static string ReactionSearch = string.Empty;
        private static int PendingReactionDuplicate = -1;
        private static int PendingAllowAllReaction = -1;
        private static string WhitelistCommandInput = string.Empty;
        private static string BlacklistCommandInput = string.Empty;
        private static string WhitelistCommandSearch = string.Empty;
        private static string BlacklistCommandSearch = string.Empty;

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
            {
                Service.semaphore.WaitOne();
                reaction.Enabled = enabled;
                configuration.Save();
                Service.semaphore.Release();
            }

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
                foreach (var index in reactionIndexes)
                {
                    if (index >= 0 && index < configuration.Reactions.Count)
                        configuration.Reactions[index].Enabled = enabled;
                }
                configuration.Save();
            }
            finally
            {
                Service.semaphore.Release();
            }
        }

        private static bool ReactionMatchesSearch(Reaction reaction)
        {
            if (string.IsNullOrWhiteSpace(ReactionSearch))
                return true;
            var trigger = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
            return reaction.Name.Contains(ReactionSearch, StringComparison.OrdinalIgnoreCase) ||
                   trigger.Contains(ReactionSearch, StringComparison.OrdinalIgnoreCase);
        }

        private static void DrawReactionStatus(Reaction reaction)
        {
            var status = GetReactionStatus(reaction);
            ImGui.TextColored(status.Color, status.Label);
        }

        private static (string Label, Vector4 Color, bool NeedsAttention) GetReactionStatus(Reaction reaction)
        {
            if (!reaction.Enabled)
                return ("Disabled", new Vector4(0.65f, 0.65f, 0.65f, 1), false);

            var hasTrigger = reaction.UseRegex
                ? !string.IsNullOrWhiteSpace(reaction.CustomPhrase) && reaction.CustomRx != null
                : !string.IsNullOrWhiteSpace(reaction.TriggerPhrase) && reaction.Rx != null;

            if (!hasTrigger)
                return ("Invalid trigger", new Vector4(1f, 0.35f, 0.35f, 1), true);
            if (reaction.EnabledChannels.Count == 0)
                return ("No channels", new Vector4(1f, 0.75f, 0.2f, 1), true);
            if (reaction.AllowAllCommands)
                return ("Unsafe", new Vector4(1f, 0.55f, 0.2f, 1), true);
            return ("Ready", new Vector4(0.35f, 0.9f, 0.45f, 1), false);
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
            var reaction = Reaction.CreateDefault($"Reaction from {channelName}");
            reaction.UseRegex = true;
            reaction.CustomPhrase = $"^{Regex.Escape(entry.TriggerText)}$";
            reaction.TestInput = entry.TriggerText;
            reaction.EnabledChannels.Add(entry.ChatTypeId);

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

        private static string NormalizeCommand(string input)
        {
            input = input.Trim();
            if (input.Length == 0)
                return string.Empty;
            if (!input.StartsWith('/'))
                input = $"/{input}";
            return Service.FormatCommand(input).Main;
        }

        private static bool ContainsCommand(List<string> commands, string command)
        {
            return commands.Exists(item => item.Equals(command, StringComparison.OrdinalIgnoreCase));
        }

        private static void DrawCommandListEditor(
            string label,
            string description,
            List<string> commands,
            List<string> oppositeCommands,
            ref string input,
            ref string search,
            string id)
        {
            var configuration = Service.configuration!;
            ImGui.TextUnformatted($"{label} ({commands.Count})");
            ImGui.TextDisabled(description);

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
                        configuration.Save();
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
                var command = NormalizeCommand(input);
                if (command.Length > 0)
                {
                    oppositeCommands.RemoveAll(item => item.Equals(command, StringComparison.OrdinalIgnoreCase));
                    if (!ContainsCommand(commands, command))
                        commands.Add(command);
                    input = string.Empty;
                    configuration.Save();
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                }
            }
        }

        private static void DrawCommandRulesEditor(Reaction reaction)
        {
            ImGui.TextDisabled("Blacklist always wins. Emotes are allowed unless blacklisted.");

            DrawCommandListEditor(
                "Allowed commands",
                reaction.AllowAllCommands
                    ? "Ignored while Allow all text commands is enabled."
                    : "Non-emote commands must appear here.",
                reaction.CommandWhitelist,
                reaction.CommandBlacklist,
                ref WhitelistCommandInput,
                ref WhitelistCommandSearch,
                "Whitelist");

            ImGui.Spacing();
            DrawCommandListEditor(
                "Denied commands",
                "These commands are always blocked, including emotes.",
                reaction.CommandBlacklist,
                reaction.CommandWhitelist,
                ref BlacklistCommandInput,
                ref BlacklistCommandSearch,
                "Blacklist");
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
                Service.configuration.Save();
            }

            if (!reaction.UseRegex)
                ImGui.TextDisabled("Separate alternatives with |, for example: please do|simon says");

            var useRegex = reaction.UseRegex;
            if (ImGui.Checkbox("Use Regex", ref useRegex))
            {
                reaction.UseRegex = useRegex;
                Service.InitializeRegex(CurrentReactionIndex, true);
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
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
                    Service.configuration.Save();
                }

                var replacement = reaction.ReplaceMatch;
                ImGui.TextUnformatted("Replacement");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextMultiline("##Replacement", ref replacement, 500, new Vector2(-1, 65)))
                {
                    reaction.ReplaceMatch = replacement;
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
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

                    if (Service.IsCommandAllowed(reaction, generatedCommand.Main, out var reason))
                    {
                        allowedCount++;
                        ImGui.TextColored(new Vector4(0.35f, 0.9f, 0.45f, 1), $"Allowed  {generatedCommand}  -  {reason}");
                    }
                    else
                    {
                        blockedCount++;
                        ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1), $"Blocked  {generatedCommand}  -  {reason}");
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
                    Service.configuration!.Save();
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                }
            }

            if (reaction.AllowAllCommands)
                ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), "Any non-blacklisted text command may run for this reaction.");

            if (ImGui.BeginPopupModal("Enable Allow All?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted("This permits any text command that is not explicitly denied.");
                ImGui.TextUnformatted("Only enable it for trusted triggers and channels.");
                if (ImGui.Button("Enable") && Service.IsValidReactionIndex(PendingAllowAllReaction))
                {
                    Service.configuration!.Reactions[PendingAllowAllReaction].AllowAllCommands = true;
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

            DrawCommandRulesEditor(reaction);
        }

        private static void DrawEmoteBehaviorEditor(Reaction reaction)
        {
            var motionOnly = reaction.MotionOnly;
            if (ImGui.Checkbox("Motion only", ref motionOnly))
            {
                reaction.MotionOnly = motionOnly;
                Service.configuration!.Save();
            }
            ImGui.TextDisabled("Suppresses emote chat text while still playing the animation.");
        }

        private static void DuplicateReaction(int index)
        {
            var configuration = Service.configuration!;
            var source = configuration.Reactions[index];
            var copy = new Reaction
            {
                Enabled = false,
                Name = $"{source.Name} Copy",
                TriggerPhrase = source.TriggerPhrase,
                AllowSit = source.AllowSit,
                MotionOnly = source.MotionOnly,
                AllowAllCommands = source.AllowAllCommands,
                UseRegex = source.UseRegex,
                CustomPhrase = source.CustomPhrase,
                ReplaceMatch = source.ReplaceMatch,
                TestInput = source.TestInput,
                EnabledChannels = new List<int>(source.EnabledChannels),
                CommandWhitelist = new List<string>(source.CommandWhitelist),
                CommandBlacklist = new List<string>(source.CommandBlacklist),
            };

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
                configuration.Reactions.RemoveAt(PendingReactionDelete);
                var nextIndex = Math.Clamp(PendingReactionDelete, 0, configuration.Reactions.Count - 1);
                PendingReactionDelete = -1;
                SelectReaction(nextIndex);
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                PendingReactionDelete = -1;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private static void DrawChannelGroup(string name, int reactionIndex, IReadOnlyList<ChannelSetting> channels)
        {
            var configuration = Service.configuration!;
            var selectedChannels = configuration.Reactions[reactionIndex].EnabledChannels;
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

            if (!ImGui.CollapsingHeader($"{name} ({selectedCount}/{visibleChannels.Count})##ChannelGroup{name}"))
                return;

            if (ImGui.SmallButton($"All##ChannelGroupAll{name}"))
            {
                foreach (var channel in visibleChannels)
                {
                    if (!selectedChannels.Contains(channel.ChatType))
                        selectedChannels.Add(channel.ChatType);
                }
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"None##ChannelGroupNone{name}"))
            {
                foreach (var channel in visibleChannels)
                    selectedChannels.Remove(channel.ChatType);
                configuration.Save();
            }

            if (ImGui.BeginTable($"##ChannelTable{name}", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                for (var index = 0; index < visibleChannels.Count; index++)
                {
                    ImGui.TableNextColumn();
                    var channel = visibleChannels[index];
                    var enabled = selectedChannels.Contains(channel.ChatType);
                    if (ImGui.Checkbox($"{channel.Name}##GroupedChannel{name}{index}{channel.ChatType}", ref enabled))
                    {
                        if (enabled)
                        {
                            if (!selectedChannels.Contains(channel.ChatType))
                                selectedChannels.Add(channel.ChatType);
                        }
                        else
                        {
                            selectedChannels.Remove(channel.ChatType);
                        }
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

        private static void DrawDefaultChannelGroup(string name, int reactionIndex, int[] indexes)
        {
            var defaultChannels = Service.configuration!.EnabledChannels;
            var channels = new List<ChannelSetting>(indexes.Length);
            foreach (var index in indexes)
            {
                if (index < defaultChannels.Count)
                    channels.Add(defaultChannels[index]);
            }
            DrawChannelGroup(name, reactionIndex, channels);
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
                if (Array.FindIndex(ChatTypes, type => (int)type == channel.ChatType) < 0)
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
            var selectedChannels = configuration.Reactions[reactionIndex].EnabledChannels;

            ImGui.TextUnformatted($"Enabled Channels ({selectedChannels.Count} selected)");
            ImGui.SameLine();
            if (ImGui.SmallButton("All##AllReactionChannels"))
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
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("None##NoReactionChannels"))
            {
                selectedChannels.Clear();
                configuration.Save();
            }

            if (selectedChannels.Count == 0)
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1), "This reaction will not listen to any messages.");

            DrawSelectedChannels(reactionIndex);

            ImGui.SetNextItemWidth(-70);
            ImGui.InputTextWithHint("##ChannelSearch", "Search channels by name or ID...", ref ChannelSearch, 100);
            if (!string.IsNullOrEmpty(ChannelSearch))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Clear##ChannelSearchClear"))
                    ChannelSearch = string.Empty;
            }

            DrawDefaultChannelGroup("Common", reactionIndex, CommonChannelIndexes);
            DrawDefaultChannelGroup("Cross-world Linkshells", reactionIndex, CrossWorldLinkshellIndexes);
            DrawDefaultChannelGroup("Linkshells", reactionIndex, LinkshellIndexes);

            var advancedChannels = GetAdvancedChannels();
            if (advancedChannels.Count > 0)
                DrawChannelGroup("Advanced", reactionIndex, advancedChannels);

            var customChannels = GetCustomChannels();
            if (customChannels.Count > 0)
                DrawChannelGroup("Custom", reactionIndex, customChannels);
        }

        private static void DrawSelectedChannels(int reactionIndex)
        {
            var configuration = Service.configuration!;
            var selectedChannels = configuration.Reactions[reactionIndex].EnabledChannels;
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

            if (ImGui.BeginTable("##SelectedChannelTable", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
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
                    if (ImGui.Checkbox($"{name}##SelectedChannel{chatTypeId}_{index}", ref enabled) && !enabled)
                    {
                        selectedChannels.Remove(chatTypeId);
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
                channel.ChatType = channelID;
                configuration.Save();
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
                for (var i = 0; i < configuration.Reactions.Count; i++)
                {
                    configuration.Reactions[i].EnabledChannels.Remove(channelID);
                }
                configuration.CustomChannels.RemoveAt(index);
                configuration.Save();
                Service.semaphore.Release();
            }
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
                    configuration.Reactions.Add(Reaction.CreateDefault());
                    Service.semaphore.Release();
                    SelectReaction(configuration.Reactions.Count - 1);
                    SelectReactionEditor = true;
                }

                var showReactionNotifications = configuration.ShowReactionNotifications;
                if (ImGui.Checkbox("Show reaction progress notifications", ref showReactionNotifications))
                {
                    configuration.ShowReactionNotifications = showReactionNotifications;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Show command progress and a Cancel button while a reaction is running.");
                    ImGui.EndTooltip();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##ReactionSearch", "Search reactions by name or trigger...", ref ReactionSearch, 100);

                var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                for (var index = 0; index < configuration.Reactions.Count; index++)
                {
                    var name = configuration.Reactions[index].Name;
                    if (!groups.TryGetValue(name, out var indexes))
                    {
                        indexes = [];
                        groups.Add(name, indexes);
                    }
                    indexes.Add(index);
                }

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
                ImGui.SetNextItemWidth(450);
                var editorSearchChanged = ImGui.InputTextWithHint(
                    "##ReactionEditorSearch",
                    "Search reactions by name or trigger...",
                    ref ReactionEditorSearch,
                    100);

                var filteredReactionIndexes = new List<int>();
                var filteredReactionNames = new List<string>();
                for (var index = 0; index < reactions.Count; index++)
                {
                    var candidate = reactions[index];
                    var trigger = candidate.UseRegex ? candidate.CustomPhrase : candidate.TriggerPhrase;
                    if (!string.IsNullOrWhiteSpace(ReactionEditorSearch) &&
                        !candidate.Name.Contains(ReactionEditorSearch, StringComparison.OrdinalIgnoreCase) &&
                        !trigger.Contains(ReactionEditorSearch, StringComparison.OrdinalIgnoreCase))
                        continue;

                    filteredReactionIndexes.Add(index);
                    filteredReactionNames.Add(string.IsNullOrWhiteSpace(trigger)
                        ? candidate.Name
                        : $"{candidate.Name} — {trigger}");
                }

                if (filteredReactionIndexes.Count > 0)
                {
                    if (editorSearchChanged && !filteredReactionIndexes.Contains(CurrentReactionIndex))
                        SelectReaction(filteredReactionIndexes[0]);

                    var filteredSelection = filteredReactionIndexes.IndexOf(CurrentReactionIndex);
                    ImGui.SetNextItemWidth(450);
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

                if (Service.IsValidReactionIndex(Service.configuration.CurrentReactionEdit))
                {
                    var reaction = Service.configuration.Reactions[CurrentReactionIndex];
                    var status = GetReactionStatus(reaction);
                    ImGui.TextUnformatted(reaction.Name);
                    ImGui.SameLine();
                    ImGui.TextColored(status.Color, status.Label);
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{reaction.EnabledChannels.Count} channels");

                    ImGui.Spacing();
                    if (ImGui.BeginChild("##ReactionEditorSectionNav", new Vector2(165, 0), true))
                    {
                        if (ImGui.Selectable("Trigger & Test", ReactionEditorSection == 0))
                            ReactionEditorSection = 0;
                        if (ImGui.Selectable("Commands", ReactionEditorSection == 1))
                            ReactionEditorSection = 1;
                        if (ImGui.Selectable("Emotes", ReactionEditorSection == 2))
                            ReactionEditorSection = 2;
                        if (ImGui.Selectable($"Channels ({reaction.EnabledChannels.Count})##ReactionEditorChannelSection", ReactionEditorSection == 3))
                            ReactionEditorSection = 3;
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();

                    if (ImGui.BeginChild("##ReactionEditorSectionContent", new Vector2(0, 0), true))
                    {
                        if (ReactionEditorSection == 0)
                            DrawTriggerEditor(reaction);
                        else if (ReactionEditorSection == 1)
                            DrawCommandPermissionsEditor(reaction);
                        else if (ReactionEditorSection == 2)
                            DrawEmoteBehaviorEditor(reaction);
                        else
                            DrawChannelSelector(CurrentReactionIndex);
                    }
                    ImGui.EndChild();
                }
                
                ImGui.EndTabItem();
            }
        
            if (ImGui.BeginTabItem("Custom Channels"))
            {
                var configuration = Service.configuration!;
                ImGui.SetNextItemWidth(400);

                if (ImGui.Button("Add##CustomChannelAdd"))
                {
                    configuration.CustomChannels.Add(new ChannelSetting
                    {
                        ChatType = -1,
                        Name = "Custom",
                        Enabled = false,
                    });
                    configuration.Save();
                }

                ImGui.SameLine();
                ImGui.TextDisabled("Only undocumented numeric log types belong here; official Dalamud types are under Advanced.");
                
                ImGui.Spacing();
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
                
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Logs"))
            {
                var configuration = Service.configuration!;
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
                if (ImGui.Button("Clear"))
                    DebugLogBuffer.Clear();

                ImGui.Separator();

                if (ImGui.BeginChild("##PuppetMasterMessageLog", new Vector2(0, 0), true))
                {
                    var entries = DebugLogBuffer.Snapshot();
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

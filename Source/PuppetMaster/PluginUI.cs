using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const String Name = "Puppet Master settings";

        private static Service.ParsedTextCommand TextCommand = new();
        private static int CurrentReactionIndex;
        private static int PendingReactionDelete = -1;
        private static bool SelectReactionEditor;
        private static string ChannelSearch = string.Empty;

        private static readonly int[] CommonChannelIndexes = [16, 17, 18, 19, 20, 21, 22];
        private static readonly int[] CrossWorldLinkshellIndexes = [0, 1, 2, 3, 4, 5, 6, 7];
        private static readonly int[] LinkshellIndexes = [8, 9, 10, 11, 12, 13, 14, 15];
        private static readonly XivChatType[] ChatTypes = Enum.GetValues<XivChatType>();


        public ConfigWindow() : base(Name)
        {
            CurrentReactionIndex = Service.configuration!.CurrentReactionEdit;
            SizeConstraints = new()
            {
                MinimumSize = new Vector2(520, 500),
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
            var enabled = reaction.Enabled;
            if (ImGui.Checkbox($"##{reaction.Name}##ReactionCheckBox{index}", ref enabled))
            {
                Service.semaphore.WaitOne();
                reaction.Enabled = enabled;
                configuration.Save();
                Service.semaphore.Release();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.PushItemWidth(150);
            var reactionName = reaction.Name;
            if (ImGui.InputText($"##CustomChannelLabel##{index}", ref reactionName, 100))
            {
                Service.semaphore.WaitOne();
                reaction.Name = reactionName;
                configuration.Save();
                Service.semaphore.Release();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            if (ImGui.Button($"Edit##ReactionEdit{index}"))
            {
                SelectReaction(index);
                SelectReactionEditor = true;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Duplicate##ReactionDuplicate{index}"))
                DuplicateReaction(index);

            ImGui.SameLine();
            if (configuration.Reactions.Count <= 1)
                ImGui.BeginDisabled();
            if (ImGui.Button($"Delete##ReactionDelete{index}"))
            {
                PendingReactionDelete = index;
                ImGui.OpenPopup("Delete reaction?");
            }
            if (configuration.Reactions.Count <= 1)
                ImGui.EndDisabled();

            ImGui.SameLine();
            DrawReactionStatus(reaction);
        }

        private static void DrawReactionStatus(Reaction reaction)
        {
            if (!reaction.Enabled)
            {
                ImGui.TextColored(new Vector4(0.65f, 0.65f, 0.65f, 1), "Disabled");
                return;
            }

            var hasTrigger = reaction.UseRegex
                ? !string.IsNullOrWhiteSpace(reaction.CustomPhrase) && reaction.CustomRx != null
                : !string.IsNullOrWhiteSpace(reaction.TriggerPhrase) && reaction.Rx != null;

            if (!hasTrigger)
                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1), "Invalid trigger");
            else if (reaction.EnabledChannels.Count == 0)
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1), "No channels");
            else
                ImGui.TextColored(new Vector4(0.35f, 0.9f, 0.45f, 1), "Ready");
        }

        private static void SelectReaction(int index)
        {
            var configuration = Service.configuration!;
            CurrentReactionIndex = index;
            configuration.CurrentReactionEdit = index;
            Service.InitializeRegex(index);
            TextCommand = Service.GetTestInputCommand(index);
            configuration.Save();
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
                if (ImGui.Button($"Add##ReactionAddButton"))
                {
                    Service.semaphore.WaitOne();
                    Service.configuration!.Reactions.Add(new Reaction() { Name = "Reaction" });
                    Service.semaphore.Release();
                    SelectReaction(Service.configuration.Reactions.Count - 1);
                    SelectReactionEditor = true;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                for (var index = 0; index < Service.configuration!.Reactions.Count; index++)
                {
                    DrawReaction(index);
                }

                DrawDeleteReactionConfirmation();

                ImGui.EndTabItem();
            }

            var editorFlags = SelectReactionEditor ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (ImGui.BeginTabItem("Reaction Editor", editorFlags))
            {
                SelectReactionEditor = false;
                var reactionNames =  new List<string>{ };
                foreach (var reaction in Service.configuration!.Reactions)
                    reactionNames.Add(reaction.Name);

                ImGui.SetNextItemWidth(450);
                if (ImGui.Combo("##ReactEditSelector", ref CurrentReactionIndex, [.. reactionNames], reactionNames.Count))
                {
                    SelectReaction(CurrentReactionIndex);
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();

                if (Service.IsValidReactionIndex(Service.configuration.CurrentReactionEdit))
                {
                    ImGui.PushItemWidth(350);
                    ImGui.Indent(40);
                    ImGui.Text("Trigger");
                    ImGui.SameLine();

                    var trigger = Service.configuration.Reactions[CurrentReactionIndex].UseRegex ? Service.configuration.Reactions[CurrentReactionIndex].CustomPhrase : Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase;
                    if (ImGui.InputText("##Trigger", ref trigger, Service.configuration.MaxRegexLength))
                    {
                        Service.semaphore.WaitOne();
                        if (!Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                            Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase = trigger;
                        else
                            Service.configuration.Reactions[CurrentReactionIndex].CustomPhrase = trigger;

                        Service.InitializeRegex(CurrentReactionIndex, true);
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.configuration.Save();
                        Service.semaphore.Release();
                    }
                    if (!Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                    {
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("Separate multiple trigger phrases with |\nExample: please do|simon says");
                            ImGui.EndTooltip();
                        }
                    }

                    ImGui.Unindent(35);

                    var replaceMatch = Service.configuration.Reactions[CurrentReactionIndex].ReplaceMatch;
                    if (Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                    {
                        ImGui.Text("Replacement");
                        ImGui.SameLine();
                        if (ImGui.InputTextMultiline("##Replacement", ref replaceMatch, 500, new Vector2(350, 80)))
                        {
                            Service.semaphore.WaitOne();
                            Service.configuration.Reactions[CurrentReactionIndex].ReplaceMatch = replaceMatch;
                            Service.configuration.Save();
                            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                            Service.semaphore.Release();
                        }
                    }

                    ImGui.Indent(50);
                    ImGui.Text("Test");
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
                    
                    if (Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                    {
                        ImGui.Text($"Matched: {TextCommand.Args}");
                    }
                    
                    ImGui.Text($"Result: {TextCommand.Main}");

                    ImGui.PopItemWidth();
                    ImGui.Spacing();
                    ImGui.Spacing();
                    
                    ImGui.Separator(); //----------------------------------------------
                    
                    var useRegex = Service.configuration.Reactions[CurrentReactionIndex].UseRegex;
                    if (ImGui.Checkbox("Use Regex", ref useRegex))
                    {
                        Service.semaphore.WaitOne();
                        Service.configuration.Reactions[CurrentReactionIndex].UseRegex = useRegex;
                        Service.configuration.Save();
                        Service.InitializeRegex(CurrentReactionIndex);
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.semaphore.Release();
                    }
                    
                    if (Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("Reset"))
                        {
                            Service.semaphore.WaitOne();
                            Service.configuration.Reactions[CurrentReactionIndex].CustomPhrase = replaceMatch = Service.GetDefaultRegex(CurrentReactionIndex);
                            Service.configuration.Reactions[CurrentReactionIndex].ReplaceMatch = trigger = Service.GetDefaultReplaceMatch();
                            Service.InitializeRegex(CurrentReactionIndex, true);
                            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                            Service.configuration.Save();
                            Service.semaphore.Release();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("Initialize regex and replacement\nbased on current non-regex trigger phrase");
                            ImGui.EndTooltip();
                        }
                    }
                    
                    var allowAllCommands = Service.configuration.Reactions[CurrentReactionIndex].AllowAllCommands;
                    if (ImGui.Checkbox("Allow all text commands", ref allowAllCommands))
                    {
                        Service.semaphore.WaitOne();
                        Service.configuration.Reactions[CurrentReactionIndex].AllowAllCommands = allowAllCommands;
                        Service.configuration.Save();
                        TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        Service.semaphore.Release();
                    }
                   
                    if (!Service.configuration.Reactions[CurrentReactionIndex].UseRegex)
                    {
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text("If command has subcommands, enclose sequence in parentheses.");
                            ImGui.Text("For placeholders, replace angle brackets with square brackets.");
                            var found = Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase.IndexOf('|');
                            var firstTriggerPhrase = found == -1 ? Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase : Service.configuration.Reactions[CurrentReactionIndex].TriggerPhrase[..found];
                            ImGui.Text("Example: " + firstTriggerPhrase + " (ac \"Vercure\" [t])");
                            ImGui.EndTooltip();
                        }
                    }
                    
                    var allowSit = Service.configuration.Reactions[CurrentReactionIndex].AllowSit;
                    if (ImGui.Checkbox("Allow \"sit\" or \"groundsit\" requests", ref allowSit))
                    {
                        Service.configuration.Reactions[CurrentReactionIndex].AllowSit = allowSit;
                        Service.configuration.Save();
                    }
                    
                    var motionOnly = Service.configuration.Reactions[CurrentReactionIndex].MotionOnly;
                    if (ImGui.Checkbox("Motion only", ref motionOnly))
                    {
                        Service.configuration.Reactions[CurrentReactionIndex].MotionOnly = motionOnly;
                        Service.configuration.Save();
                    }
                    
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                    DrawChannelSelector(CurrentReactionIndex);
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

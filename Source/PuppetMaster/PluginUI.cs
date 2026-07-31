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
        private static bool ShowReactionChannels;
        private static int ReactionOptionsSection;
        private static int ChannelCategorySection;
        private static int SettingsSection;
        private static int MainSection;
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
        private static float ReplacementEditorHeight = 110;
        private static bool ColorLogEntries = true;
        private static bool AutoScrollLogs = true;
        private static long LastDisplayedLogRevision = -1;
        private static readonly Dictionary<ChannelSetting, int> CustomChannelIdDrafts = new();
        private static readonly Dictionary<ChannelSetting, (string Message, DateTime ExpiresAt)> CustomChannelValidationMessages = new();

        private static readonly int[] CommonChannelIndexes = [16, 17, 18, 19, 20, 21, 22];
        private static readonly int[] CrossWorldLinkshellIndexes = [0, 1, 2, 3, 4, 5, 6, 7];
        private static readonly int[] LinkshellIndexes = [8, 9, 10, 11, 12, 13, 14, 15];
        private static readonly XivChatType[] ChatTypes = Enum.GetValues<XivChatType>();
        private static readonly Vector4[] LogChannelColors =
        [
            new(0.45f, 0.75f, 1.00f, 1f),
            new(0.45f, 0.88f, 0.64f, 1f),
            new(0.94f, 0.68f, 0.38f, 1f),
            new(0.80f, 0.58f, 0.96f, 1f),
            new(0.96f, 0.54f, 0.64f, 1f),
            new(0.50f, 0.84f, 0.86f, 1f),
            new(0.88f, 0.84f, 0.42f, 1f),
            new(0.68f, 0.70f, 0.96f, 1f),
        ];


        public ConfigWindow() : base(Name)
        {
            CurrentReactionIndex = Service.configuration!.CurrentReactionEdit;
            SizeConstraints = new()
            {
                MinimumSize = new Vector2(1080, 600),
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

        private static bool DrawPrimaryButton(string label, string id, Vector2 size = default)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.45f, 0.68f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.52f, 0.76f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.20f, 0.40f, 0.62f, 1f));
            var clicked = ImGui.Button($"{label}##{id}", size);
            ImGui.PopStyleColor(3);
            return clicked;
        }

        private static bool DrawDangerButton(string label, string id, Vector2 size = default)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.48f, 0.20f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.62f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.42f, 0.16f, 0.16f, 1f));
            var clicked = ImGui.Button($"{label}##{id}", size);
            ImGui.PopStyleColor(3);
            return clicked;
        }

        private static bool DrawWindowButton(string label, string id)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.16f, 0.38f, 0.40f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.50f, 0.52f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.13f, 0.32f, 0.34f, 1f));
            var clicked = ImGui.Button($"{label}##{id}");
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"Open {label} in a separate window");
                ImGui.EndTooltip();
            }
            return clicked;
        }

        private static bool DrawRemoveIconButton(
            string id,
            string tooltip = "Remove",
            Vector2 size = default)
        {
            if (size == default)
                size = new Vector2(-1, 0);
            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.30f, 0.33f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.62f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.48f, 0.18f, 0.18f, 1f));
            var clicked = ImGui.Button($"{FontAwesome.Trash}##{id}", size);
            ImGui.PopStyleColor(3);
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
            return clicked;
        }

        private static bool DrawDuplicateIconButton(string id)
        {
            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.38f, 0.30f, 0.62f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.50f, 0.40f, 0.78f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.32f, 0.24f, 0.54f, 1f));
            var clicked = ImGui.Button($"{FontAwesome.Layers}##{id}", new Vector2(-1, 0));
            ImGui.PopStyleColor(3);
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Duplicate reaction");
                ImGui.EndTooltip();
            }
            return clicked;
        }

        private static void DrawWrappedDisabledText(string text)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextDisabled(text);
            ImGui.PopTextWrapPos();
        }

        private static void DrawWrappedColoredText(Vector4 color, string text)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(color, text);
            ImGui.PopTextWrapPos();
        }

        private static void DrawWrappedText(string text)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }

        private static Vector4 GetLogChannelColor(int chatTypeId)
        {
            var colorIndex = (int)((uint)chatTypeId % (uint)LogChannelColors.Length);
            return LogChannelColors[colorIndex];
        }

        private static bool DrawLogActionButton(
            string symbol,
            string id,
            string tooltip,
            Vector4 color,
            bool useIconFont = false)
        {
            if (useIconFont)
                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(color.X, color.Y, color.Z, 0.78f));
            ImGui.PushStyleColor(
                ImGuiCol.ButtonHovered,
                new Vector4(
                    Math.Min(1f, color.X + 0.12f),
                    Math.Min(1f, color.Y + 0.12f),
                    Math.Min(1f, color.Z + 0.12f),
                    1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(color.X, color.Y, color.Z, 1f));
            var frameSize = ImGui.GetFrameHeight();
            var clicked = ImGui.Button($"{symbol}##{id}", new Vector2(frameSize, frameSize));
            ImGui.PopStyleColor(3);
            if (useIconFont)
                ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
            return clicked;
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

        private static bool ReactionMatchesSearch(Reaction reaction)
        {
            return PluginUiLogic.MatchesSearch(reaction, ReactionSearch);
        }

        private static (string Label, Vector4 Color, bool NeedsAttention) GetReactionStatus(Reaction reaction)
        {
            return PluginUiLogic.GetStatus(reaction) switch
            {
                ReactionUiStatus.Disabled => ("Disabled", new Vector4(0.65f, 0.65f, 0.65f, 1), false),
                ReactionUiStatus.InvalidTrigger => ("Invalid trigger", new Vector4(1f, 0.35f, 0.35f, 1), true),
                ReactionUiStatus.NoChannels => ("No channels", new Vector4(1f, 0.75f, 0.2f, 1), true),
                ReactionUiStatus.Unsafe => ("Review commands", new Vector4(1f, 0.55f, 0.2f, 1), true),
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
            PendingAllowAllReaction = -1;
            PendingReactionDelete = -1;
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
            Reaction? reactionToInvalidate = null,
            Vector4? accentColor = null)
        {
            var configuration = Service.configuration!;
            if (accentColor.HasValue)
                ImGui.TextColored(accentColor.Value, $"{label} ({commands.Count})");
            else
                ImGui.TextUnformatted($"{label} ({commands.Count})");
            if (!string.IsNullOrWhiteSpace(description))
            {
                if (showDescriptionWarning)
                    DrawWrappedColoredText(new Vector4(1f, 0.55f, 0.2f, 1), description);
                else
                    DrawWrappedDisabledText(description);
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
            var visibleRows = Math.Clamp(visibleCommands.Length, 4, 7);
            var listHeight =
                (visibleRows * ImGui.GetFrameHeightWithSpacing()) +
                (ImGui.GetStyle().WindowPadding.Y * 2);
            if (accentColor.HasValue)
            {
                var accent = accentColor.Value;
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(accent.X, accent.Y, accent.Z, 0.72f));
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(accent.X, accent.Y, accent.Z, 0.07f));
            }
            if (ImGui.BeginChild(
                    $"##{id}List",
                    new Vector2(0, listHeight),
                    true))
            {
                if (ImGui.BeginTable(
                        $"##{id}Rows",
                        2,
                        ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 34);
                    foreach (var command in visibleCommands)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted(command);
                        ImGui.TableSetColumnIndex(1);
                        if (DrawRemoveIconButton($"{id}{command}"))
                        {
                            commands.Remove(command);
                            if (reactionToInvalidate != null)
                                ChatHandler.InvalidateReaction(reactionToInvalidate, true);
                            configuration.Save();
                            if (refreshReactionTest)
                                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                        }
                    }
                    ImGui.EndTable();
                }

                if (visibleCommands.Length == 0)
                    ImGui.TextDisabled(commands.Count == 0 ? "No commands added." : "No matching commands.");
            }
            ImGui.EndChild();
            if (accentColor.HasValue)
                ImGui.PopStyleColor(2);

            var addFromEnter = false;
            var addFromButton = false;
            if (ImGui.BeginTable(
                    $"##{id}AddRow",
                    2,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Add", ImGuiTableColumnFlags.WidthFixed, 62);
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                addFromEnter = ImGui.InputTextWithHint(
                    $"##{id}Input",
                    "/command",
                    ref input,
                    100,
                    ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.TableNextColumn();
                addFromButton = DrawPrimaryButton(
                    "Add",
                    $"{id}Add",
                    new Vector2(-1, ImGui.GetFrameHeight()));
                ImGui.EndTable();
            }
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
            if (!reaction.AllowAllCommands)
            {
                DrawCommandListEditor(
                    "Allowed commands",
                    string.Empty,
                    reaction.CommandWhitelist,
                    reaction.CommandBlacklist,
                    ref WhitelistCommandInput,
                    ref WhitelistCommandSearch,
                    "Whitelist",
                    true,
                    reactionToInvalidate: reaction,
                    accentColor: new Vector4(0.35f, 0.78f, 0.45f, 1f));
                ImGui.Spacing();
            }

            DrawCommandListEditor(
                "Blocked commands",
                string.Empty,
                reaction.CommandBlacklist,
                reaction.CommandWhitelist,
                ref BlacklistCommandInput,
                ref BlacklistCommandSearch,
                "Blacklist",
                reactionToInvalidate: reaction,
                accentColor: new Vector4(0.90f, 0.35f, 0.35f, 1f));
        }

        private static void DrawTriggerEditor(Reaction reaction)
        {
            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);

            var trigger = reaction.UseRegex ? reaction.CustomPhrase : reaction.TriggerPhrase;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint(
                    "##Trigger",
                    reaction.UseRegex ? "Regex pattern" : "Trigger phrase",
                    ref trigger,
                    Service.configuration!.MaxRegexLength))
            {
                if (reaction.UseRegex)
                    reaction.CustomPhrase = trigger;
                else
                    reaction.TriggerPhrase = trigger;
                Service.InitializeRegex(CurrentReactionIndex, true);
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration!.Save();
            }

            if (!reaction.UseRegex)
                DrawWrappedDisabledText("Separate alternatives with |, for example: please do|simon says");

            var useRegex = reaction.UseRegex;
            const string restoreRegexLabel = "Restore regex defaults";
            var restoreRegexButtonWidth = MathF.Ceiling(
                ImGui.CalcTextSize(restoreRegexLabel).X +
                (ImGui.GetStyle().FramePadding.X * 2) +
                (ImGui.GetStyle().CellPadding.X * 2));
            if (ImGui.BeginTable(
                    "##RegexModeRow",
                    2,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(
                    "Restore",
                    ImGuiTableColumnFlags.WidthFixed,
                    reaction.UseRegex ? restoreRegexButtonWidth : 1f);
                ImGui.TableNextColumn();
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.34f, 0.52f, 0.72f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.24f, 0.46f, 0.68f, 0.88f));
                ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.45f, 0.76f, 1f, 1f));
                if (ImGui.Checkbox("Use Regex", ref useRegex))
                {
                    PluginUiLogic.SetRegexMode(reaction, useRegex);
                    Service.InitializeRegex(CurrentReactionIndex, true);
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    ChatHandler.InvalidateReaction(reaction, false);
                    Service.configuration!.Save();
                }
                ImGui.PopStyleColor(3);

                ImGui.TableNextColumn();
                var restoreRegexDefaults = false;
                if (reaction.UseRegex)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.52f, 0.36f, 0.14f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.68f, 0.48f, 0.18f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.44f, 0.30f, 0.10f, 1f));
                    restoreRegexDefaults = ImGui.Button(
                        $"{restoreRegexLabel}##ReactionRegexReset",
                        new Vector2(-1, ImGui.GetFrameHeight()));
                    ImGui.PopStyleColor(3);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Restores the default regex pattern and replacement.");
                        ImGui.EndTooltip();
                    }
                }
                if (restoreRegexDefaults)
                {
                    PluginUiLogic.EnsureRegexRestoreTrigger(reaction);
                    reaction.CustomPhrase = Service.GetDefaultRegex(CurrentReactionIndex);
                    reaction.ReplaceMatch = Service.GetDefaultReplaceMatch();
                    Service.InitializeRegex(CurrentReactionIndex, true);
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    ChatHandler.InvalidateReaction(reaction, false);
                    Service.configuration.Save();
                }
                ImGui.EndTable();
            }

            if (reaction.UseRegex)
            {
                var replacement = reaction.ReplaceMatch;
                ImGui.TextUnformatted("Replacement");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextMultiline("##Replacement", ref replacement, 500, new Vector2(-1, ReplacementEditorHeight)))
                {
                    reaction.ReplaceMatch = replacement;
                    TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                    ChatHandler.InvalidateReaction(reaction, false);
                    Service.configuration.Save();
                }
                var gripPosition = ImGui.GetCursorScreenPos();
                var gripSize = new Vector2(ImGui.GetContentRegionAvail().X, 14);
                ImGui.InvisibleButton("##ReplacementResize", gripSize);
                var gripColor = ImGui.IsItemActive()
                    ? new Vector4(0.36f, 0.68f, 0.96f, 1f)
                    : ImGui.IsItemHovered()
                        ? new Vector4(0.42f, 0.66f, 0.88f, 0.95f)
                        : new Vector4(0.52f, 0.55f, 0.60f, 0.72f);
                var gripCenterX = gripPosition.X + (gripSize.X * 0.5f);
                var drawList = ImGui.GetWindowDrawList();
                for (var line = 0; line < 3; line++)
                {
                    var lineY = gripPosition.Y + 4 + (line * 3);
                    drawList.AddLine(
                        new Vector2(gripCenterX - 12, lineY),
                        new Vector2(gripCenterX + 12, lineY),
                        ImGui.GetColorU32(gripColor),
                        1.5f);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Drag to resize");
                    ImGui.EndTooltip();
                }
                if (ImGui.IsItemActive())
                    ReplacementEditorHeight = Math.Clamp(
                        ReplacementEditorHeight + ImGui.GetIO().MouseDelta.Y,
                        65,
                        420);
            }

        }

        private static void DrawReactionTest(Reaction reaction)
        {
            TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
            var testInput = reaction.TestInput;
            ImGui.TextUnformatted("Example chat message");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint(
                    "##TestInput",
                    "Type a message as it would appear in chat...",
                    ref testInput,
                    500))
            {
                reaction.TestInput = testInput;
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
                Service.configuration!.Save();
            }

            if (!string.IsNullOrWhiteSpace(testInput) && string.IsNullOrWhiteSpace(TextCommand.Main))
                ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), "No match");
            else if (!string.IsNullOrWhiteSpace(testInput))
            {
                var generatedLines = TextCommand.Main.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
                var allowedCount = 0;
                var blockedCount = 0;
                ImGui.TextUnformatted("Resulting commands");
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
                        DrawWrappedColoredText(allowedColor, generatedCommand.ToString());
                    }
                    else
                    {
                        blockedCount++;
                        var blockedColor = new Vector4(1f, 0.35f, 0.35f, 1);
                        FontAwesome.Print(blockedColor, FontAwesome.Cross);
                        ImGui.SameLine();
                        DrawWrappedColoredText(blockedColor, generatedCommand.ToString());
                    }
                }

                if (blockedCount == 0)
                    ImGui.TextColored(new Vector4(0.35f, 0.9f, 0.45f, 1), $"All {allowedCount} commands will run.");
                else
                    ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1), $"{allowedCount} allowed, {blockedCount} blocked.");
                if (reaction.UseRegex)
                    ImGui.TextDisabled($"Matched: {TextCommand.Args}");
            }
        }

        private static void DrawCommandPermissionsEditor(Reaction reaction)
        {
            ImGui.TextUnformatted("Which commands can run?");
            if (ImGui.RadioButton("Only the commands listed below", !reaction.AllowAllCommands) && reaction.AllowAllCommands)
            {
                reaction.AllowAllCommands = false;
                ChatHandler.InvalidateReaction(reaction, true);
                Service.configuration!.Save();
                TextCommand = Service.GetTestInputCommand(CurrentReactionIndex);
            }
            if (ImGui.RadioButton("Any command except those blocked", reaction.AllowAllCommands) && !reaction.AllowAllCommands)
                PendingAllowAllReaction = CurrentReactionIndex;

            if (PendingAllowAllReaction == CurrentReactionIndex && !reaction.AllowAllCommands)
            {
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0.55f, 0.2f, 0.80f));
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1f, 0.55f, 0.2f, 0.08f));
                const string confirmationText =
                    "Any text command could run unless blocked. Use this only with triggers and channels you trust.";
                var confirmationWrapWidth = Math.Max(
                    120,
                    ImGui.GetContentRegionAvail().X - (ImGui.GetStyle().WindowPadding.X * 2));
                var confirmationHeight = PluginUiLogic.CalculateWrappedPanelHeight(
                    ImGui.CalcTextSize(confirmationText, false, confirmationWrapWidth).Y,
                    ImGui.GetStyle().WindowPadding.Y + 2,
                    ImGui.GetStyle().ItemSpacing.Y,
                    ImGui.GetFrameHeight());
                ImGui.BeginChild(
                    "##AllowAllConfirmation",
                    new Vector2(0, confirmationHeight),
                    true,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                ImGui.TextWrapped(confirmationText);
                if (DrawPrimaryButton("Enable", "ConfirmAllowAll") && Service.IsValidReactionIndex(PendingAllowAllReaction))
                {
                    var pendingReaction = Service.configuration!.Reactions[PendingAllowAllReaction];
                    pendingReaction.AllowAllCommands = true;
                    ChatHandler.InvalidateReaction(pendingReaction, true);
                    Service.configuration.Save();
                    TextCommand = Service.GetTestInputCommand(PendingAllowAllReaction);
                    PendingAllowAllReaction = -1;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                    PendingAllowAllReaction = -1;
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }
        }

        private static void DrawEmoteBehaviorEditor(Reaction reaction)
        {
            var motionOnly = reaction.MotionOnly;
            if (ImGui.Checkbox("Hide emote text", ref motionOnly))
            {
                reaction.MotionOnly = motionOnly;
                ChatHandler.InvalidateReaction(reaction, true);
                Service.configuration!.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("The animation still plays, but its emote message is hidden.");
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
        }

        private static void DrawDeleteReactionConfirmation()
        {
            var configuration = Service.configuration!;
            var isValid = PendingReactionDelete >= 0 && PendingReactionDelete < configuration.Reactions.Count;
            if (!isValid)
                return;

            var reactionName = isValid ? configuration.Reactions[PendingReactionDelete].Name : "this reaction";
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.90f, 0.35f, 0.35f, 0.80f));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.90f, 0.35f, 0.35f, 0.08f));
            var confirmationText = $"Delete '{reactionName}'? This cannot be undone.";
            var confirmationWrapWidth = Math.Max(
                160,
                ImGui.GetContentRegionAvail().X - (ImGui.GetStyle().WindowPadding.X * 2));
            var confirmationHeight = PluginUiLogic.CalculateWrappedPanelHeight(
                ImGui.CalcTextSize(confirmationText, false, confirmationWrapWidth).Y,
                ImGui.GetStyle().WindowPadding.Y + 2,
                ImGui.GetStyle().ItemSpacing.Y,
                ImGui.GetFrameHeight());
            ImGui.BeginChild(
                "##DeleteReactionConfirmation",
                new Vector2(0, confirmationHeight),
                true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.TextWrapped(confirmationText);

            if (DrawDangerButton("Delete", "ConfirmReactionDelete") && configuration.Reactions.Count > 1)
            {
                if (PluginUiLogic.TryDeleteReaction(
                        configuration.Reactions,
                        PendingReactionDelete,
                        out var nextIndex,
                        ChatHandler.CancelReaction))
                {
                    PendingReactionDelete = -1;
                    SelectReaction(nextIndex);
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                PendingReactionDelete = -1;
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
        }

        private static void DrawChannelGroup(
            string name,
            List<int> selectedChannels,
            string idSuffix,
            IReadOnlyList<ChannelSetting> channels,
            Action? onChanged = null,
            bool showHeading = true)
        {
            var configuration = Service.configuration!;
            var visibleChannels = new List<ChannelSetting>();
            foreach (var channel in channels)
            {
                var matchesSearch =
                    string.IsNullOrWhiteSpace(ChannelSearch) ||
                    channel.Name.Contains(ChannelSearch, StringComparison.OrdinalIgnoreCase) ||
                    channel.ChatType.ToString().Contains(ChannelSearch, StringComparison.OrdinalIgnoreCase);
                if (matchesSearch)
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

            if (showHeading)
            {
                ImGui.Spacing();
                ImGui.TextUnformatted($"{name} ({selectedCount}/{visibleChannels.Count})");
                ImGui.Separator();
            }

            var channelActionSpacing = ImGui.GetStyle().ItemSpacing.X;
            var channelActionWidth = (ImGui.GetContentRegionAvail().X - channelActionSpacing) * 0.5f;
            if (DrawPrimaryButton(
                    "Select all",
                    $"ChannelGroupAll{idSuffix}{name}",
                    new Vector2(channelActionWidth, 0)))
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
            if (ImGui.Button(
                    $"Clear##ChannelGroupNone{idSuffix}{name}",
                    new Vector2(channelActionWidth, 0)))
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

        private static void DrawDefaultChannelGroup(
            string name,
            List<int> selectedChannels,
            string idSuffix,
            int[] indexes,
            Action? onChanged = null)
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

        private static List<ChannelSetting> GetDefaultChannels(int[] indexes)
        {
            var defaultChannels = Service.configuration!.EnabledChannels;
            var channels = new List<ChannelSetting>(indexes.Length);
            foreach (var index in indexes)
            {
                if (index < defaultChannels.Count)
                {
                    var channel = defaultChannels[index];
                    channels.Add(new ChannelSetting
                    {
                        ChatType = channel.ChatType,
                        Name = Enum.GetName(typeof(XivChatType), (ushort)channel.ChatType) ?? channel.Name,
                    });
                }
            }
            return channels;
        }

        private static int CountSelectedChannels(
            IReadOnlyList<ChannelSetting> channels,
            IReadOnlyCollection<int> selectedChannels)
        {
            var count = 0;
            foreach (var channel in channels)
            {
                if (selectedChannels.Contains(channel.ChatType))
                    count++;
            }
            return count;
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

        private static void DrawChannelSelector(int reactionIndex, bool compact = false)
        {
            var configuration = Service.configuration!;
            var reaction = configuration.Reactions[reactionIndex];
            DrawChannelSelector(
                reaction.EnabledChannels,
                $"Reaction{reactionIndex}",
                "This reaction will not listen to any messages.",
                () => ChatHandler.InvalidateReaction(reaction, false),
                compact);
        }

        private static void DrawChannelSelector(
            List<int> selectedChannels,
            string idSuffix,
            string emptyMessage,
            Action? onChanged = null,
            bool compact = false,
            bool showSelectedChannels = true)
        {
            var commonChannels = GetDefaultChannels(CommonChannelIndexes);
            var crossWorldLinkshells = GetDefaultChannels(CrossWorldLinkshellIndexes);
            var linkshells = GetDefaultChannels(LinkshellIndexes);
            var advancedChannels = GetAdvancedChannels();
            var customChannels = GetCustomChannels();

            if (showSelectedChannels)
                DrawSelectedChannels(selectedChannels, $"{idSuffix}Active", onChanged, maxVisible: 9);

            if (!compact && selectedChannels.Count == 0)
                DrawWrappedColoredText(new Vector4(1f, 0.75f, 0.2f, 1), emptyMessage);

            var categoryLabels = PluginUiLogic.ChannelCategoryLabels;
            var additionalGroups = PluginUiLogic.AdditionalChannelCategoryLabels
                .Select(label => (IReadOnlyList<ChannelSetting>)advancedChannels
                    .Where(channel => PluginUiLogic.GetAdvancedChannelCategory(channel.Name) == label)
                    .ToList())
                .ToArray();
            IReadOnlyList<ChannelSetting>[] categories =
            [
                commonChannels,
                crossWorldLinkshells,
                linkshells,
                .. additionalGroups,
                customChannels,
            ];
            if (ImGui.BeginChild("##ChannelCategoryRail", new Vector2(155, 0), true))
            {
                for (var index = 0; index < categoryLabels.Length; index++)
                {
                    var selectedCount = CountSelectedChannels(categories[index], selectedChannels);
                    var label = $"{categoryLabels[index]}  {selectedCount}/{categories[index].Count}";
                    if (ImGui.Selectable(
                            $"{label}##ChannelCategory{idSuffix}{index}",
                            ChannelCategorySection == index))
                    {
                        ChannelCategorySection = index;
                    }
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();

            var activeCategory = categories[ChannelCategorySection];
            if (ImGui.BeginChild("##ChannelCategoryContent", new Vector2(0, 0), true))
            {
                if (ImGui.BeginTable(
                        $"##ChannelSearchRow{idSuffix}",
                        2,
                        ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableSetupColumn("Search", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Clear", ImGuiTableColumnFlags.WidthFixed, 62);
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextWithHint(
                        $"##ChannelSearch{idSuffix}",
                        "Search channels by name or ID...",
                        ref ChannelSearch,
                        100);
                    ImGui.TableNextColumn();
                    var searchIsEmpty = string.IsNullOrEmpty(ChannelSearch);
                    if (searchIsEmpty)
                        ImGui.BeginDisabled();
                    if (ImGui.Button(
                            $"Clear##ChannelSearchClear{idSuffix}",
                            new Vector2(-1, ImGui.GetFrameHeight())))
                        ChannelSearch = string.Empty;
                    if (searchIsEmpty)
                        ImGui.EndDisabled();
                    ImGui.EndTable();
                }

                if (!string.IsNullOrWhiteSpace(ChannelSearch))
                {
                    var searchChannels = commonChannels
                        .Concat(crossWorldLinkshells)
                        .Concat(linkshells)
                        .Concat(advancedChannels)
                        .Concat(customChannels)
                        .GroupBy(channel => channel.ChatType)
                        .Select(group => group.First())
                        .ToList();
                    DrawChannelGroup("Search results", selectedChannels, idSuffix, searchChannels, onChanged);
                }
                else if (activeCategory.Count == 0)
                {
                    ImGui.TextDisabled("No channels in this category.");
                }
                else
                {
                    DrawChannelGroup(
                        categoryLabels[ChannelCategorySection],
                        selectedChannels,
                        idSuffix,
                        activeCategory,
                        onChanged,
                        false);
                }
            }
            ImGui.EndChild();
        }

        private static void DrawSelectedChannels(
            List<int> selectedChannels,
            string idSuffix,
            Action? onChanged = null,
            string? headerActionLabel = null,
            Action? headerAction = null,
            int maxVisible = int.MaxValue,
            string headingLabel = "Active channels",
            string? description = null,
            Vector4? accentColor = null,
            int columns = 3)
        {
            var configuration = Service.configuration!;

            var availableChannels = new Dictionary<int, string>();
            foreach (var channel in configuration.EnabledChannels)
            {
                var canonicalName = Enum.GetName(typeof(XivChatType), (ushort)channel.ChatType);
                availableChannels.TryAdd(channel.ChatType, canonicalName ?? channel.Name);
            }
            foreach (var channel in GetAdvancedChannels())
                availableChannels.TryAdd(channel.ChatType, channel.Name);
            foreach (var channel in GetCustomChannels())
                availableChannels.TryAdd(channel.ChatType, channel.Name);

            ImGui.Spacing();
            if (accentColor.HasValue)
                ImGui.TextColored(accentColor.Value, $"●  {headingLabel} ({selectedChannels.Count})");
            else
                ImGui.TextUnformatted($"{headingLabel} ({selectedChannels.Count})");
            if (headerActionLabel != null && headerAction != null)
            {
                ImGui.SameLine();
                var actionWidth =
                    ImGui.CalcTextSize(headerActionLabel).X +
                    (ImGui.GetStyle().FramePadding.X * 2);
                var rightAlignedX = ImGui.GetWindowContentRegionMax().X - actionWidth;
                if (rightAlignedX > ImGui.GetCursorPosX())
                    ImGui.SetCursorPosX(rightAlignedX);
                var actionClicked = headerActionLabel.StartsWith("Edit", StringComparison.Ordinal)
                    ? DrawPrimaryButton(headerActionLabel, $"SelectedChannelAction{idSuffix}")
                    : ImGui.Button($"{headerActionLabel}##SelectedChannelAction{idSuffix}");
                if (actionClicked)
                    headerAction();
            }
            if (accentColor.HasValue)
            {
                var accent = accentColor.Value;
                ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(accent.X, accent.Y, accent.Z, 0.72f));
            }
            ImGui.Separator();
            if (accentColor.HasValue)
                ImGui.PopStyleColor();
            if (!string.IsNullOrWhiteSpace(description))
                DrawWrappedDisabledText(description);

            if (ImGui.BeginTable(
                    $"##SelectedChannelTable{idSuffix}",
                    columns,
                    ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                var selectedSnapshot = selectedChannels.Take(maxVisible).ToArray();
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
            var hiddenCount = selectedChannels.Count - Math.Min(selectedChannels.Count, maxVisible);
            if (hiddenCount > 0)
                ImGui.TextDisabled($"+ {hiddenCount} more active");

            ImGui.Spacing();
            ImGui.Separator();
        }

        private static void DrawCustomChannels(int index)
        {
            var configuration = Service.configuration!;
            var channel = configuration.CustomChannels[index];
            if (!CustomChannelIdDrafts.TryGetValue(channel, out var channelID))
            {
                channelID = channel.ChatType;
                CustomChannelIdDrafts[channel] = channelID;
            }
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt($"##CustomChannelID##{index}", ref channelID))
                CustomChannelIdDrafts[channel] = channelID;

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                var validationError = ValidateCustomChannelId(channel, channelID);
                if (validationError != null)
                {
                    CustomChannelIdDrafts[channel] = channel.ChatType;
                    CustomChannelValidationMessages[channel] =
                        (validationError, DateTime.UtcNow.AddSeconds(4));
                }
                else
                {
                    CustomChannelValidationMessages.Remove(channel);
                    Service.semaphore.WaitOne();
                    try
                    {
                        var previousChannelId = channel.ChatType;
                        channel.ChatType = channelID;
                        CustomChannelIdDrafts[channel] = channelID;
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

            if (DrawRemoveIconButton(
                    $"CustomChannelDelete{index}",
                    "Delete channel",
                    new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight())))
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
                    CustomChannelIdDrafts.Remove(channel);
                    CustomChannelValidationMessages.Remove(channel);
                    configuration.Save();
                }
                finally
                {
                    Service.semaphore.Release();
                }
            }

            if (CustomChannelValidationMessages.TryGetValue(channel, out var validationMessage))
            {
                if (DateTime.UtcNow < validationMessage.ExpiresAt)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 88);
                    DrawWrappedColoredText(new Vector4(1f, 0.35f, 0.35f, 1f), validationMessage.Message);
                }
                else
                {
                    CustomChannelValidationMessages.Remove(channel);
                }
            }

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
            ImGui.TextUnformatted("Custom channels");
            ImGui.Separator();
            if (DrawPrimaryButton("Add channel", "CustomChannelAdd"))
            {
                var channel = new ChannelSetting
                {
                    ChatType = -1,
                    Name = "Custom",
                    Enabled = false,
                };
                configuration.CustomChannels.Add(channel);
                configuration.Save();
            }

            ImGui.SameLine();
            DrawWrappedDisabledText("Add channel IDs found through message logging but not included in the standard list.");
            ImGui.Spacing();

            var customChannelCount = 0;
            for (var index = 0; index < configuration.CustomChannels.Count; ++index)
            {
                var channel = configuration.CustomChannels[index];
                if (PluginUiLogic.ShouldShowCustomChannel(
                        channel,
                        IsOfficialChatType,
                        id => Enum.GetName(typeof(XivChatType), (ushort)id)))
                {
                    customChannelCount++;
                    DrawCustomChannels(index);
                }
            }

            if (customChannelCount == 0)
                ImGui.TextDisabled("No custom channels configured.");
        }

        private static void DrawRepeatBehavior(Reaction reaction)
        {
            var executionPolicy = reaction.ExecutionPolicy;
            ImGui.TextUnformatted("If another message arrives while busy");
            var policyChanged = false;
            var policyGroupHeight =
                (PluginUiLogic.ExecutionPolicyOptions.Length * ImGui.GetFrameHeightWithSpacing()) +
                (ImGui.GetStyle().WindowPadding.Y * 2);
            if (ImGui.BeginChild(
                    "##ReactionExecutionPolicy",
                    new Vector2(0, policyGroupHeight),
                    true,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                for (var index = 0; index < PluginUiLogic.ExecutionPolicyOptions.Length; index++)
                {
                    var option = PluginUiLogic.ExecutionPolicyOptions[index];
                    if (ImGui.RadioButton(
                            $"{option.Label}##ReactionExecutionPolicy{option.Policy}",
                            executionPolicy == option.Policy))
                    {
                        executionPolicy = option.Policy;
                        policyChanged = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(
                            PluginUiLogic.GetExecutionPolicyDescription(option.Policy));
                        ImGui.EndTooltip();
                    }
                }
            }
            ImGui.EndChild();
            if (policyChanged)
            {
                reaction.ExecutionPolicy = executionPolicy;
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration!.Save();
            }

            DrawSectionGap(0.5f);
            var cooldownSeconds = reaction.CooldownSeconds;
            ImGui.TextUnformatted("Cooldown");
            ImGui.SetNextItemWidth(-1);
            var ignoresCooldown = PluginUiLogic.IgnoresCooldown(reaction.ExecutionPolicy);
            if (ignoresCooldown)
                ImGui.BeginDisabled();
            if (ImGui.InputInt("##ReactionCooldown", ref cooldownSeconds, 1, 10))
            {
                reaction.CooldownSeconds = PluginUiLogic.ClampCooldown(cooldownSeconds);
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration!.Save();
            }
            if (ignoresCooldown)
                ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(PluginUiLogic.GetCooldownDescription(reaction.ExecutionPolicy));
                ImGui.EndTooltip();
            }
        }

        private static bool DrawNotificationTableRow(string label, string id, ref int value)
        {
            var changed = false;
            var labels = PluginUiLogic.NotificationSettingLabels;
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(label);
            ImGui.PopTextWrapPos();
            for (var index = 0; index < labels.Length; index++)
            {
                ImGui.TableSetColumnIndex(index + 1);
                var cellWidth = ImGui.GetContentRegionAvail().X;
                var radioWidth = ImGui.GetFrameHeight();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (cellWidth - radioWidth) * 0.5f));
                if (ImGui.RadioButton($"##{id}{index}", value == index))
                {
                    value = index;
                    changed = true;
                }
            }
            return changed;
        }

        private static void DrawReactionNotifications(Reaction reaction)
        {
            var labels = PluginUiLogic.NotificationSettingLabels;
            var progressNotifications = (int)reaction.ProgressNotifications;
            var suppressedNotifications = (int)reaction.SuppressedNotifications;
            var progressChanged = false;
            var suppressedChanged = false;
            if (ImGui.BeginTable(
                    "##ReactionNotifications",
                    4,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn("##NotificationType", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(labels[(int)ReactionNotificationSetting.Inherit], ImGuiTableColumnFlags.WidthFixed, 54);
                ImGui.TableSetupColumn(labels[(int)ReactionNotificationSetting.Enabled], ImGuiTableColumnFlags.WidthFixed, 42);
                ImGui.TableSetupColumn(labels[(int)ReactionNotificationSetting.Disabled], ImGuiTableColumnFlags.WidthFixed, 42);
                ImGui.TableHeadersRow();
                progressChanged = DrawNotificationTableRow(
                    "Progress and completion",
                    "ReactionProgressNotifications",
                    ref progressNotifications);
                suppressedChanged = DrawNotificationTableRow(
                    "Ignored messages",
                    "ReactionIgnoredNotifications",
                    ref suppressedNotifications);
                ImGui.EndTable();
            }

            if (progressChanged)
            {
                reaction.ProgressNotifications = (ReactionNotificationSetting)progressNotifications;
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration!.Save();
            }

            if (suppressedChanged)
            {
                reaction.SuppressedNotifications = (ReactionNotificationSetting)suppressedNotifications;
                ChatHandler.InvalidateReaction(reaction, false);
                Service.configuration!.Save();
            }
        }

        private static void DrawWorkspaceHeading(string label)
        {
            ImGui.TextUnformatted(label);
            ImGui.Separator();
        }

        private static void DrawEditorSectionHeading(string label, string? description, Vector4 accent)
        {
            ImGui.Spacing();
            ImGui.TextColored(accent, $"●  {label}");
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(accent.X, accent.Y, accent.Z, 0.72f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            if (!string.IsNullOrWhiteSpace(description))
                DrawWrappedDisabledText(description);
        }

        private static void DrawSectionGap(float scale = 1f)
        {
            ImGui.Dummy(new Vector2(
                0,
                MathF.Ceiling(ImGui.GetStyle().ItemSpacing.Y * scale)));
        }

        private static void DrawMainToolbar()
        {
            if (MainSection == 0)
            {
                DrawPrimaryButton("Reactions", "MainReactions");
            }
            else if (ImGui.Button("Reactions##MainReactions"))
            {
                MainSection = 0;
            }

            ImGui.SameLine();
            if (MainSection == 1)
            {
                DrawPrimaryButton("Settings", "MainSettings");
            }
            else if (ImGui.Button("Settings##MainSettings"))
            {
                MainSection = 1;
            }

            ImGui.SameLine();
            ImGui.TextDisabled("|  Open window:");
            ImGui.SameLine();
            if (DrawWindowButton("Visualizer", "OpenVisualizer"))
                Service.plugin!.DrawVisualizerUI();
            ImGui.SameLine();
            if (DrawWindowButton("Logs", "OpenLogs"))
                Service.plugin!.DrawLogsUI();

            ImGui.Separator();
        }

        private static void DrawUnifiedReactionsTab()
        {
            var configuration = Service.configuration!;
            if (configuration.Reactions.Count == 0 ||
                CurrentReactionIndex < 0 || CurrentReactionIndex >= configuration.Reactions.Count)
                SelectReaction(PluginUiLogic.EnsureReactionSelection(configuration, configuration.CurrentReactionEdit));

            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var workspaceLayout = PluginUiLogic.CalculateThreeColumnLayout(
                ImGui.GetContentRegionAvail().X,
                spacing);
            var listWidth = workspaceLayout.ListWidth;
            var middleWidth = workspaceLayout.EditorWidth;
            var optionsWidth = workspaceLayout.OptionsWidth;

            if (ImGui.BeginChild(
                    "##ReactionListPanel",
                    new Vector2(listWidth, 0),
                    true,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (DrawPrimaryButton(
                        "New reaction",
                        "ReactionAddButton",
                        new Vector2(-1, ImGui.GetFrameHeight())))
                {
                    var reaction = Reaction.CreateDefault(
                        commandWhitelist: configuration.DefaultCommandWhitelist,
                        commandBlacklist: configuration.DefaultCommandBlacklist,
                        allowAllCommands: configuration.DefaultAllowAllCommands,
                        motionOnly: configuration.DefaultMotionOnly,
                        enabledChannels: configuration.DefaultEnabledChannels);
                    configuration.Reactions.Add(reaction);
                    SelectReaction(configuration.Reactions.Count - 1);
                }

                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##ReactionSearch", "Search reactions...", ref ReactionSearch, 100);
                ImGui.Separator();

                if (ImGui.BeginChild("##ReactionListScroll", new Vector2(0, 0), false))
                {
                    var visibleCount = 0;
                    if (ImGui.BeginTable(
                            "##ReactionList",
                            3,
                            ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                    {
                        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 18);
                        ImGui.TableSetupColumn("Reaction", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 24);
                        for (var index = 0; index < configuration.Reactions.Count; index++)
                        {
                            var reaction = configuration.Reactions[index];
                            if (!ReactionMatchesSearch(reaction))
                                continue;
                            visibleCount++;

                            var displayName = string.IsNullOrWhiteSpace(reaction.Name) ? "Unnamed reaction" : reaction.Name;
                            var status = GetReactionStatus(reaction);
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.AlignTextToFramePadding();
                            ImGui.TextColored(status.Color, "●");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted(status.Label);
                                ImGui.EndTooltip();
                            }
                            ImGui.TableSetColumnIndex(1);
                            if (ImGui.Selectable(
                                    $"{displayName}##ReactionSelect{index}",
                                    CurrentReactionIndex == index))
                                SelectReaction(index);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted(displayName);
                                ImGui.EndTooltip();
                            }
                            ImGui.TableSetColumnIndex(2);
                            var reactionEnabled = reaction.Enabled;
                            if (ImGui.Checkbox($"##ReactionEnabled{index}", ref reactionEnabled))
                                SetReactionEnabled(reaction, reactionEnabled);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted(reaction.Enabled ? "Disable reaction" : "Enable reaction");
                                ImGui.EndTooltip();
                            }
                        }
                        ImGui.EndTable();
                    }

                    if (visibleCount == 0)
                        ImGui.TextDisabled("No matching reactions.");
                }
                ImGui.EndChild();
            }
            ImGui.EndChild();
            ImGui.SameLine();

            if (!ImGui.BeginChild(
                    "##ReactionSetupPanel",
                    new Vector2(middleWidth, 0),
                    true,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.EndChild();
                return;
            }

            if (configuration.Reactions.Count == 0)
            {
                ImGui.TextDisabled("Create a reaction to get started.");
                ImGui.EndChild();
                return;
            }

            var current = configuration.Reactions[CurrentReactionIndex];
            var name = current.Name;
            if (ImGui.BeginTable(
                    "##ReactionHeader",
                    3,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Duplicate", ImGuiTableColumnFlags.WidthFixed, 34);
                ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 34);
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##ReactionName", ref name, 100))
                {
                    current.Name = name;
                    configuration.Save();
                }
                ImGui.TableNextColumn();
                if (DrawDuplicateIconButton("ReactionDuplicate"))
                    PendingReactionDuplicate = CurrentReactionIndex;
                ImGui.TableNextColumn();
                if (configuration.Reactions.Count <= 1)
                    ImGui.BeginDisabled();
                if (DrawRemoveIconButton("ReactionDelete", "Delete reaction"))
                    PendingReactionDelete = CurrentReactionIndex;
                if (configuration.Reactions.Count <= 1)
                    ImGui.EndDisabled();
                ImGui.EndTable();
            }
            DrawDeleteReactionConfirmation();

            if (ImGui.BeginChild("##ReactionEditorScroll", new Vector2(0, 0), false))
            {
                DrawEditorSectionHeading(
                    "Listen for",
                    null,
                    new Vector4(0.32f, 0.62f, 0.95f, 1f));
                DrawTriggerEditor(current);

                DrawSelectedChannels(
                    current.EnabledChannels,
                    $"ReactionSummary{CurrentReactionIndex}",
                    () => ChatHandler.InvalidateReaction(current, false),
                    ShowReactionChannels ? "Hide channels" : "Edit channels",
                    () => ShowReactionChannels = !ShowReactionChannels,
                    6,
                    "In channels",
                    accentColor: new Vector4(0.25f, 0.78f, 0.82f, 1f));
                if (current.EnabledChannels.Count == 0)
                    DrawWrappedColoredText(new Vector4(1f, 0.75f, 0.2f, 1), "Choose at least one channel.");

                DrawEditorSectionHeading(
                    PluginUiLogic.ReactionWorkspaceSectionLabels[1],
                    null,
                    new Vector4(0.35f, 0.78f, 0.45f, 1f));
                DrawReactionTest(current);
            }
            ImGui.EndChild();

            ImGui.EndChild();
            ImGui.SameLine();

            if (ImGui.BeginChild("##ReactionOptionsPanel", new Vector2(optionsWidth, 0), true))
            {
                ImGui.PushTextWrapPos(0);
                var behaviorLabels = PluginUiLogic.ReactionBehaviorSectionLabels;
                ReactionOptionsSection = Math.Clamp(ReactionOptionsSection, 0, behaviorLabels.Length - 1);
                var tabSpacing = ImGui.GetStyle().ItemSpacing.X;
                var tabWidths = behaviorLabels
                    .Select(label =>
                        ImGui.CalcTextSize(label).X +
                        (ImGui.GetStyle().FramePadding.X * 2))
                    .ToArray();
                tabWidths = PluginUiLogic.CalculateButtonWidths(
                    ImGui.GetContentRegionAvail().X,
                    tabSpacing,
                    tabWidths);
                for (var index = 0; index < behaviorLabels.Length; index++)
                {
                    var selected = ReactionOptionsSection == index;
                    if (selected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.45f, 0.68f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.52f, 0.76f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.20f, 0.40f, 0.62f, 1f));
                    }
                    if (ImGui.Button(
                            $"{behaviorLabels[index]}##ReactionBehavior{index}",
                            new Vector2(tabWidths[index], 0)))
                        ReactionOptionsSection = index;
                    if (selected)
                        ImGui.PopStyleColor(3);
                    if (index < behaviorLabels.Length - 1)
                        ImGui.SameLine();
                }
                ImGui.Spacing();

                if (ReactionOptionsSection == 0)
                {
                    DrawEmoteBehaviorEditor(current);
                    DrawSectionGap(0.75f);
                    DrawCommandPermissionsEditor(current);
                    ImGui.Spacing();
                    ImGui.Separator();
                    DrawCommandRulesEditor(current);
                }
                else
                {
                    DrawRepeatBehavior(current);
                    DrawSectionGap(1.5f);
                    ImGui.TextColored(new Vector4(0.72f, 0.58f, 0.92f, 1f), "●  Notifications");
                    ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.72f, 0.58f, 0.92f, 0.72f));
                    ImGui.Separator();
                    ImGui.PopStyleColor();
                    DrawReactionNotifications(current);
                }
                ImGui.PopTextWrapPos();
            }
            ImGui.EndChild();

            if (PendingReactionDuplicate >= 0 && PendingReactionDuplicate < configuration.Reactions.Count)
            {
                var duplicateIndex = PendingReactionDuplicate;
                PendingReactionDuplicate = -1;
                DuplicateReaction(duplicateIndex);
            }
            if (ShowReactionChannels)
            {
                const float channelCategoryRailWidth = 155;
                const float minimumChannelContentWidth = 340;
                const float minimumChannelContentHeight = 420;
                var minimumChannelWindowWidth = PluginUiLogic.CalculateChannelWindowMinimumWidth(
                    channelCategoryRailWidth,
                    minimumChannelContentWidth,
                    ImGui.GetStyle().ItemSpacing.X,
                    ImGui.GetStyle().WindowPadding.X);
                ImGui.SetNextWindowSize(new Vector2(680, 560), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSizeConstraints(
                    new Vector2(minimumChannelWindowWidth, minimumChannelContentHeight),
                    new Vector2(float.MaxValue, float.MaxValue));
                var channelWindowOpen = ShowReactionChannels;
                var channelWindowTitle = string.IsNullOrWhiteSpace(current.Name)
                    ? "Channels — Unnamed reaction"
                    : $"Channels — {current.Name}";
                ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.10f, 0.28f, 0.34f, 1f));
                ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.12f, 0.48f, 0.58f, 1f));
                ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(0.10f, 0.28f, 0.34f, 1f));
                if (ImGui.Begin($"{channelWindowTitle}###PuppetMasterReactionChannels", ref channelWindowOpen))
                    DrawChannelSelector(CurrentReactionIndex, true);
                ImGui.End();
                ImGui.PopStyleColor(3);
                ShowReactionChannels = channelWindowOpen;
            }
        }


        public override void Draw()
        {
            DrawMainToolbar();

            if (MainSection == 0)
                DrawUnifiedReactionsTab();


            if (MainSection == 1)
            {
                var configuration = Service.configuration!;
                if (ImGui.BeginChild("##SettingsSectionNav", new Vector2(210, 0), true))
                {
                    if (ImGui.Selectable("Notifications", SettingsSection == 0))
                        SettingsSection = 0;
                    if (ImGui.Selectable("Command Defaults", SettingsSection == 1))
                        SettingsSection = 1;
                    if (ImGui.Selectable("Channel Defaults", SettingsSection == 2))
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
                        ImGui.TextUnformatted("Notification defaults");
                        ImGui.Separator();
                        DrawWrappedDisabledText("Used by reactions set to Default.");
                        var showReactionNotifications = configuration.ShowReactionNotifications;
                        if (ImGui.Checkbox("Show progress and completion", ref showReactionNotifications))
                        {
                            configuration.ShowReactionNotifications = showReactionNotifications;
                            configuration.Save();
                        }
                        DrawWrappedDisabledText("Shows updates while a reaction runs and when it finishes.");

                        DrawSectionGap();
                        var showSuppressedReactionNotifications = configuration.ShowSuppressedReactionNotifications;
                        if (ImGui.Checkbox("Show notifications for ignored messages", ref showSuppressedReactionNotifications))
                        {
                            configuration.ShowSuppressedReactionNotifications = showSuppressedReactionNotifications;
                            configuration.Save();
                        }
                        DrawWrappedDisabledText("Shown occasionally when a reaction is busy or cooling down.");
                    }
                    else if (SettingsSection == 1)
                    {
                        ImGui.TextUnformatted("Command defaults");
                        ImGui.Separator();
                        DrawWrappedDisabledText("Used by new reactions. Existing reactions do not change.");

                        var defaultMotionOnly = configuration.DefaultMotionOnly;
                        if (ImGui.Checkbox("Hide emote text in new reactions", ref defaultMotionOnly))
                        {
                            configuration.DefaultMotionOnly = defaultMotionOnly;
                            configuration.Save();
                        }
                        DrawWrappedDisabledText("The animation still plays, but its emote message is hidden.");
                        ImGui.Spacing();

                        var defaultAllowAllCommands = configuration.DefaultAllowAllCommands;
                        ImGui.TextUnformatted("Which commands can new reactions run?");
                        if (ImGui.RadioButton("Only the commands listed below", !defaultAllowAllCommands))
                        {
                            configuration.DefaultAllowAllCommands = false;
                            configuration.Save();
                        }
                        if (ImGui.RadioButton("Any command except those blocked", defaultAllowAllCommands))
                        {
                            configuration.DefaultAllowAllCommands = true;
                            configuration.Save();
                        }
                        ImGui.Spacing();

                        if (ImGui.BeginTable(
                                "##CommandDefaultLists",
                                2,
                                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
                        {
                            ImGui.TableNextColumn();
                            DrawCommandListEditor(
                                "Allowed by default",
                                configuration.DefaultAllowAllCommands
                                    ? "Not used while any command is allowed."
                                    : "Non-emote commands new reactions can run.",
                                configuration.DefaultCommandWhitelist,
                                configuration.DefaultCommandBlacklist,
                                ref DefaultWhitelistCommandInput,
                                ref DefaultWhitelistCommandSearch,
                                "DefaultWhitelist",
                                false,
                                configuration.DefaultAllowAllCommands,
                                accentColor: new Vector4(0.35f, 0.78f, 0.45f, 1f));

                            ImGui.TableNextColumn();
                            DrawCommandListEditor(
                                "Blocked by default",
                                "Commands new reactions cannot run.",
                                configuration.DefaultCommandBlacklist,
                                configuration.DefaultCommandWhitelist,
                                ref DefaultBlacklistCommandInput,
                                ref DefaultBlacklistCommandSearch,
                                "DefaultBlacklist",
                                false,
                                accentColor: new Vector4(0.90f, 0.35f, 0.35f, 1f));
                            ImGui.EndTable();
                        }
                    }
                    else if (SettingsSection == 2)
                    {
                        ImGui.TextUnformatted("Channel defaults");
                        ImGui.Separator();
                        DrawWrappedDisabledText("Used by new reactions. Reactions created from Logs use only the source channel.");
                        ImGui.Spacing();
                        const float selectedDefaultsWidth = 220;
                        if (ImGui.BeginChild(
                                "##DefaultSelectedChannels",
                                new Vector2(selectedDefaultsWidth, 0),
                                true))
                        {
                            DrawSelectedChannels(
                                configuration.DefaultEnabledChannels,
                                "DefaultsSelected",
                                maxVisible: int.MaxValue,
                                headingLabel: "Selected channels",
                                accentColor: new Vector4(0.25f, 0.78f, 0.82f, 1f),
                                columns: 1);
                            if (configuration.DefaultEnabledChannels.Count == 0)
                            {
                                DrawWrappedColoredText(
                                    new Vector4(1f, 0.75f, 0.2f, 1),
                                    "New reactions will start without channels.");
                            }
                        }
                        ImGui.EndChild();
                        ImGui.SameLine();
                        if (ImGui.BeginChild("##DefaultChannelPicker", new Vector2(0, 0), true))
                        {
                            DrawChannelSelector(
                                configuration.DefaultEnabledChannels,
                                "Defaults",
                                "New reactions will start without any enabled channels.",
                                compact: true,
                                showSelectedChannels: false);
                        }
                        ImGui.EndChild();
                    }
                    else
                    {
                        DrawCustomChannelSettings();
                    }
                }
                ImGui.EndChild();
            }
        }

        internal static void DrawLogsContent()
        {
                var configuration = Service.configuration!;
                var entries = DebugLogBuffer.Snapshot();
                var logRevision = DebugLogBuffer.Revision;
                var debugLogTypes = configuration.DebugLogTypes;
                var saveLogs = false;
                var saveLogButtonWidth =
                    ImGui.CalcTextSize("Save file").X +
                    (ImGui.GetStyle().FramePadding.X * 2);
                var clearLogButtonWidth =
                    ImGui.CalcTextSize("Clear").X +
                    (ImGui.GetStyle().FramePadding.X * 2);
                if (ImGui.BeginTable(
                        "##LogToolbar",
                        3,
                        ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                {
                    ImGui.TableSetupColumn("Capture", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn(
                        "Save",
                        ImGuiTableColumnFlags.WidthFixed,
                        saveLogButtonWidth + (ImGui.GetStyle().CellPadding.X * 2));
                    ImGui.TableSetupColumn(
                        "Clear",
                        ImGuiTableColumnFlags.WidthFixed,
                        clearLogButtonWidth + (ImGui.GetStyle().CellPadding.X * 2));
                    ImGui.TableNextColumn();
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.34f, 0.52f, 0.72f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.24f, 0.46f, 0.68f, 0.88f));
                    ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.45f, 0.76f, 1f, 1f));
                    if (ImGui.Checkbox("Capture messages", ref debugLogTypes))
                        configuration.DebugLogTypes = debugLogTypes;
                    ImGui.PopStyleColor(3);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Capture game messages with their channel ID, type, and sender.");
                        ImGui.EndTooltip();
                    }
                    ImGui.SameLine();
                    ImGui.Checkbox("Channel colors", ref ColorLogEntries);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Color log rows by channel for this session.");
                        ImGui.EndTooltip();
                    }
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Auto-scroll", ref AutoScrollLogs) && AutoScrollLogs)
                        LastDisplayedLogRevision = -1;
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted("Jump to the latest entry when new messages arrive.");
                        ImGui.EndTooltip();
                    }
                    ImGui.TableNextColumn();
                    if (entries.Length == 0)
                        ImGui.BeginDisabled();
                    saveLogs = DrawPrimaryButton(
                        "Save file",
                        "SaveLogs",
                        new Vector2(-1, ImGui.GetFrameHeight()));
                    if (entries.Length == 0)
                        ImGui.EndDisabled();
                    ImGui.TableNextColumn();
                    if (entries.Length == 0)
                        ImGui.BeginDisabled();
                    if (ImGui.Button("Clear##ClearLogs", new Vector2(-1, ImGui.GetFrameHeight())))
                    {
                        DebugLogBuffer.Clear();
                        ChatHandler.ResetDroppedMessageCount();
                    }
                    if (entries.Length == 0)
                        ImGui.EndDisabled();
                    ImGui.EndTable();
                }

                if (saveLogs)
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

                ImGui.TextDisabled($"{entries.Length} captured");
                var droppedMessageCount = ChatHandler.DroppedMessageCount;
                var droppedRetriggerCount = ChatHandler.DroppedRetriggerCount;
                if (droppedMessageCount > 0 || droppedRetriggerCount > 0)
                {
                    ImGui.SameLine();
                    DrawWrappedColoredText(
                        new Vector4(1f, 0.65f, 0.2f, 1f),
                        $"Discarded: {droppedMessageCount} messages, {droppedRetriggerCount} waiting requests");
                }

                ImGui.Separator();

                if (!string.IsNullOrWhiteSpace(Service.LastDebugLogExportPath))
                {
                    ImGui.TextDisabled("Last saved file:");
                    ImGui.PushTextWrapPos(0);
                    ImGui.TextUnformatted(Service.LastDebugLogExportPath);
                    ImGui.PopTextWrapPos();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(Service.LastDebugLogExportPath);
                        ImGui.EndTooltip();
                    }
                }

                if (ImGui.BeginChild("##PuppetMasterMessageLog", new Vector2(0, 0), true))
                {
                    var hasAddableChannels = entries.Any(entry =>
                        !IsOfficialChatType(entry.ChatTypeId) &&
                        !IsConfiguredCustomChannel(entry.ChatTypeId));
                    var logActionsWidth = PluginUiLogic.CalculateLogActionWidth(
                        ImGui.GetFrameHeight(),
                        ImGui.GetStyle().ItemSpacing.X,
                        ImGui.GetStyle().CellPadding.X,
                        hasAddableChannels);
                    if (ImGui.BeginTable(
                            "##LogEntries",
                            3,
                            ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                    {
                        ImGui.TableSetupColumn("Channel", ImGuiTableColumnFlags.WidthFixed, 18);
                        ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, logActionsWidth);
                        for (var index = 0; index < entries.Length; index++)
                        {
                            var entry = entries[index];
                            var channelColor = ColorLogEntries
                                ? GetLogChannelColor(entry.ChatTypeId)
                                : new Vector4(0.68f, 0.70f, 0.74f, 1f);
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.AlignTextToFramePadding();
                            ImGui.TextColored(channelColor, "●");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.TextUnformatted($"Channel ID: {entry.ChatTypeId}");
                                ImGui.EndTooltip();
                            }
                            ImGui.TableSetColumnIndex(1);
                            if (ColorLogEntries)
                                DrawWrappedColoredText(channelColor, entry.Text);
                            else
                                DrawWrappedText(entry.Text);
                            ImGui.TableSetColumnIndex(2);
                            if (DrawLogActionButton(
                                    FontAwesome.Plus,
                                    $"LogReaction{entry.ChatTypeId}_{index}",
                                    "Create reaction",
                                    new Vector4(0.22f, 0.45f, 0.68f, 1f),
                                    true))
                                CreateReactionFromLog(entry);
                            if (!IsOfficialChatType(entry.ChatTypeId) && !IsConfiguredCustomChannel(entry.ChatTypeId))
                            {
                                ImGui.SameLine();
                                if (DrawLogActionButton(
                                        "#",
                                        $"LogCustomChannel{entry.ChatTypeId}_{index}",
                                        "Add custom channel",
                                        new Vector4(0.16f, 0.52f, 0.58f, 1f)))
                                    AddCustomChannel(entry.ChatTypeId);
                            }
                        }
                        ImGui.EndTable();
                    }
                    if (AutoScrollLogs && logRevision != LastDisplayedLogRevision)
                        ImGui.SetScrollHereY(1f);
                    LastDisplayedLogRevision = logRevision;
                }
                ImGui.EndChild();
            }
        }
    }

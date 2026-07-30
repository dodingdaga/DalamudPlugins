using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace PuppetMaster;

internal sealed class ReactionVisualizerWindow : Window
{
    [Flags]
    private enum RosterFilter
    {
        Active = 1 << 0,
        Queued = 1 << 1,
        Ready = 1 << 2,
        Attention = 1 << 3,
        Disabled = 1 << 4,
        All = Active | Queued | Ready | Attention | Disabled,
    }

    private static readonly Vector4 RunningColor = new(0.25f, 0.62f, 1f, 1f);
    private static readonly Vector4 QueuedColor = new(1f, 0.72f, 0.2f, 1f);
    private static readonly Vector4 CompletedColor = new(0.3f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 CancelledColor = new(1f, 0.4f, 0.35f, 1f);
    private static readonly Vector4 DisabledColor = new(0.48f, 0.48f, 0.5f, 1f);
    private static readonly Vector4 AttentionColor = new(1f, 0.4f, 0.3f, 1f);
    private string reactionSearch = string.Empty;
    private RosterFilter rosterFilter = RosterFilter.All;

    public ReactionVisualizerWindow() : base("Puppet Master — Reaction Visualizer")
    {
        SizeConstraints = new() { MinimumSize = new Vector2(620, 420), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
    }

    public override void Draw()
    {
        var snapshot = ReactionVisualizerState.Snapshot();
        ImGui.TextColored(new Vector4(0.35f, 0.9f, 0.55f, 1f), "● LIVE");
        ImGui.SameLine();
        ImGui.TextDisabled($"Active: {snapshot.Active.Length}   Queued: {snapshot.Queued.Length}");
        ImGui.SameLine();
        ImGui.TextDisabled("Read-only viewer");
        ImGui.Separator();

        if (!ImGui.BeginTable(
                "##ReactionVisualizerColumns",
                2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
            return;
        ImGui.TableSetupColumn("Reactions", ImGuiTableColumnFlags.WidthFixed, 230);
        ImGui.TableSetupColumn("Activity", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawReactionRoster(snapshot);
        ImGui.TableNextColumn();
        DrawActivity(snapshot);
        ImGui.EndTable();
    }

    private void DrawReactionRoster(ReactionVisualizerSnapshot snapshot)
    {
        var reactions = Service.configuration?.Reactions;
        ImGui.TextUnformatted($"All Reactions ({reactions?.Count ?? 0})");
        ImGui.Separator();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##VisualizerReactionSearch", "Filter by name...", ref reactionSearch, 100);
        ImGui.SetNextItemWidth(-1);
        DrawRosterFilter();
        ImGui.Spacing();
        if (!ImGui.BeginChild("##VisualizerReactionRoster", new Vector2(0, 0), true))
        {
            ImGui.EndChild();
            return;
        }

        if (reactions == null || reactions.Count == 0)
            ImGui.TextDisabled("No reactions configured");
        else
        {
            for (var index = 0; index < reactions.Count; index++)
            {
                var reaction = reactions[index];
                var reactionId = ChatHandler.GetVisualizerId(reaction);
                var activeCount = snapshot.Active.Count(item => item.ReactionId == reactionId);
                var queuedCount = snapshot.Queued.Count(item => item.ReactionId == reactionId);
                var state = GetRosterState(reaction, activeCount, queuedCount);
                if (!MatchesRosterFilter(reaction, state))
                    continue;
                var color = GetRosterColor(state);
                DrawRosterCard(reaction.Name, index, color, activeCount, queuedCount);
                ImGui.Spacing();
            }
        }
        ImGui.EndChild();
    }

    private static void DrawActivity(ReactionVisualizerSnapshot snapshot)
    {
        ImGui.TextUnformatted("Worker Activity");
        ImGui.Separator();

        if (ImGui.BeginChild("##VisualizerLanes", new Vector2(0, 238), true))
        {
            for (var lane = 0; lane < 4; lane++)
            {
                DrawLane(lane, snapshot.Active.FirstOrDefault(item => item.Lane == lane));
                if (lane < 3) ImGui.Separator();
            }
            var overflow = snapshot.Active.Count(item => item.Lane < 0);
            if (overflow > 0) ImGui.TextColored(QueuedColor, $"+{overflow} additional active reaction{(overflow == 1 ? string.Empty : "s")}");
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextUnformatted($"Current Queue ({snapshot.Queued.Length})");
        ImGui.Separator();
        if (ImGui.BeginChild("##VisualizerQueue", new Vector2(0, 72), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (snapshot.Queued.Length == 0) ImGui.TextDisabled("No pending retriggers");
            foreach (var queued in snapshot.Queued)
            {
                DrawCard(queued.ReactionName, "QUEUED", queued.Command, queued.QueuedAt, QueuedColor);
                ImGui.SameLine();
            }
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextUnformatted("Recently Completed");
        ImGui.Separator();
        if (ImGui.BeginChild("##VisualizerRecent", new Vector2(0, 0), true))
        {
            if (snapshot.Recent.Length == 0) ImGui.TextDisabled("Completed reactions will appear here");
            foreach (var item in snapshot.Recent)
            {
                var color = item.Status == VisualizerRunStatus.Cancelled ? CancelledColor : CompletedColor;
                var duration = item.FinishedAt.HasValue ? item.FinishedAt.Value - item.StartedAt : TimeSpan.Zero;
                ImGui.TextColored(color, item.Status == VisualizerRunStatus.Cancelled ? "×" : "✓");
                ImGui.SameLine(); ImGui.TextUnformatted(item.ReactionName);
                ImGui.SameLine(); ImGui.TextDisabled($"{duration.TotalMilliseconds:0} ms  ·  {item.StartedAt:HH:mm:ss}");
                DrawTooltip(item.Command, item.StartedAt);
            }
        }
        ImGui.EndChild();
    }

    private static RosterFilter GetRosterState(Reaction reaction, int activeCount, int queuedCount)
    {
        if (activeCount > 0)
            return RosterFilter.Active;
        if (queuedCount > 0)
            return RosterFilter.Queued;
        return PluginUiLogic.GetStatus(reaction) switch
        {
            ReactionUiStatus.Disabled => RosterFilter.Disabled,
            ReactionUiStatus.Ready => RosterFilter.Ready,
            _ => RosterFilter.Attention,
        };
    }

    private static Vector4 GetRosterColor(RosterFilter state)
    {
        return state switch
        {
            RosterFilter.Active => RunningColor,
            RosterFilter.Queued => QueuedColor,
            RosterFilter.Ready => CompletedColor,
            RosterFilter.Attention => AttentionColor,
            _ => DisabledColor,
        };
    }

    private bool MatchesRosterFilter(Reaction reaction, RosterFilter state)
    {
        if (!string.IsNullOrWhiteSpace(reactionSearch) &&
            reaction.Name?.Contains(reactionSearch, StringComparison.OrdinalIgnoreCase) != true)
            return false;
        return (rosterFilter & state) != 0;
    }

    private void DrawRosterFilter()
    {
        var selectedCount = 0;
        foreach (var state in new[]
                 {
                     RosterFilter.Active,
                     RosterFilter.Queued,
                     RosterFilter.Ready,
                     RosterFilter.Attention,
                     RosterFilter.Disabled,
                 })
        {
            if ((rosterFilter & state) != 0)
                selectedCount++;
        }

        var preview = rosterFilter == RosterFilter.All
            ? "All states"
            : selectedCount == 0 ? "No states" : $"{selectedCount} states";
        if (!ImGui.BeginCombo("##VisualizerReactionStateFilter", preview))
            return;

        var all = rosterFilter == RosterFilter.All;
        if (ImGui.Checkbox("All states", ref all))
            rosterFilter = all ? RosterFilter.All : 0;
        ImGui.Separator();
        DrawRosterFilterFlag("Active", RosterFilter.Active);
        DrawRosterFilterFlag("Queued", RosterFilter.Queued);
        DrawRosterFilterFlag("Ready", RosterFilter.Ready);
        DrawRosterFilterFlag("Needs attention", RosterFilter.Attention);
        DrawRosterFilterFlag("Disabled", RosterFilter.Disabled);
        ImGui.EndCombo();
    }

    private void DrawRosterFilterFlag(string label, RosterFilter flag)
    {
        var selected = (rosterFilter & flag) != 0;
        if (!ImGui.Checkbox(label, ref selected))
            return;
        if (selected)
            rosterFilter |= flag;
        else
            rosterFilter &= ~flag;
    }

    private static void DrawRosterCard(
        string name,
        int index,
        Vector4 color,
        int activeCount,
        int queuedCount)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(color.X * 0.28f, color.Y * 0.28f, color.Z * 0.28f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(color.X * 0.34f, color.Y * 0.34f, color.Z * 0.34f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.Button($"{(string.IsNullOrWhiteSpace(name) ? "Unnamed reaction" : name)}##VisualizerRoster{index}", new Vector2(-1, 36));
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextDisabled($"Active: {activeCount}   Queued: {queuedCount}");
            ImGui.EndTooltip();
        }
    }

    private static void DrawLane(int lane, VisualizerRunSnapshot? active)
    {
        ImGui.TextDisabled($"WORKER {lane + 1}");
        ImGui.SameLine(92);
        if (active == null) { ImGui.TextDisabled("Idle"); ImGui.Dummy(new Vector2(0, 28)); return; }
        DrawCard(active.ReactionName, "RUNNING", active.Command, active.StartedAt, RunningColor);
    }

    private static void DrawCard(string name, string status, string command, DateTime timestamp, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(color.X * 0.28f, color.Y * 0.28f, color.Z * 0.28f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(color.X * 0.36f, color.Y * 0.36f, color.Z * 0.36f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.Button($"{name}\n{status}##VisualizerCard{status}{timestamp.Ticks}", new Vector2(210, 48));
        ImGui.PopStyleColor(3);
        DrawTooltip(command, timestamp);
    }

    private static void DrawTooltip(string command, DateTime timestamp)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextDisabled($"Received {timestamp:HH:mm:ss.fff}");
        ImGui.Separator();
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(command) ? "No generated command" : command);
        ImGui.EndTooltip();
    }
}

using ImGuiNET;

using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Interface.Windowing;

namespace PuppetMaster
{
    public class ConfigWindow : Window, IDisposable
    {
        public const String Name = "Puppet Master settings";

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
                Service.configuration.Reactions[index].Enabled= enabled;
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

            /*
            // Can't figure out how to set focus on a tab
            if (ImGui.Button($"Edit##ReactionEdit##{index}"))
            {
                Service.configuration.CurrentReactionEdit = index;
                Service.configuration.Save();
            }
            */

            ImGui.SameLine();
            if (ImGui.Button($"Delete##ReactionDelete##{index}"))
            {
                Service.semaphore.WaitOne();
                Service.configuration.Reactions.RemoveAt(index);
                Service.configuration.Save();
                Service.semaphore.Release();
            }
        }

        private static void DrawChannelCheckbox(int reactionIndex, int channelIndex)
        {
            if (channelIndex % 4 != 0) ImGui.SameLine();
            
            var chatType = Service.configuration!.EnabledChannels[channelIndex].ChatType;
            var enabled = Service.configuration.Reactions[reactionIndex].EnabledChannels.Contains(chatType);
           
            if (ImGui.Checkbox($"{Service.configuration.EnabledChannels[channelIndex].Name}##DefaultChannelCheckBox{channelIndex}{chatType}", ref enabled))
            {
                Service.semaphore.WaitOne();
                if (enabled)
                {
                    Service.configuration.Reactions[reactionIndex].EnabledChannels.Add(chatType);
                }
                else
                {
                    Service.configuration.Reactions[reactionIndex].EnabledChannels.Remove(chatType);
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

        private static void DrawCustomChannelCheckbox(int reactionIndex, int channelIndex)
        {
            if (channelIndex % 4 != 0) ImGui.SameLine();
            
            var chatType = Service.configuration!.CustomChannels[channelIndex].ChatType;
            var enabled = Service.configuration.Reactions[reactionIndex].EnabledChannels.Contains(chatType);
            
            if (ImGui.Checkbox($"{Service.configuration.CustomChannels[channelIndex].Name}##CustomChannelCheckBox{channelIndex}{chatType}", ref enabled))
            {
                Service.semaphore.WaitOne();
                if (enabled)
                {
                    Service.configuration.Reactions[reactionIndex].EnabledChannels.Add(chatType);
                }
                else
                {
                    Service.configuration.Reactions[reactionIndex].EnabledChannels.Remove(chatType);
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
            if(ImGui.InputText($"##CustomChannelLabel##{index}",ref channelName,100))
            {
                Service.configuration.CustomChannels[index].Name = channelName;
                Service.configuration.Save();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (ImGui.Button($"Delete##CustomChannelDelete#{index}"))
            {
                Service.semaphore.WaitOne();
                for (var i = 0; i < Service.configuration.Reactions.Count; i++)
                {
                    Service.configuration.Reactions[i].EnabledChannels.Remove(channelID);
                }
                Service.configuration.CustomChannels.RemoveAt(index);
                Service.configuration.Save();
                Service.semaphore.Release();
            }
        }

        public override void Draw()
        {            
            ImGui.SetNextWindowSize(new Vector2(480, 640), ImGuiCond.FirstUseEver);

            ImGui.BeginTabBar("PuppetMaster Config Tabs");

            if (ImGui.BeginTabItem("Reactions"))
            {
                if (ImGui.Button($"Add##ReactionAddButton"))
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

            if (ImGui.BeginTabItem("Edit Reactions"))
            {
                var reactionNames =  new List<string>{ };
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
                    ImGui.Text("Enabled Channels");
                    ImGui.Indent(20);
                    
                    if (Service.configuration.CustomChannels.Count > 0)
                    {
                        ImGui.Separator(); //----------------------------------------------
                        for (var channelIndex = 0; channelIndex < Service.configuration.CustomChannels.Count; ++channelIndex)
                            DrawCustomChannelCheckbox(CurrentReactionIndex, channelIndex);
                    }
                    
                    ImGui.Separator(); //----------------------------------------------
                   
                    for (var channelIndex = 16; channelIndex < 23; ++channelIndex)
                    {
                        DrawChannelCheckbox(CurrentReactionIndex, channelIndex);
                    }
                    
                    ImGui.Separator(); //----------------------------------------------
                    
                    for (var channelIndex = 0; channelIndex < 8; ++channelIndex)
                    {
                        DrawChannelCheckbox(CurrentReactionIndex,channelIndex);
                    }
                    
                    ImGui.Separator(); //----------------------------------------------
                    
                    for (var channelIndex = 8; channelIndex < 16; ++channelIndex)
                    {
                        DrawChannelCheckbox(CurrentReactionIndex,channelIndex);
                    }
                }
                
                ImGui.EndTabItem();
            }
        
            if (ImGui.BeginTabItem("Custom Channels"))
            {
                ImGui.SetNextItemWidth(400);
               
                var debugLogTypes = Service.configuration!.DebugLogTypes;
                if (ImGui.Checkbox("Debug log types", ref debugLogTypes))
                {  
                    Service.configuration.DebugLogTypes = debugLogTypes;
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Enabling this print all game messages in the log windows.");
                    ImGui.Text("Logs will be prefixed with log type ID (and optionally the type name and sender, if they exist)");
                    ImGui.EndTooltip();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Add##CustomChannelAdd"))
                {
                    Service.configuration.CustomChannels.Add( new ChannelSetting(){ChatType = (int)0, Name = "Custom", Enabled = false});
                    Service.configuration.Save();
                }
                
                ImGui.Spacing();
                ImGui.Spacing();
                
                if (Service.configuration.CustomChannels.Count > 0)
                {
                    for (var index = 0; index <  Service.configuration.CustomChannels.Count; ++index)
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

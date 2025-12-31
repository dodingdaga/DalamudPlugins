using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;

using ECommons;

namespace PuppetMaster
{
    public class Plugin : IDalamudPlugin
    {
        public static String Name => "PuppetMaster";
        public WindowSystem windowSystem = new("PuppetMaster");
        public ConfigWindow configWindow;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            // Service
            pluginInterface.Create<Service>();
            Service.plugin = this;

            // Configuration
            Service.InitializeConfig();

            this.configWindow = new ConfigWindow();
            windowSystem.AddWindow(configWindow);

            // Initialize commands
            InitializeCommands();

            Service.ChatGui.ChatMessage += ChatHandler.OnChatMessage;
            Service.PluginInterface.UiBuilder.Draw += DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;

            // Excel sheets
            Service.InitializeEmotes();

            // ECommons
            ECommonsMain.Init(pluginInterface, this, Module.All);
        }

        private void InitializeCommands()
        {
            var prefix = Service.configuration?.CommandPrefix?.Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "/puppetmaster";
                Service.configuration!.CommandPrefix = prefix;
                Service.configuration.Save();
            }

            // Ensure prefix starts with /
            if (!prefix.StartsWith("/"))
            {
                prefix = "/" + prefix;
                Service.configuration!.CommandPrefix = prefix;
                Service.configuration.Save();
            }

            // Register main command
            Service.CommandManager.AddHandler(prefix, new CommandInfo(OnCommand)
            {
                HelpMessage = @"Open settings dialog
" + prefix + @" on|off - enable or disable all reactions
" + prefix + @" on|off <ReactionName> - enable or disable reactions by name
" + prefix + @" debug - toggle debug mode
" + prefix + @" list - list all reactions"
            });

            // Register short command alias if enabled
            if (Service.configuration!.EnableShortCommand && prefix != "/pm")
            {
                Service.CommandManager.AddHandler("/pm", new CommandInfo(OnCommand)
                {
                    HelpMessage = "Short alias for PuppetMaster commands."
                });
            }
        }

        private void DisposeCommands()
        {
            var prefix = Service.configuration?.CommandPrefix ?? "/puppetmaster";

            try
            {
                Service.CommandManager.RemoveHandler(prefix);
            }
            catch { }

            if (Service.configuration?.EnableShortCommand == true)
            {
                try
                {
                    Service.CommandManager.RemoveHandler("/pm");
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Service.ChatGui.ChatMessage -= ChatHandler.OnChatMessage;
            DisposeCommands();
            windowSystem.RemoveAllWindows();

            ECommonsMain.Dispose();
        }

        private void OnCommand(String command, String args)
        {
            if (string.IsNullOrEmpty(args))
                DrawConfigUI();
            else
            {
                var ptc = Service.FormatCommand($"/{args}");

                if (ptc.Main == "/debug")
                {
                    Service.semaphore.WaitOne();
                    try
                    {
                        bool debugState = !Service.configuration!.EnableVerboseDebug;
                        Service.configuration.EnableVerboseDebug = debugState;
                        Service.configuration.Save();

                        Service.ChatGui.Print($"[PuppetMaster] {(debugState ?
                            Localization.Get("Command.DebugEnabled") :
                            Localization.Get("Command.DebugDisabled"))}");
                    }
                    finally
                    {
                        Service.semaphore.Release();
                    }
                    return;
                }

                void enableReactions(bool enable)
                {
                    if (string.IsNullOrEmpty(ptc.Args))
                        Service.SetEnabledAll(enable);
                    else
                        Service.SetEnabled(ptc.Args, enable);
                }

                if (ptc.Main.Equals("/on"))
                {
                    enableReactions(true);
                }
                else if (ptc.Main.Equals("/off"))
                {
                    enableReactions(false);
                }
                else if (ptc.Main.Equals("/list"))
                {
                    Service.semaphore.WaitOne();
                    try
                    {
                        Service.ChatGui.Print("[PuppetMaster] Reactions:");
                        foreach (var reaction in Service.configuration!.Reactions)
                        {
                            Service.ChatGui.Print($"  - {reaction.Name}: {(reaction.Enabled ? "Enabled" : "Disabled")}");
                        }
                    }
                    finally
                    {
                        Service.semaphore.Release();
                    }
                }
                else
                {
                    Service.ChatGui.Print($"[PuppetMaster] {Localization.Get("Command.Unknown")} {args}");
                    Service.ChatGui.Print($"[PuppetMaster] {Localization.Get("Command.Available")}");
                }
            }
        }

        private void DrawUI()
        {
            this.windowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            this.configWindow.IsOpen = true;
            ConfigWindow.PreloadTestResult();
        }
    }
}

using System;
using System.Collections.Generic;

namespace PuppetMaster
{

    public static class Localization
    {
        private static readonly Dictionary<PluginLanguage, Dictionary<string, string>> Strings = new();

        static Localization()
        {
            InitializeEnglish();
            InitializeChinese();
            // InitializeJapanese() removed as requested
        }

        private static void InitializeEnglish()
        {
            Strings[PluginLanguage.English] = new Dictionary<string, string>
            {
                // Window title
                ["Window.Title"] = "Puppet Master Settings",

                // Tab names
                ["Tab.GlobalSettings"] = "Global Settings",
                ["Tab.Reactions"] = "Reactions",
                ["Tab.EditReaction"] = "Edit Reaction",
                ["Tab.CustomChannels"] = "Custom Channels",

                // Global Settings
                ["Global.PlayerLists"] = "Player List Settings",
                ["Global.EnablePlayerLists"] = "Enable global player lists",
                ["Global.PlayerWhitelist"] = "Player whitelist (one per line, empty to skip):",
                ["Global.PlayerBlacklist"] = "Player blacklist (one per line):",
                ["Global.CommandLists"] = "Command List Settings",
                ["Global.EnableCommandLists"] = "Enable global command lists",
                ["Global.CommandWhitelist"] = "Command whitelist (one per line, empty to skip):",
                ["Global.CommandBlacklist"] = "Command blacklist (one per line):",
                ["Global.BasicSettings"] = "Basic Settings",
                ["Global.GlobalDelay"] = "Global base delay",
                ["Global.GlobalCooldown"] = "Global base cooldown",
                ["Global.SpeakerFilter"] = "Global speaker filter",
                ["Global.GameStateRestrictions"] = "Game State Restrictions",
                ["Global.EnableGameState"] = "Enable global game state restrictions",
                ["Global.DisableInCombat"] = "Disable in combat",
                ["Global.DisableInCutscene"] = "Disable in cutscene",
                ["Global.DisableWhileLoading"] = "Disable while loading",
                ["Global.ChannelSettings"] = "Channel Settings",
                ["Global.EnableChannels"] = "Enable global channel settings",
                ["Global.ChannelNote1"] = "Note: Check the channels you want to enable globally below",
                ["Global.ChannelNote2"] = "Individual reactions can override this setting",
                ["Global.DefaultChannels"] = "Default Channels",
                ["Global.CustomChannels"] = "Custom Channels",
                ["Global.DebugSettings"] = "Debug Settings",
                ["Global.EnableDebug"] = "Enable verbose debug logging",
                ["Global.DebugWarning"] = "Note: Debug logging will appear in game chat and may be spammy",
                ["Tab.FilterSettings"] = "Filter Settings",
                ["Tab.BasicSettings"] = "Basic Settings",
                ["Tab.CommandSettings"] = "Command Settings",
                ["Help.PlayerLists"] = "One player per line. Use PlayerName or PlayerName@Server. Case-insensitive.",
                ["Help.CommandLists"] = "One command per line (e.g., /sit, /wave). Case-sensitive.",
                ["Help.RegexSyntax"] = "Use standard .NET regex syntax. Use $1, $2 for capture groups.",
                ["UI_ChannelSelection"] = "Channel Selection",

                // Reaction list
                ["Reaction.Add"] = "Add",
                ["Reaction.Delete"] = "Delete",
                ["Reaction.Name"] = "Name",

                // Edit Reaction
                ["Edit.SelectReaction"] = "Select Reaction",
                ["Edit.TriggerMode"] = "Trigger Mode",
                ["Edit.TriggerMode.Keyword"] = "Keyword Mode",
                ["Edit.TriggerMode.Regex"] = "Regex Mode",
                ["Edit.TriggerMode.SpecificPlayer"] = "Specific Player Mode",
                ["Edit.TriggerMode.Tooltip"] = "Select trigger mode:\n- Keyword Mode: Use keywords to trigger\n- Regex Mode: Use regular expression matching\n- Specific Player Mode: Only respond to specific players' emotes (message must contain emote name)",
                ["Edit.SpecificPlayers"] = "Specific player list (one per line, can include server):",
                ["Edit.SpecificPlayersNote"] = "Note: In specific player mode, only player name needs to match, message must contain emote name",
                ["Edit.Keyword"] = "Keyword (use | to separate multiple keywords)",
                ["Edit.Regex"] = "Regular expression",
                ["Edit.Replacement"] = "Replacement text (commands to execute)",
                ["Edit.TestInput"] = "Test input",
                ["Edit.Matched"] = "Matched:",
                ["Edit.DetectedEmote"] = "Detected emote:",
                ["Edit.WillExecute"] = "Will execute:",

                // Reaction overrides
                ["Override.Title"] = "Global Settings Overrides",
                ["Override.Delay"] = "Delay:",
                ["Override.Cooldown"] = "Cooldown:",
                ["Override.UseGlobalDelay"] = "Use global delay",
                ["Override.CustomDelay"] = "Custom delay",
                ["Override.UseGlobalCooldown"] = "Use global cooldown",
                ["Override.CustomCooldown"] = "Custom cooldown",
                ["Override.SpeakerFilter"] = "Speaker filter:",
                ["Override.UseGlobalSpeaker"] = "Use global speaker filter",
                ["Override.CustomSpeaker"] = "Custom speaker filter",
                ["Override.PlayerLists"] = "Player List Overrides",
                ["Override.UseGlobalPlayers"] = "Use global player lists",
                ["Override.Whitelist"] = "Override whitelist (one per line, empty to skip):",
                ["Override.Blacklist"] = "Override blacklist (one per line):",
                ["Override.CommandLists"] = "Command List Overrides",
                ["Override.UseGlobalCommands"] = "Use global command lists",
                ["Override.CommandWhitelist"] = "Override whitelist (one per line, empty to skip):",
                ["Override.CommandBlacklist"] = "Override blacklist (one per line):",
                ["Override.GameState"] = "Game State Restriction Overrides",
                ["Override.UseGlobalGameState"] = "Use global game state restrictions",
                ["Override.ChannelSettings"] = "Channel Settings Override",
                ["Override.UseGlobalChannels"] = "Use global channel settings",
                ["Override.ChannelNote"] = "Custom channel settings (override global):",
                ["Override.UsingGlobalChannels"] = "Using global channel settings ({0} channels)",

                // Common settings
                ["Common.AllowAllCommands"] = "Allow all text commands",
                ["Common.AllowSit"] = "Allow \"sit\" or \"groundsit\" requests",
                ["Common.MotionOnly"] = "Motion only",
                ["Common.Tooltip.AllowAllCommands"] = "If a command contains subcommands, enclose the sequence in parentheses.\nFor placeholders, replace angle brackets with square brackets.\nExample: please do (ac \"Vercure\" [t])",

                // Custom Channels
                ["Custom.EnableDebugTypes"] = "Debug log types",
                ["Custom.DebugTooltip"] = "When enabled, all game messages will be printed in the log window.\nLogs will be prefixed with log type ID (and type name and sender if they exist)",
                ["Custom.Add"] = "Add",
                ["Custom.ID"] = "ID",
                ["Custom.Label"] = "Label",

                // Speaker filter options
                ["Speaker.All"] = "All",
                ["Speaker.IgnoreSelf"] = "Ignore self",
                ["Speaker.SelfOnly"] = "Self only",

                // Units
                ["Unit.Seconds"] = "seconds",
                ["Unit.Channels"] = "channels",

                // Plugin commands
                ["Command.Enabled"] = "enabled",
                ["Command.Disabled"] = "disabled",
                ["Command.DebugEnabled"] = "Verbose debug mode enabled",
                ["Command.DebugDisabled"] = "Verbose debug mode disabled",
                ["Command.Unknown"] = "Unknown command:",
                ["Command.Available"] = "Available commands: on, off, debug, list",
                ["Command.LanguageChanged"] = "Language changed to {0}. Please restart plugin for full effect.",
                ["Command.RestartRequired"] = "Please restart plugin for command changes to take effect.",

                // Command settings
                ["UI_CommandSettings"] = "Command Settings",
                ["UI_CommandPrefix"] = "Command Prefix",
                ["UI_CommandPrefix_Help"] = "Customize the chat command prefix (e.g., /pm, /puppet)",
                ["UI_EnableShortCommand"] = "Enable /pm short command",
                ["UI_EnableShortCommand_Help"] = "Also register /pm as an alias for the main command",
                ["UI_AvailableCommands"] = "Available commands",
                ["UI_LanguageSettings"] = "Language Settings",
                ["UI_Language"] = "Language",

                // Debug messages
                ["Debug.GameStateFailed"] = "Game state check failed, skipping execution",
                ["Debug.AntiLoop"] = "Anti-loop triggered, skipping execution",
                ["Debug.ApplyDelay"] = "Apply delay: {0} seconds",
                ["Debug.ProcessingCommand"] = "Processing command {0}/{1}: {2}",
                ["Debug.Waiting"] = "Waiting {0} seconds",
                ["Debug.SendingCommand"] = "Sending command: {0}",
                ["Debug.CommandSent"] = "Command sent: {0}",
                ["Debug.AllCommandsComplete"] = "All commands executed",
                ["Debug.AlreadyExecuting"] = "Command already executing, skipping",
                ["Debug.ChannelNotEnabled"] = "Channel {0} not enabled, skipping",
                ["Debug.Cooldown"] = "On cooldown, skipping",
                ["Debug.SenderNotInList"] = "Sender {0} not in specific player list",
                ["Debug.NoEmoteFound"] = "No emote found in message: {0}",
                ["Debug.RegexEmpty"] = "Regular expression is empty",
                ["Debug.NoMatch"] = "No match for pattern: {0}",
                ["Debug.MatchSuccess"] = "Match successful, generated command: {0}",
                ["Debug.GeneratedCommandEmpty"] = "Generated command is empty",
                ["Debug.ReactionDisabled"] = "Reaction {0} disabled",
                ["Debug.FilterFailed"] = "Reaction {0} filter failed",
                ["Debug.FilterPassed"] = "Reaction {0} passed filter",
                ["Debug.ReceivedMessage"] = "Received message - Type:{0} Sender:{1} Content:{2}",
                ["Debug.IgnoreEmote"] = "Ignore emote message: {0}",
                ["Debug.StartProcessing"] = "Start processing - Message: {0}",
                ["Debug.WaitCancelled"] = "Wait cancelled",
                ["Debug.CommandCancelled"] = "Command execution cancelled",
                ["Debug.CommandTaskFailed"] = "Command task failed: {0}",
                ["Debug.RegexFailed"] = "Regex replacement failed: {0}",
                ["Debug.SendFailed"] = "Send command failed: {0} - {1}",
                ["Debug.FrameworkFailed"] = "Framework thread execution failed: {0}",
                ["Debug.ExecuteFailed"] = "Execute command error: {0}",
                ["Debug.RunMacroFailed"] = "RunMacroAsync error: {0}",

                // Error messages
                ["Error.SendCommand"] = "Send command failed:",
                ["Error.RegexInit"] = "Failed to initialize custom regex:",
                ["Error.RegexInitDefault"] = "Failed to initialize regex:",
                ["Error.LoadEmotes"] = "Failed to read emotes list",
                ["Error.BuildEmotes"] = "Failed to build emotes list",
                ["Error.InitializeConfig"] = "Configuration initialization completed, verbose debug logging enabled",
            };
        }

        private static void InitializeChinese()
        {
            Strings[PluginLanguage.Chinese] = new Dictionary<string, string>
            {
                // Window title
                ["Window.Title"] = "Puppet Master 设置",

                // Tab names
                ["Tab.GlobalSettings"] = "全局设置",
                ["Tab.Reactions"] = "反应列表",
                ["Tab.EditReaction"] = "编辑反应",
                ["Tab.CustomChannels"] = "自定义频道",

                // Global Settings
                ["Global.PlayerLists"] = "全局玩家名单设置",
                ["Global.EnablePlayerLists"] = "启用全局玩家名单",
                ["Global.PlayerWhitelist"] = "玩家白名单（每行一个玩家名，为空则跳过）：",
                ["Global.PlayerBlacklist"] = "玩家黑名单（每行一个玩家名）：",
                ["Global.CommandLists"] = "全局命令名单设置",
                ["Global.EnableCommandLists"] = "启用全局命令名单",
                ["Global.CommandWhitelist"] = "命令白名单（每行一个命令，为空则跳过）：",
                ["Global.CommandBlacklist"] = "命令黑名单（每行一个命令）：",
                ["Global.BasicSettings"] = "基础设置",
                ["Global.GlobalDelay"] = "全局基础延迟",
                ["Global.GlobalCooldown"] = "全局基础冷却",
                ["Global.SpeakerFilter"] = "全局发言者过滤",
                ["Global.GameStateRestrictions"] = "游戏状态限制",
                ["Global.EnableGameState"] = "启用全局游戏状态限制",
                ["Global.DisableInCombat"] = "战斗中禁用",
                ["Global.DisableInCutscene"] = "过场动画中禁用",
                ["Global.DisableWhileLoading"] = "加载中禁用",
                ["Global.ChannelSettings"] = "全局频道设置",
                ["Global.EnableChannels"] = "启用全局频道设置",
                ["Global.ChannelNote1"] = "注意：在下方勾选需要启用的全局频道",
                ["Global.ChannelNote2"] = "单个Reaction可以覆盖此设置",
                ["Global.DefaultChannels"] = "默认频道",
                ["Global.CustomChannels"] = "自定义频道",
                ["Global.DebugSettings"] = "调试设置",
                ["Global.EnableDebug"] = "启用详细调试日志",
                ["Global.DebugWarning"] = "注意：调试日志会显示在游戏聊天窗口中，可能会很刷屏",
                ["Tab.FilterSettings"] = "筛选设置",
                ["Tab.BasicSettings"] = "基础设置",
                ["Tab.CommandSettings"] = "命令设置",
                ["Help.PlayerLists"] = "每行一个玩家。支持玩家名或玩家名@服务器。不区分大小写。",
                ["Help.CommandLists"] = "每行一个命令（例如：/sit, /wave）。区分大小写。",
                ["Help.RegexSyntax"] = "使用标准.NET正则表达式语法。使用$1、$2引用捕获组。",
                ["UI_ChannelSelection"] = "频道选择",

                // Reaction list
                ["Reaction.Add"] = "添加",
                ["Reaction.Delete"] = "删除",
                ["Reaction.Name"] = "名称",

                // Edit Reaction
                ["Edit.SelectReaction"] = "选择反应",
                ["Edit.TriggerMode"] = "触发模式",
                ["Edit.TriggerMode.Keyword"] = "关键词模式",
                ["Edit.TriggerMode.Regex"] = "正则表达式模式",
                ["Edit.TriggerMode.SpecificPlayer"] = "指定玩家模式",
                ["Edit.TriggerMode.Tooltip"] = "选择触发模式：\n- 关键词模式：使用关键词触发\n- 正则表达式模式：使用正则表达式匹配\n- 指定玩家模式：只响应指定玩家的情感动作（消息需包含情感动作名称）",
                ["Edit.SpecificPlayers"] = "指定玩家列表（每行一个玩家名，可带服务器名）：",
                ["Edit.SpecificPlayersNote"] = "注意：指定玩家模式下，只需玩家名匹配，消息需包含情感动作名称",
                ["Edit.Keyword"] = "关键词（用|分隔多个关键词）",
                ["Edit.Regex"] = "正则表达式",
                ["Edit.Replacement"] = "替换文本（要执行的命令）",
                ["Edit.TestInput"] = "测试输入",
                ["Edit.Matched"] = "匹配到：",
                ["Edit.DetectedEmote"] = "检测到情感动作：",
                ["Edit.WillExecute"] = "将执行：",

                // Reaction overrides
                ["Override.Title"] = "全局设置覆盖",
                ["Override.Delay"] = "延迟：",
                ["Override.Cooldown"] = "冷却：",
                ["Override.UseGlobalDelay"] = "使用全局延迟",
                ["Override.CustomDelay"] = "自定义延迟",
                ["Override.UseGlobalCooldown"] = "使用全局冷却",
                ["Override.CustomCooldown"] = "自定义冷却",
                ["Override.SpeakerFilter"] = "发言者过滤：",
                ["Override.UseGlobalSpeaker"] = "使用全局发言者过滤",
                ["Override.CustomSpeaker"] = "自定义发言者过滤",
                ["Override.PlayerLists"] = "玩家名单覆盖",
                ["Override.UseGlobalPlayers"] = "使用全局玩家名单",
                ["Override.Whitelist"] = "覆盖白名单（每行一个玩家名，为空则跳过）：",
                ["Override.Blacklist"] = "覆盖黑名单（每行一个玩家名）：",
                ["Override.CommandLists"] = "命令名单覆盖",
                ["Override.UseGlobalCommands"] = "使用全局命令名单",
                ["Override.CommandWhitelist"] = "覆盖白名单（每行一个命令，为空则跳过）：",
                ["Override.CommandBlacklist"] = "覆盖黑名单（每行一个命令）：",
                ["Override.GameState"] = "游戏状态限制覆盖",
                ["Override.UseGlobalGameState"] = "使用全局游戏状态限制",
                ["Override.ChannelSettings"] = "频道设置覆盖",
                ["Override.UseGlobalChannels"] = "使用全局频道设置",
                ["Override.ChannelNote"] = "自定义频道设置（覆盖全局）：",
                ["Override.UsingGlobalChannels"] = "使用全局频道设置（{0}个频道）",

                // Common settings
                ["Common.AllowAllCommands"] = "允许所有文本命令",
                ["Common.AllowSit"] = "允许 \"sit\" 或 \"groundsit\" 请求",
                ["Common.MotionOnly"] = "仅动作",
                ["Common.Tooltip.AllowAllCommands"] = "如果命令包含子命令，请将序列用括号括起来。\n对于占位符，请将尖括号替换为方括号。\n示例：please do (ac \"Vercure\" [t])",

                // Custom Channels
                ["Custom.EnableDebugTypes"] = "调试日志类型",
                ["Custom.DebugTooltip"] = "启用后将在日志窗口中打印所有游戏消息。\n日志将以日志类型ID为前缀（如果存在类型名称和发送者，还会包含它们）",
                ["Custom.Add"] = "添加",
                ["Custom.ID"] = "ID",
                ["Custom.Label"] = "标签",

                // Speaker filter options
                ["Speaker.All"] = "全部识别",
                ["Speaker.IgnoreSelf"] = "不识别自己",
                ["Speaker.SelfOnly"] = "只识别自己",

                // Units
                ["Unit.Seconds"] = "秒",
                ["Unit.Channels"] = "个频道",

                // Plugin commands
                ["Command.Enabled"] = "启用",
                ["Command.Disabled"] = "禁用",
                ["Command.DebugEnabled"] = "详细调试模式启用",
                ["Command.DebugDisabled"] = "详细调试模式禁用",
                ["Command.Unknown"] = "未知命令：",
                ["Command.Available"] = "可用命令：on, off, debug, list",
                ["Command.LanguageChanged"] = "语言已切换到{0}。请重启插件使设置完全生效。",
                ["Command.RestartRequired"] = "请重启插件以使命令更改生效。",

                // Command settings (new)
                ["UI_CommandSettings"] = "命令设置",
                ["UI_CommandPrefix"] = "命令前缀",
                ["UI_CommandPrefix_Help"] = "自定义聊天命令前缀 (例如：/pm, /puppet)",
                ["UI_EnableShortCommand"] = "启用/pm短命令",
                ["UI_EnableShortCommand_Help"] = "同时注册/pm作为主命令的别名",
                ["UI_AvailableCommands"] = "可用命令",
                ["UI_LanguageSettings"] = "语言设置",
                ["UI_Language"] = "语言",

                // Debug messages
                ["Debug.GameStateFailed"] = "游戏状态检查失败，跳过执行",
                ["Debug.AntiLoop"] = "防循环触发，跳过执行",
                ["Debug.ApplyDelay"] = "应用延迟：{0}秒",
                ["Debug.ProcessingCommand"] = "处理命令 {0}/{1}: {2}",
                ["Debug.Waiting"] = "等待 {0}秒",
                ["Debug.SendingCommand"] = "发送命令：{0}",
                ["Debug.CommandSent"] = "命令已发送：{0}",
                ["Debug.AllCommandsComplete"] = "所有命令执行完成",
                ["Debug.AlreadyExecuting"] = "已有命令正在执行，跳过",
                ["Debug.ChannelNotEnabled"] = "频道 {0} 未启用，跳过",
                ["Debug.Cooldown"] = "冷却中，跳过",
                ["Debug.SenderNotInList"] = "发送者 {0} 不在指定玩家列表中",
                ["Debug.NoEmoteFound"] = "未在消息中找到情感动作：{0}",
                ["Debug.RegexEmpty"] = "正则表达式为空",
                ["Debug.NoMatch"] = "未匹配到模式：{0}",
                ["Debug.MatchSuccess"] = "匹配成功，生成命令：{0}",
                ["Debug.GeneratedCommandEmpty"] = "生成的命令为空",
                ["Debug.ReactionDisabled"] = "反应 {0} 已禁用",
                ["Debug.FilterFailed"] = "反应 {0} 过滤失败",
                ["Debug.FilterPassed"] = "反应 {0} 通过过滤",
                ["Debug.ReceivedMessage"] = "收到消息 - 类型:{0} 发送者:{1} 内容:{2}",
                ["Debug.IgnoreEmote"] = "忽略表情消息：{0}",
                ["Debug.StartProcessing"] = "开始处理 - 消息：{0}",
                ["Debug.WaitCancelled"] = "等待被取消",
                ["Debug.CommandCancelled"] = "命令执行被取消",
                ["Debug.CommandTaskFailed"] = "执行命令任务失败：{0}",
                ["Debug.RegexFailed"] = "正则替换失败：{0}",
                ["Debug.SendFailed"] = "发送命令失败：{0} - {1}",
                ["Debug.FrameworkFailed"] = "框架线程执行失败：{0}",
                ["Debug.ExecuteFailed"] = "执行命令错误：{0}",
                ["Debug.RunMacroFailed"] = "RunMacroAsync 错误：{0}",

                // Error messages
                ["Error.SendCommand"] = "发送命令失败：",
                ["Error.RegexInit"] = "初始化自定义正则表达式失败：",
                ["Error.RegexInitDefault"] = "初始化正则表达式失败：",
                ["Error.LoadEmotes"] = "无法读取情感动作列表",
                ["Error.BuildEmotes"] = "无法构建情感动作列表",
                ["Error.InitializeConfig"] = "配置初始化完成，启用详细调试日志",
            };
        }

        public static string Get(string key)
        {
            var language = Service.configuration?.Language ?? PluginLanguage.English;

            if (Strings.TryGetValue(language, out var langDict) &&
                langDict.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback to English if key not found in selected language
            if (language != PluginLanguage.English &&
                Strings[PluginLanguage.English].TryGetValue(key, out var englishValue))
            {
                return englishValue;
            }

            return key; // Return key as last resort
        }

        // Helper method for formatted strings
        public static string GetFormatted(string key, params object[] args)
        {
            var format = Get(key);
            return string.Format(format, args);
        }
    }
}

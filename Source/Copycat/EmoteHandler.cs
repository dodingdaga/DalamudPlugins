using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using System;
using ECommons.Automation;

namespace Copycat
{
    public class EmoteHandler
    {
        public int PlayerIndex;
        private readonly Lumina.Excel.ExcelSheet<Emote>? emoteSheet;
        public Func<bool> isPlayerLoggedIn = null!;

        public EmoteHandler()
        {
            this.emoteSheet = Service.dataManager?.GetExcelSheet<Emote>();
            if (this.emoteSheet == null) Service.chatGui.Print($"Failed to get Lumina.GetExcelSheet.Emote");
        }

        public void OnEmote(IPlayerCharacter instigator, int emoteId, IPlayerCharacter target, ulong unk, ulong unk2)
        {
            if (!isPlayerLoggedIn() || this.emoteSheet == null || !Service.configuration!.PlayerConfigurations[Service.playerIndex].Enabled)
                return;

            //Service.chatGui.Print($"{instigator.Name}:{emoteSheet.GetRow((uint)emoteId)?.Name}");
            //Beckon == 8

            if (Service.configuration.PlayerConfigurations[Service.playerIndex].Allowed == PlayerFlags.Friend && !instigator.StatusFlags.HasFlag(StatusFlags.Friend))
                return;

            if (Service.configuration.PlayerConfigurations[Service.playerIndex].TargetBack && instigator != null)
                Service.targetManager.Target = instigator;

            //Service.commonBase!.Functions.Chat.SendMessage($"{emoteSheet.GetRow((uint)emoteId)?.TextCommand.Value?.Command} {Service.configuration!.PlayerConfigurations[Service.playerIndex].MotionOnly}");

            //Temporary fix to XivCommon
            
            if (Service.configuration.PlayerConfigurations[Service.playerIndex].MotionOnly.IsNullOrEmpty())
                Chat.Instance.SendMessage($"{emoteSheet.GetRow((uint)emoteId).TextCommand.ValueNullable?.Command.ExtractText()}");
            else
                Chat.Instance.SendMessage($"{emoteSheet.GetRow((uint)emoteId).TextCommand.ValueNullable?.Command.ExtractText()} motion");
        }
    }
}

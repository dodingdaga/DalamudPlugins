using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Hooking;
using System;
using System.Linq;

namespace Copycat
{
    public class EmoteReaderHooks : IDisposable
    {
        public Action<IPlayerCharacter, int, IPlayerCharacter, ulong, ulong> OnEmote = null!;

        public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
        private readonly Hook<OnEmoteFuncDelegate> hookEmote = null!;

        public bool IsValid = false;

        public EmoteReaderHooks()
        {
            try
            {
                hookEmote = Service.gameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
                //hookEmote = Service.gameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("48 89 5c 24 08 48 89 6c 24 10 48 89 74 24 18 48 89 7c 24 20 41 56 48 83 ec 30 4c 8b 74 24 60 48 8b d9 48 81 c1 80 2f 00 00", OnEmoteDetour);
                //var emoteFuncPtr = Service.sigScanner.ScanText("48 89 5c 24 08 48 89 6c 24 10 48 89 74 24 18 48 89 7c 24 20 41 56 48 83 ec 30 4c 8b 74 24 60 48 8b d9 48 81 c1 60 2f 00 00");
                //hookEmote = Hook<OnEmoteFuncDelegate>.FromAddress(emoteFuncPtr, OnEmoteDetour);
                hookEmote.Enable();

                IsValid = true;
            }
            catch (Exception ex)
            {
                Service.logger.Error(ex, "oh noes!");
            }
        }

        public void Dispose()
        {
            hookEmote?.Dispose();
            IsValid = false;
        }

        public void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
        {
            // unk - some field of event framework singleton? doesn't matter here anyway
            // this.chatGui.Print($" ukn:{unk} unk2:{unk2}");
            if (Service.clientState.LocalPlayer != null && targetId == Service.clientState.LocalPlayer.GameObjectId)
            {
                if (Service.objectTable.FirstOrDefault(x => (ulong)x.GameObjectId == targetId) is IPlayerCharacter targetOb && targetOb.ObjectKind == ObjectKind.Player)
                {
                    if (Service.objectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr) is IPlayerCharacter instigatorOb && instigatorOb.ObjectKind == ObjectKind.Player && instigatorOb.GameObjectId != targetId)
                    {
                        OnEmote?.Invoke(instigatorOb, emoteId, targetOb, unk, unk2);
                    }

                }
            }
            hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }
}

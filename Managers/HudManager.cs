﻿using System;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin;
using RezPls.SeFunctions;

namespace RezPls.Managers
{
    public unsafe class HudManager : IDisposable
    {
        private const int GroupMemberOffset    = 0x0B50;
        private const int AllianceMemberOffset = 0x0C4C;
        private const int AllianceSizeOffset   = 0x0CEC;
        private const int GroupMemberSize      = 0x18;
        private const int GroupMemberIdOffset  = 0x10;

        private readonly Hook<UpdatePartyDelegate> _updatePartyHook;
        private          IntPtr                    _hudAgentPtr = IntPtr.Zero;

        public HudManager(SigScanner scanner)
        {
            UpdateParty updateParty = new(scanner);
            _updatePartyHook = updateParty.CreateHook(UpdatePartyHook, this)!;
        }

        private readonly int[,] _idOffsets =
        {
            {
                GroupMemberOffset + 0 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 1 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 2 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 3 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 4 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 5 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 6 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 7 * GroupMemberSize + GroupMemberIdOffset,

                AllianceMemberOffset + 0 * 4,
                AllianceMemberOffset + 1 * 4,
                AllianceMemberOffset + 2 * 4,
                AllianceMemberOffset + 3 * 4,
                AllianceMemberOffset + 4 * 4,
                AllianceMemberOffset + 5 * 4,
                AllianceMemberOffset + 6 * 4,
                AllianceMemberOffset + 7 * 4,
                AllianceMemberOffset + 8 * 4,
                AllianceMemberOffset + 9 * 4,
                AllianceMemberOffset + 10 * 4,
                AllianceMemberOffset + 11 * 4,
                AllianceMemberOffset + 12 * 4,
                AllianceMemberOffset + 13 * 4,
                AllianceMemberOffset + 14 * 4,
                AllianceMemberOffset + 15 * 4,
            },
            {
                GroupMemberOffset + 0 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 1 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 2 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 3 * GroupMemberSize + GroupMemberIdOffset,

                AllianceMemberOffset + 0 * 4,
                AllianceMemberOffset + 1 * 4,
                AllianceMemberOffset + 2 * 4,
                AllianceMemberOffset + 3 * 4,
                AllianceMemberOffset + 8 * 4,
                AllianceMemberOffset + 9 * 4,
                AllianceMemberOffset + 10 * 4,
                AllianceMemberOffset + 11 * 4,
                AllianceMemberOffset + 16 * 4,
                AllianceMemberOffset + 17 * 4,
                AllianceMemberOffset + 18 * 4,
                AllianceMemberOffset + 19 * 4,
                AllianceMemberOffset + 24 * 4,
                AllianceMemberOffset + 25 * 4,
                AllianceMemberOffset + 26 * 4,
                AllianceMemberOffset + 27 * 4,
                AllianceMemberOffset + 32 * 4,
                AllianceMemberOffset + 33 * 4,
                AllianceMemberOffset + 34 * 4,
                AllianceMemberOffset + 35 * 4,
            },
        };

        public int GroupSize
            => *(int*) (_hudAgentPtr + AllianceSizeOffset);

        public bool IsAlliance
            => GroupSize == 8;

        public bool IsPvP
            => GroupSize == 4;

        public bool IsGroup
            => GroupSize == 0;

        public (int groupIdx, int idx)? FindGroupMemberById(uint actorId)
        {
            if (_hudAgentPtr == IntPtr.Zero)
                return null;

            var groupSize = GroupSize;
            int numGroups;
            if (groupSize == 0)
            {
                numGroups = 1;
                groupSize = 8;
            }
            else
            {
                numGroups = groupSize == 4 ? 6 : 3;
            }

            var count = numGroups * groupSize;
            var pvp   = groupSize == 4 ? 1 : 0;
            for (var i = 0; i < count; ++i)
            {
                if (*(uint*) (_hudAgentPtr + _idOffsets[pvp, i]) == actorId)
                    return (i / groupSize, i % groupSize);
            }

            return null;
        }

        private void UpdatePartyHook(IntPtr hudAgent)
        {
            _hudAgentPtr = hudAgent;
            PluginLog.LogVerbose($"Obtained HUD agent at address 0x{_hudAgentPtr.ToInt64():X16}.");
            _updatePartyHook.Original(hudAgent);
            _updatePartyHook.Disable();
            _updatePartyHook.Dispose();
        }

        public void Dispose()
        {
            _updatePartyHook?.Dispose();
        }
    }
}
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace RezPls.Managers;

public unsafe class HudManager
{
    private static bool TryAgent(out AgentHUD* agent)
    {
        agent = AgentHUD.Instance();
        return agent != null;
    }

    private static uint GetIdPvP(AgentHUD* agent, int idx)
    {
        if (idx < 4)
            return agent->PartyMembers[idx].EntityId;

        idx = ((idx >> 2) << 3) + (idx & 0b11);
        return agent->RaidMemberIds[idx];
    }

    private static uint GetId(AgentHUD* agent, int idx)
        => idx < 8
            ? agent->PartyMembers[idx].EntityId
            : agent->RaidMemberIds[idx - 8];

    public int GroupSize
        => TryAgent(out var agent) ? agent->RaidGroupSize : 0;

    public bool IsAlliance
        => GroupSize == 8;

    public bool IsPvP
        => GroupSize == 4;

    public bool IsGroup
        => GroupSize == 0;

    public (int groupIdx, int idx)? FindGroupMemberById(ulong actorId)
    {
        if (!TryAgent(out var agent))
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
        if (groupSize == 4) // PVP mode
            for (var i = 0; i < count; ++i)
            {
                var id = GetIdPvP(agent, i);
                if (id == actorId)
                    return (i / groupSize, i % groupSize);
            }
        else
            for (var i = 0; i < count; ++i)
            {
                var id = GetId(agent, i);
                if (id == actorId)
                    return (i / groupSize, i % groupSize);
            }

        return null;
    }
}

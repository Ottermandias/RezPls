using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RezPls.Enums;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace RezPls.Managers;

public enum CastType : byte
{
    None,
    Raise,
    Dispel,
};

public readonly struct ActorState(ulong caster, CastType type, bool hasStatus)
{
    public readonly ulong    Caster    = caster;
    public readonly CastType Type      = type;
    public readonly bool     HasStatus = hasStatus;

    public ActorState SetHasStatus(bool hasStatus)
        => new(Caster, Type, hasStatus);

    public ActorState SetCasting(ulong target, CastType type)
        => new(target, type, HasStatus);

    public static ActorState Nothing = new(0, CastType.None, false);
}

public class ActorWatcher : IDisposable
{
    public static    int                       TestMode;
    private          bool                      _outsidePvP = true;
    private          bool                      _enabled;
    private readonly StatusSet                 _statusSet;
    private const    int                       ActorTablePlayerLength = 200;
    private readonly ExcelSheet<TerritoryType> _territories;

    public readonly Dictionary<ulong, ActorState> RezList        = new(128);
    public readonly Dictionary<ulong, string>     ActorNames     = new();
    public readonly Dictionary<ulong, Vector3>    ActorPositions = new();
    public          (ulong, ActorState)           PlayerRez      = (0, ActorState.Nothing);

    public ActorWatcher(StatusSet statusSet)
    {
        _statusSet   = statusSet;
        _territories = Dalamud.GameData.GetExcelSheet<TerritoryType>()!;

        CheckPvP(Dalamud.ClientState.TerritoryType);
    }

    public void Enable()
    {
        if (_enabled)
            return;

        Dalamud.Framework.Update             += OnFrameworkUpdate;
        Dalamud.ClientState.TerritoryChanged += CheckPvP;
        _enabled                             =  true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        Dalamud.Framework.Update             -= OnFrameworkUpdate;
        Dalamud.ClientState.TerritoryChanged -= CheckPvP;
        _enabled                             =  false;
        RezList.Clear();
        PlayerRez = (0, ActorState.Nothing);
    }

    public void Dispose()
        => Disable();

    private void CheckPvP(ushort territoryId)
    {
        var row = _territories.GetRow(territoryId);
        _outsidePvP = !(row?.IsPvpZone ?? false);
    }

    public (Job job, byte level) CurrentPlayerJob()
    {
        var player = Dalamud.ClientState.LocalPlayer;
        if (player == null || !IsPlayer(player))
            return (Job.ADV, 0);

        return (PlayerJob(player), player.Level);
    }

    private static unsafe (uint, GameObjectId) GetCurrentCast(IBattleChara player)
    {
        var     battleChara = (BattleChara*)player.Address;
        ref var cast        = ref *battleChara->GetCastInfo();
        if (cast.ActionType != ActionType.Action)
            return (0, 0);

        return (cast.ActionId, cast.TargetId);
    }

    private static CastType GetCastType(uint castId)
    {
        return castId switch
        {
            173   => CastType.Raise,  // ACN, SMN, SCH
            125   => CastType.Raise,  // CNH, WHM
            3603  => CastType.Raise,  // AST
            18317 => CastType.Raise,  // BLU
            208   => CastType.Raise,  // WHM LB3
            4247  => CastType.Raise,  // SCH LB3
            4248  => CastType.Raise,  // AST LB3
            24859 => CastType.Raise,  // SGE LB3
            7523  => CastType.Raise,  // RDM
            22345 => CastType.Raise,  // Lost Sacrifice, Bozja
            20730 => CastType.Raise,  // Lost Arise, Bozja
            12996 => CastType.Raise,  // Raise L, Eureka
            24287 => CastType.Raise,  // Egeiro
            7568  => CastType.Dispel, // Esuna
            3561  => CastType.Dispel, // The Warden's Paean, instant, so irrelevant
            18318 => CastType.Dispel, // Exuviation

            _ => CastType.None,
        };
    }

    private static bool IsPlayer(IGameObject actor)
        => actor.ObjectKind == ObjectKind.Player;

    private static bool IsDead(ICharacter player)
        => player.CurrentHp <= 0;

    private static Job PlayerJob(ICharacter player)
        => (Job)player.ClassJob.Id;

    private unsafe CastType HasStatus(IBattleChara player)
    {
        var battleChar = (BattleChara*)player.Address;
        var statuses   = battleChar->GetStatusManager()->Status;
        foreach (ref var status in statuses)
        {
            switch (status.StatusId)
            {
                case 148 or 1140: return CastType.Raise;
                case 0:           continue;
                default:
                    if (_statusSet.IsEnabled(status.StatusId))
                        return CastType.Dispel;

                    break;
            }
        }

        return CastType.None;
    }

    private void IterateActors()
    {
        for (var i = 0; i < ActorTablePlayerLength; i += 2)
        {
            var actor = Dalamud.Objects[i];
            if (actor is not IPlayerCharacter player)
                continue;

            var actorId = player.GameObjectId;
            if (IsDead(player))
            {
                ActorPositions[actorId] = player.Position;
                if (HasStatus(player) == CastType.Raise)
                    RezList[actorId] = new ActorState(0, CastType.Raise, false);
                ActorNames.TryAdd(actorId, player.Name.TextValue);
            }
            else
            {
                var (castId, castTarget) = GetCurrentCast(player);
                var castType    = GetCastType(castId);
                var dispellable = HasStatus(player) == CastType.Dispel;
                if (castType == CastType.None && !dispellable)
                    continue;

                if (dispellable)
                {
                    RezList[actorId] = RezList.TryGetValue(actorId, out var state)
                        ? state.SetHasStatus(true)
                        : new ActorState(0, CastType.None, true);
                    ActorPositions[actorId] = player.Position;
                }

                ActorNames.TryAdd(actorId, player.Name.TextValue);
                if (i == 0)
                    PlayerRez = (castTarget, new ActorState(actorId, castType, false));

                if (castType == CastType.Raise
                 && (!RezList.TryGetValue(castTarget, out var caster) || caster.Caster == PlayerRez.Item2.Caster))
                    RezList[castTarget] = RezList.TryGetValue(castTarget, out var state)
                        ? state.SetCasting(actorId, castType)
                        : new ActorState(actorId, castType, false);
                if (castType == CastType.Dispel)
                    RezList[castTarget] = RezList.TryGetValue(castTarget, out var state)
                        ? state.SetCasting(actorId, castType)
                        : new ActorState(actorId, castType, false);
            }
        }
    }

    private void ActorNamesAdd(IGameObject actor)
        => ActorNames.TryAdd(actor.EntityId, actor.Name.ToString());

    private unsafe void HandleTestMode()
    {
        var p = Dalamud.ClientState.LocalPlayer;
        if (p == null)
            return;

        ActorNamesAdd(p);
        ActorPositions[p.GameObjectId] = p.Position;

        var t         = Dalamud.Targets.Target ?? p;
        var tObjectId = Dalamud.Targets.Target?.GameObjectId ?? 10;
        switch (TestMode)
        {
            case 1:
                RezList[p.GameObjectId] = new ActorState(0, CastType.Raise, false);
                return;
            case 2:
                RezList[p.GameObjectId] = new ActorState(t.GameObjectId, CastType.Raise, false);
                ActorNamesAdd(t);
                return;
            case 3:
                RezList[p.GameObjectId] = new ActorState(tObjectId, CastType.Raise, false);
                PlayerRez               = (p.GameObjectId, new ActorState(p.GameObjectId, CastType.Raise, false));
                return;
            case 4:
                RezList[p.GameObjectId] = new ActorState(0, CastType.None, true);
                return;
            case 5:
                RezList[p.GameObjectId] = new ActorState(t.GameObjectId, CastType.Dispel, true);
                ActorNamesAdd(t);
                return;
            case 6:
                RezList[p.GameObjectId] = new ActorState(tObjectId, CastType.Dispel, false);
                PlayerRez               = (p.GameObjectId, new ActorState(p.GameObjectId, CastType.Raise, true));
                return;
        }
    }


    public void OnFrameworkUpdate(object _)
    {
        if (!_outsidePvP)
            return;

        RezList.Clear();
        PlayerRez = (0, PlayerRez.Item2);
        if (TestMode == 0)
            IterateActors();
        else
            HandleTestMode();
    }
}

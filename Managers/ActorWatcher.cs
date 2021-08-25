using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using RezPls.Enums;

namespace RezPls.Managers
{
    public enum CastType : byte
    {
        None,
        Raise,
        Dispel,
    };

    public readonly struct ActorState
    {
        public readonly uint     Caster;
        public readonly CastType Type;
        public readonly bool     HasStatus;

        public ActorState(uint caster, CastType type, bool hasStatus)
        {
            Caster    = caster;
            Type      = type;
            HasStatus = hasStatus;
        }

        public ActorState SetHasStatus(bool hasStatus)
            => new(Caster, Type, hasStatus);

        public ActorState SetCasting(uint target, CastType type)
            => new(target, type, HasStatus);

        public static ActorState Nothing = new(0, CastType.None, false);
    }

    public class ActorWatcher : IDisposable
    {
        private          bool                      _outsidePvP = true;
        private          bool                      _enabled;
        private readonly StatusSet                 _statusSet;
        private readonly IntPtr                    _actorTablePtr;
        private const    int                       ActorTablePlayerLength = 200;
        private readonly ExcelSheet<TerritoryType> _territories;

        public readonly Dictionary<uint, ActorState> RezList        = new(128);
        public readonly Dictionary<uint, string>     ActorNames     = new();
        public readonly Dictionary<uint, Vector3>    ActorPositions = new();
        public          (uint, ActorState)           PlayerRez      = (0, ActorState.Nothing);

        public ActorWatcher(StatusSet statusSet)
        {
            _statusSet   = statusSet;
            _territories = Dalamud.GameData.GetExcelSheet<TerritoryType>()!;

            CheckPvP(null!, Dalamud.ClientState.TerritoryType);

            _actorTablePtr = Dalamud.Objects.Address;
        }

        public void Enable()
        {
            if (_enabled)
                return;

            Dalamud.Framework.Update             += OnFrameworkUpdate;
            Dalamud.ClientState.TerritoryChanged += CheckPvP;
            _enabled                            =  true;
        }

        public void Disable()
        {
            if (!_enabled)
                return;

            Dalamud.Framework.Update             -= OnFrameworkUpdate;
            Dalamud.ClientState.TerritoryChanged -= CheckPvP;
            _enabled                            =  false;
            RezList.Clear();
            PlayerRez = (0, ActorState.Nothing);
        }

        public void Dispose()
            => Disable();

        private void CheckPvP(object? _, ushort territoryId)
        {
            var row = _territories.GetRow(territoryId);
            _outsidePvP = !(row?.IsPvpZone ?? false);
        }

        public unsafe (Job job, byte level) CurrentPlayerJob()
        {
            var player = *(byte**) _actorTablePtr;
            if (player == null || !IsPlayer(player))
                return (Job.ADV, 0);

            return (PlayerJob(player), PlayerLevel(player));
        }

        private static unsafe ushort GetCurrentCast(byte* actorPtr)
        {
            const int    currentCastTypeOffset = 0x1B82;
            const int    currentCastIdOffset   = 0x1B84;
            const ushort currentCastIdSpell    = 0x01;

            if (*(ushort*) (actorPtr + currentCastTypeOffset) != currentCastIdSpell)
                return 0;

            return *(ushort*) (actorPtr + currentCastIdOffset);
        }

        private static unsafe uint GetCastTarget(byte* actorPtr)
        {
            const int currentCastTargetOffset = 0x1B90;
            return *(uint*) (actorPtr + currentCastTargetOffset);
        }

        private static unsafe uint GetActorId(byte* actorPtr)
        {
            const int actorIdOffset = 0x74;
            return *(uint*) (actorPtr + actorIdOffset);
        }

        private static unsafe string GetActorName(byte* actorPtr)
        {
            const int actorNameOffset = 0x30;
            const int actorNameLength = 30;

            return Marshal.PtrToStringAnsi(new IntPtr(actorPtr) + actorNameOffset, actorNameLength).TrimEnd('\0');
        }

        private static unsafe Vector3 GetActorPosition(byte* actorPtr)
        {
            const int actorPositionOffset = 0xA0;
            return new Vector3
            {
                X = *(float*) (actorPtr + actorPositionOffset),
                Y = *(float*) (actorPtr + actorPositionOffset + 8),
                Z = *(float*) (actorPtr + actorPositionOffset + 4),
            };
        }

        private static unsafe CastType GetCastType(byte* actorPtr)
        {
            return GetCurrentCast(actorPtr) switch
            {
                173   => CastType.Raise, // ACN, SMN, SCH
                125   => CastType.Raise, // CNH, WHM
                3603  => CastType.Raise, // AST
                18317 => CastType.Raise, // BLU
                208   => CastType.Raise, // WHM LB3
                4247  => CastType.Raise, // SCH LB3
                4248  => CastType.Raise, // AST LB3
                7523  => CastType.Raise, // RDM
                22345 => CastType.Raise, // Lost Sacrifice, Bozja
                20730 => CastType.Raise, // Lost Arise, Bozja
                12996 => CastType.Raise, // Raise L, Eureka

                7568  => CastType.Dispel, // Esuna
                3561  => CastType.Dispel, // The Warden's Paean, instant, so irrelevant
                18318 => CastType.Dispel, // Exuviation

                _ => CastType.None,
            };
        }

        private static unsafe bool IsPlayer(byte* actorPtr)
        {
            const int  objectKindOffset = 0x8C;
            const byte playerObjectKind = (byte) ObjectKind.Player;

            return *(actorPtr + objectKindOffset) == playerObjectKind;
        }

        private static unsafe bool IsDead(byte* actorPtr)
        {
            const int currentHpOffset = 0x1C4;

            return *(int*) (actorPtr + currentHpOffset) == 0;
        }

        private static unsafe Job PlayerJob(byte* actorPtr)
        {
            const int jobOffset = 0x1E2;
            return *(Job*) (actorPtr + jobOffset);
        }

        private static unsafe byte PlayerLevel(byte* actorPtr)
        {
            const int jobOffset = 0x1E3;
            return *(actorPtr + jobOffset);
        }

        private unsafe CastType HasStatus(byte* actorPtr)
        {
            const int statusEffectsOffset = 0x19F8;
            const int statusEffectSize    = 12;
            const int maxStatusEffects    = 20;

            var start = actorPtr + statusEffectsOffset;
            var end   = start + statusEffectSize * maxStatusEffects;
            for (; start < end; start += 12)
            {
                var id = *(ushort*) start;

                if (id == 148 || id == 1140)
                    return CastType.Raise;

                if (_statusSet.IsEnabled(id))
                    return CastType.Dispel;
            }

            return CastType.None;
        }

        private unsafe void IterateActors()
        {
            var current = (byte**) _actorTablePtr;
            var end     = current + ActorTablePlayerLength;
            for (; current < end; current += 2)
            {
                var actor = *current;
                if (actor == null || !IsPlayer(actor))
                    continue;

                if (IsDead(actor))
                {
                    var actorId = GetActorId(actor);
                    ActorPositions[actorId] = GetActorPosition(actor);
                    if (HasStatus(actor) == CastType.Raise)
                        RezList[actorId] = new ActorState(0, CastType.Raise, false);
                    if (!ActorNames.ContainsKey(actorId))
                        ActorNames.Add(actorId, GetActorName(actor));
                }
                else
                {
                    var cast        = GetCastType(actor);
                    var dispellable = HasStatus(actor) == CastType.Dispel;
                    if (cast == CastType.None && !dispellable)
                        continue;

                    var actorId = GetActorId(actor);
                    if (dispellable)
                    {
                        RezList[actorId] = RezList.TryGetValue(actorId, out var state)
                            ? state.SetHasStatus(true)
                            : new ActorState(0, CastType.None, true);
                        ActorPositions[actorId] = GetActorPosition(actor);
                    }

                    if (!ActorNames.ContainsKey(actorId))
                        ActorNames.Add(actorId, GetActorName(actor));
                    var corpseId = GetCastTarget(actor);
                    if (current == (byte**) _actorTablePtr)
                        PlayerRez = (corpseId, new ActorState(actorId, cast, false));

                    if (cast == CastType.Raise && (!RezList.TryGetValue(corpseId, out var caster) || caster.Caster == PlayerRez.Item2.Caster))
                        RezList[corpseId] = RezList.TryGetValue(corpseId, out var state)
                            ? state.SetCasting(actorId, cast)
                            : new ActorState(actorId, cast, false);
                    if (cast == CastType.Dispel)
                        RezList[corpseId] = RezList.TryGetValue(corpseId, out var state)
                            ? state.SetCasting(actorId, cast)
                            : new ActorState(actorId, cast, false);
                }
            }
        }

        public void OnFrameworkUpdate(object _)
        {
            if (!_outsidePvP)
                return;

            RezList.Clear();
            PlayerRez = (0, PlayerRez.Item2);
            IterateActors();
        }
    }
}

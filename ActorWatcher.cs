using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using RezzPls.SeFunctions;

namespace RezzPls
{
    public class ActorWatcher : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly int                    _actorsPerUpdate;
        private          int                    _currentActor;

        public ActorWatcher(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface                         =  pluginInterface;
            _pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            _actorsPerUpdate                         =  32;
        }

        public void Dispose()
        {
            _pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
        }

        private static unsafe ushort GetCurrentCast(IntPtr actorPtr)
        {
            const int currentCastIdOffset = 0x1B64;
            return *(ushort*) (actorPtr + currentCastIdOffset);
        }

        private static unsafe int GetCastTarget(IntPtr actorPtr)
        {
            const int currentCastTargetOffset = 0x1B70;
            return *(int*) (actorPtr + currentCastTargetOffset);
        }

        private unsafe bool IsCastingResurrection(IntPtr actorPtr)
        {
            switch (GetCurrentCast(actorPtr))
            {
                case 173:   // ACN, SMN, SCH
                case 125:   // CNH, WHM
                case 3603:  // AST
                case 18317: // BLU
                case 208:   // WHM LB3
                case 4247:  // SCH LB3
                case 4248:  // AST LB3
                case 7523:  // RDM
                case 22345: // Lost Sacrifice, Bozja
                case 20730: // Lost Arise, Bozja
                case 12996: // Raise L, Eureka
                    return true;
            }

            return false;
        }

        private static bool IsRaised(Actor actor)
            => actor.StatusEffects.Any(s => s.EffectId == 148 || s.EffectId == 1140);

        private Actor? GetActorById(int actorId)
            => _pluginInterface.ClientState.Actors.FirstOrDefault(a => a.ActorId == actorId);

        public readonly Dictionary<int, (Actor? caster, Actor? target)> RezzList   = new(128);
        public readonly Dictionary<int, Actor>                          RezzedList = new(128);

        public void OnFrameworkUpdate(object _)
        {
            var length    = Math.Max(_pluginInterface.ClientState.Actors.Length - 2, 256);
            var numActors = _actorsPerUpdate == 0 ? length / 2 + 1 : Math.Max(length / 2 + 1, _actorsPerUpdate);
            for (var i = 0; i < numActors; ++i)
            {
                _currentActor = _currentActor >= length ? 0 : _currentActor + 2;
                var actor = _pluginInterface.ClientState.Actors[_currentActor];
                if ((actor?.ObjectKind ?? 0) != ObjectKind.Player)
                    continue;

                if (IsRaised(actor!))
                    RezzedList[actor!.ActorId] = actor;
                else
                    RezzedList.Remove(actor!.ActorId);

                if (!IsCastingResurrection(actor!.Address))
                {
                    RezzList.Remove(actor.ActorId);
                    continue;
                }

                if (RezzList.ContainsKey(actor!.ActorId))
                    continue;

                var targetId = GetCastTarget(actor.Address);
                var target   = GetActorById(targetId);
                RezzList.Add(actor.ActorId, (actor, target));
            }
        }
    }
}

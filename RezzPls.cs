using Dalamud.Plugin;

namespace RezzPls
{
    public class RezzPls : IDalamudPlugin
    {
        public string Name
            => "RezzPls";

        private DalamudPluginInterface? _pluginInterface;
        private ActorWatcher?           _actorWatcher;
        private Interface?              _interface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            _actorWatcher    = new ActorWatcher(_pluginInterface);
            _interface       = new Interface(_pluginInterface, _actorWatcher);
        }

        public void Dispose()
        {
            _actorWatcher?.Dispose();
            _pluginInterface?.Dispose();
        }
    }
}

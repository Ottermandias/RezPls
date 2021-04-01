﻿using Dalamud.Plugin;

namespace RezzPls
{
    public class RezzPls : IDalamudPlugin
    {
        public string Name
            => "RezzPls";

        private DalamudPluginInterface? _pluginInterface;
        private ActorWatcher?           _actorWatcher;
        private Overlay?                _overlay;
        private Interface?              _interface;
        private RezzPlsConfig?          _config;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            _config          = _pluginInterface.GetPluginConfig() as RezzPlsConfig;
            if (_config == null)
            {
                _config = new RezzPlsConfig();
                Save();
            }

            _actorWatcher = new ActorWatcher(_pluginInterface);
            _overlay      = new Overlay(_pluginInterface, _actorWatcher, _config);
            _interface    = new Interface(_pluginInterface, this, _config);
            if (_config.Enabled)
                Enable();
        }

        public void Save()
            => _pluginInterface!.SavePluginConfig(_config);

        public void Enable()
        {
            _actorWatcher!.Enable();
            _overlay!.Enable();
        }

        public void Disable()
        {
            _actorWatcher!.Disable();
            _overlay!.Disable();
        }

        public void Dispose()
        {
            _interface?.Dispose();
            _overlay?.Dispose();
            _actorWatcher?.Dispose();
            _pluginInterface?.Dispose();
        }
    }
}
